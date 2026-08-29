#!/usr/bin/env python3
"""MoonWeChat PC 网关：把 pyweixin RPA 暴露成手机瘦客户端已在用的 HTTP 契约。"""

from __future__ import annotations

import argparse
import base64
import hashlib
import ipaddress
import json
import os
import queue
import re
import secrets
import threading
import time
import traceback
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, urlparse

from weixin_ui_fallback import WeixinCanvasUi

ROOT = Path(__file__).resolve().parent
CONFIG_PATH = ROOT / "config.json"
DATA_DIR = ROOT / "data"
QR_PATH = DATA_DIR / "login_qr.png"
DEFAULT_PORT = 18765

#: Cap on retained inbox/dedup state so a long-running gateway cannot grow
#: without bound.
INBOX_CAP = 2000

#: How long a login-state verdict from status() stays reusable.  Short enough that
#: a genuine logout is noticed promptly, long enough that one client poll cycle
#: (which hits status() through several path aliases) probes Weixin only once.
PROBE_CACHE_TTL = 8.0

#: Cooldown after a failed RPA read before the UI is driven for it again.
#: The client walks several candidate paths per logical call and retries each
#: with GET (see GetSyncMsgAsync), so reporting a failure honestly — which we now
#: do — would otherwise turn one poll into four 90s RPA attempts queued back to
#: back.  Within the cooldown the cached error is replayed without touching Weixin.
READ_FAIL_COOLDOWN = 20.0

# 与客户端 LooksLikeOffline / ParseLoginStatus / ParseFriendList / ParseSyncMsg 对齐
OK = 200

#: 「已登录，不需要二维码」不是成功。客户端 Interpret() 优先读 Success，但任何
#: 只看 Code 的消费者会把 Code=200 当成功，所以这里必须用一个非 200 的码；也不能
#: 用 fail() 的默认 300 —— 客户端把 300 解释成「需重新登录」，语义正好相反。
ALREADY_ONLINE = 409

#: 通讯录/消息同步这类「RPA 失败」与「真的没有数据」必须能被手机区分开。
UI_UNAVAILABLE = 503


def now_unix() -> int:
    return int(time.time())


def ok(data: Any = None, text: str = "ok") -> dict[str, Any]:
    return {"Code": OK, "Success": True, "Text": text, "Data": data if data is not None else {}}


def fail(text: str, code: int = 300, data: Any = None) -> dict[str, Any]:
    return {"Code": code, "Success": False, "Text": text, "Data": data if data is not None else {}}


class WeixinSyncError(RuntimeError):
    """An RPA read failed.  Distinct from "there was genuinely nothing to read".

    The gateway must never answer a failed read with an empty-but-successful
    payload: the phone cannot tell that apart from an empty contact list or a
    quiet inbox, and shows a confident "synced" state built on nothing.
    """


def load_config() -> dict[str, Any]:
    if CONFIG_PATH.exists():
        try:
            cfg = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
        except Exception:
            cfg = {}
        if isinstance(cfg, dict) and cfg.get("token"):
            # Older configs shipped a well-known admin key.  Anyone on the LAN
            # who knew the literal could mint a fresh token via
            # /Admin/GenAuthKey and take over the gateway, which drives the
            # user's real Weixin.  Replace it on load.
            if cfg.get("admin_key") in (None, "", "moonwechat_local_2026"):
                cfg["admin_key"] = secrets.token_hex(16)
                save_config(cfg)
                print("[warn] 检测到默认/空 adminKey，已随机重置。请用下面打印的新值。")
            return cfg

    # The admin key is never a hardcoded constant: it gates token minting.
    cfg = {
        "admin_key": secrets.token_hex(16),
        "token": secrets.token_hex(16),
        "host": "0.0.0.0",
        "port": DEFAULT_PORT,
    }
    save_config(cfg)
    return cfg


def save_config(cfg: dict[str, Any]) -> None:
    """Persist config, then narrow its permissions.

    config.json holds the token and adminKey in cleartext, so it is created with
    owner-only access rather than the default inherited ACL.  On Windows the
    chmod bits are largely advisory, so also strip inherited ACEs via icacls
    when it is available; failure to tighten is never fatal.
    """
    is_new = not CONFIG_PATH.exists()
    CONFIG_PATH.write_text(json.dumps(cfg, ensure_ascii=False, indent=2), encoding="utf-8")
    try:
        os.chmod(CONFIG_PATH, 0o600)
    except OSError:
        pass
    if is_new and os.name == "nt":
        _restrict_windows_acl(CONFIG_PATH)


def _restrict_windows_acl(path: Path) -> None:
    """Best-effort: owner-only ACL on the credential file."""
    user = os.environ.get("USERNAME") or ""
    if not user:
        return
    try:
        import subprocess

        subprocess.run(
            ["icacls", str(path), "/inheritance:r", "/grant:r", "%s:(R,W)" % user],
            check=False,
            capture_output=True,
            timeout=10,
        )
    except Exception:
        pass


class JobRunner:
    """串行执行 RPA，避免两个 pywinauto 操作同时抢微信窗口。"""

    def __init__(self) -> None:
        self._q: queue.Queue[tuple[Any, Any]] = queue.Queue()
        self._t = threading.Thread(target=self._loop, name="pyweixin-rpa", daemon=True)
        self._t.start()

    def _loop(self) -> None:
        while True:
            fn, box = self._q.get()
            try:
                box["result"] = fn()
            except Exception as ex:
                box["error"] = ex
                box["trace"] = traceback.format_exc()
            finally:
                box["evt"].set()

    def run(self, fn, timeout: float = 90.0):
        box: dict[str, Any] = {"evt": threading.Event()}
        self._q.put((fn, box))
        if not box["evt"].wait(timeout):
            # Deliberately avoids the words 未登录 / 掉线 / 离线: the client's
            # LooksLikeOffline() keys on those, and a covered window is not a
            # logged-out account.  Saying "尚未登录" here would make the phone
            # drop a working session and send the user back to the QR screen.
            raise TimeoutError("RPA 操作超时（微信窗口可能被其它窗口挡住，或微信正忙）")
        if "error" in box:
            raise box["error"]
        return box.get("result")


class WeixinBackend:
    def __init__(self, mock: bool) -> None:
        self.mock = mock
        self.runner = JobRunner()
        self.lock = threading.RLock()
        self.seen_msg_ids: set[str] = set()
        self.inbox: list[dict[str, Any]] = []
        self.contacts: dict[str, dict[str, Any]] = {}
        self.groups: set[str] = set()
        self.self_info = {
            "wxid": "wxid_pyweixin",
            "NickName": "我",
            "UserName": "wxid_pyweixin",
            "Alias": "",
        }
        self.login_state = "unknown"
        self.qr_uuid: str | None = None
        self.qr_ready_at = 0.0
        self.last_probe = 0.0
        #: Cached status() verdict guarded by last_probe.  See PROBE_CACHE_TTL.
        self._probe_result: dict[str, Any] | None = None
        self._wx = None
        self._canvas_ui = WeixinCanvasUi()
        self.last_ui_error = ""
        #: kind -> (failed_at, message).  See READ_FAIL_COOLDOWN.
        self._read_failures: dict[str, tuple[float, str]] = {}
        DATA_DIR.mkdir(parents=True, exist_ok=True)
        if mock:
            self._seed_mock()

    # -- failed-read cooldown -------------------------------------------

    def _recent_failure(self, kind: str) -> str:
        """Return the cached error for ``kind`` while its cooldown is active."""
        with self.lock:
            entry = self._read_failures.get(kind)
            if not entry:
                return ""
            failed_at, message = entry
            if time.time() - failed_at < READ_FAIL_COOLDOWN:
                return message or "上一次读取失败"
            del self._read_failures[kind]
            return ""

    def _note_failure(self, kind: str, message: str) -> None:
        with self.lock:
            self._read_failures[kind] = (time.time(), message)
        self.last_ui_error = message

    def _clear_failure(self, kind: str) -> None:
        with self.lock:
            self._read_failures.pop(kind, None)
        self.last_ui_error = ""

    def _seed_mock(self) -> None:
        self.login_state = "online"
        self.self_info = {
            "wxid": "wxid_mock_self",
            "NickName": "大月（演示）",
            "UserName": "wxid_mock_self",
            "Alias": "moonwechat",
        }
        for name, remark, group in (
            ("文件传输助手", "", False),
            ("苏晚", "苏晚", False),
            ("阿KEN", "", False),
            ("白泽策划组", "", True),
        ):
            dto = self._contact(name, name, remark, group)
            self.contacts[dto["UserName"]] = dto
            if group:
                self.groups.add(name)
        self.inbox.append(
            self._msg("苏晚", self.self_info["wxid"], "网关 mock 已就绪，可以发文本试试。", 1)
        )

    def _import_wx(self):
        if self._wx is not None:
            return self._wx
        from pyweixin import Contacts, Files, FriendSettings, Messages, Moments, Navigator, Tools
        from pyweixin.Config import GlobalConfig

        GlobalConfig.close_weixin = False
        GlobalConfig.is_maximize = False
        self._wx = {
            "Contacts": Contacts,
            "Files": Files,
            "FriendSettings": FriendSettings,
            "Messages": Messages,
            "Moments": Moments,
            "Navigator": Navigator,
            "Tools": Tools,
        }
        return self._wx

    @staticmethod
    def display_name(user: str) -> str:
        name = (user or "").strip()
        if name.lower() == "filehelper":
            return "文件传输助手"
        if name.lower().endswith("@chatroom"):
            return name[: -len("@chatroom")]
        return name

    @staticmethod
    def canon_user(user: str, group: bool = False) -> str:
        name = WeixinBackend.display_name(user)
        if group and name and not name.lower().endswith("@chatroom"):
            return name + "@chatroom"
        return name

    def _is_group(self, user: str) -> bool:
        raw = self.display_name(user)
        return raw in self.groups or (user or "").lower().endswith("@chatroom")

    def _contact(self, user: str, nick: str, remark: str = "", group: bool = False) -> dict[str, Any]:
        user = self.canon_user(user, group)
        return {
            "UserName": user,
            "userName": user,
            "Wxid": user,
            "NickName": nick or user,
            "Remark": remark or "",
            "Alias": "",
            "BigHeadImgUrl": "",
            "SmallHeadImgUrl": "",
            "Province": "",
            "City": "",
            "Signature": "",
            "IsChatroom": bool(group),
        }

    def _msg(
        self,
        frm: str,
        to: str,
        content: str,
        msg_type: int = 1,
        msg_id: str | None = None,
    ) -> dict[str, Any]:
        mid = msg_id or uuid.uuid4().hex
        return {
            "MsgId": mid,
            "NewMsgId": mid,
            "FromUserName": frm,
            "ToUserName": to,
            "Content": content,
            "MsgType": msg_type,
            "CreateTime": now_unix(),
            "PushContent": content,
        }

    def _guess_msg_type(self, content: str, hinted: str = "") -> int:
        text = (hinted or content or "").lower()
        if any(k in text for k in ("图片", "image", "[图片]")):
            return 3
        if any(k in text for k in ("语音", "voice", "[语音]")):
            return 34
        if any(k in text for k in ("视频", "video", "[视频]")):
            return 43
        if any(k in text for k in ("文件", "file", "[文件]")):
            return 49
        if any(k in text for k in ("位置", "location", "[位置]")):
            return 48
        if any(k in text for k in ("链接", "http://", "https://")):
            return 49
        return 1

    def status(self, fresh: bool = False) -> dict[str, Any]:
        """Probe the login state.  ``fresh=True`` bypasses the short-lived cache.

        ``last_probe`` was declared but never read.  It matters now: this runs a
        45s RPA probe and is reached from four routes (heartbeat, profile,
        capture_qr, check_qr), each of which the client tries under several path
        aliases — so one poll cycle could queue the same probe five times back to
        back.  ``check_qr`` passes ``fresh=True`` because it is explicitly
        watching for a login transition and must not read a stale "offline".
        """
        if self.mock:
            self.login_state = "online"
            return {"online": True, **self.self_info, "mock": True}

        if not fresh:
            cached = self._recent_probe()
            if cached is not None:
                return cached

        def work():
            wx = self._import_wx()
            running = bool(wx["Tools"].is_weixin_running())
            if not running:
                return {"online": False, "reason": "微信未运行"}
            try:
                info = wx["Contacts"].check_my_info(close_weixin=False, is_maximize=False) or {}
            except Exception as ex:
                name = type(ex).__name__
                if "NotLogin" in name or "Login" in name:
                    return {"online": False, "reason": "未登录"}
                # Weixin 4.1.12 renders the logged-in main window as a Qt
                # canvas, so pyweixin cannot read its mmui::MainWindow tree.
                # The wxid folder is only used as a login signal here; actual
                # operations still go through the visible UI fallback.
                try:
                    wxid = wx["Tools"].get_current_wxid() or ""
                    if self._canvas_ui.is_logged_in(wxid):
                        return {
                            "online": True,
                            "wxid": wxid,
                            "NickName": "我",
                            "UserName": wxid,
                            "Alias": "",
                            "reason": "",
                            "uiFallback": True,
                        }
                except Exception:
                    pass
                return {"online": False, "reason": str(ex) or name}
            wxid = info.get("wxid") or wx["Tools"].get_current_wxid() or ""
            nick = info.get("昵称") or info.get("NickName") or "我"
            alias = info.get("微信号") or ""
            return {
                "online": bool(wxid or nick),
                "wxid": wxid or nick,
                "NickName": nick,
                "UserName": wxid or nick,
                "Alias": alias,
                "reason": "",
            }

        try:
            st = self.runner.run(work, timeout=45)
        except Exception as ex:
            st = {"online": False, "reason": str(ex)}
            self._remember_probe(st)
            return st

        with self.lock:
            if st.get("online"):
                self.login_state = "online"
                self.self_info.update(
                    {
                        "wxid": st.get("wxid") or self.self_info["wxid"],
                        "NickName": st.get("NickName") or self.self_info["NickName"],
                        "UserName": st.get("UserName") or st.get("wxid") or self.self_info["UserName"],
                        "Alias": st.get("Alias") or "",
                    }
                )
            else:
                self.login_state = "offline"
        self._remember_probe(st)
        return st

    # -- login-probe cache ----------------------------------------------

    def _recent_probe(self) -> dict[str, Any] | None:
        with self.lock:
            if self._probe_result is None:
                return None
            if time.time() - self.last_probe < PROBE_CACHE_TTL:
                return dict(self._probe_result)
            self._probe_result = None
            return None

    def _remember_probe(self, st: dict[str, Any]) -> None:
        with self.lock:
            self.last_probe = time.time()
            self._probe_result = dict(st)

    def invalidate_probe(self) -> None:
        """Drop the cached login verdict (call after a login-state transition)."""
        with self.lock:
            self.last_probe = 0.0
            self._probe_result = None

    def capture_qr(self) -> dict[str, Any]:
        if self.mock:
            # 1x1 png
            raw = base64.b64decode(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+X2ZkAAAAASUVORK5CYII="
            )
            self.qr_uuid = uuid.uuid4().hex
            self.qr_ready_at = time.time()
            self.login_state = "waiting"
            return ok(
                {
                    "uuid": self.qr_uuid,
                    "Uuid": self.qr_uuid,
                    "qrCodeBase64": base64.b64encode(raw).decode("ascii"),
                    "QrCodeUrl": "",
                    "QrLink": "mock://login",
                },
                "mock qr",
            )

        st = self.status()
        if st.get("online"):
            return fail("电脑微信已登录，无需再扫", ALREADY_ONLINE, {"alreadyOnline": True})

        def work():
            wx = self._import_wx()
            QR_PATH.parent.mkdir(parents=True, exist_ok=True)
            saved = wx["Navigator"].capture_Login_QRCode(str(QR_PATH))
            return bool(saved) and QR_PATH.exists()

        try:
            saved = self.runner.run(work, timeout=40)
        except Exception as ex:
            return fail("截取登录二维码失败：" + str(ex))

        if not saved:
            return fail("未找到微信登录窗口。请先打开电脑微信并停在扫码页。")

        raw = QR_PATH.read_bytes()
        self.qr_uuid = uuid.uuid4().hex
        self.qr_ready_at = time.time()
        self.login_state = "waiting"
        return ok(
            {
                "uuid": self.qr_uuid,
                "Uuid": self.qr_uuid,
                "qrCodeBase64": base64.b64encode(raw).decode("ascii"),
                "QrCodeUrl": "",
                "QrLink": "",
            },
            "请用手机微信扫电脑登录窗上的码",
        )

    def check_qr(self, qid: str) -> dict[str, Any]:
        if not qid or (self.qr_uuid and qid != self.qr_uuid):
            return fail("uuid 无效", -1, {"state": 4})

        if self.mock:
            elapsed = time.time() - self.qr_ready_at
            if elapsed < 2:
                return ok({"state": 0, "Status": 0}, "等待扫码")
            self.login_state = "online"
            data = {
                "state": 2,
                "Status": 2,
                "wxid": self.self_info["wxid"],
                "Wxid": self.self_info["wxid"],
                "nick_name": self.self_info["NickName"],
                "NickName": self.self_info["NickName"],
            }
            return ok(data, "登录成功")

        if time.time() - self.qr_ready_at > 280:
            self.login_state = "expired"
            return ok({"state": 4, "Status": 4}, "二维码已过期")

        # This poll exists precisely to catch the offline -> online transition,
        # so it must never be answered from the cache.
        st = self.status(fresh=True)
        if st.get("online"):
            data = {
                "state": 2,
                "Status": 2,
                "wxid": self.self_info["wxid"],
                "Wxid": self.self_info["wxid"],
                "UserName": self.self_info["UserName"],
                "nick_name": self.self_info["NickName"],
                "NickName": self.self_info["NickName"],
            }
            return ok(data, "登录成功")
        return ok({"state": 0, "Status": 0, "msg": st.get("reason") or "等待扫码"}, "等待扫码")

    def profile_payload(self) -> dict[str, Any]:
        st = self.status()
        if not st.get("online"):
            return fail(st.get("reason") or "未登录")
        return ok(
            {
                "wxid": self.self_info["wxid"],
                "Wxid": self.self_info["wxid"],
                "UserName": self.self_info["UserName"],
                "NickName": self.self_info["NickName"],
                "nickName": self.self_info["NickName"],
                "Alias": self.self_info.get("Alias") or "",
                "loginState": "MMLoginStateNormal",
                "State": "online",
            }
        )

    def refresh_contacts(self) -> list[dict[str, Any]]:
        if self.mock:
            return list(self.contacts.values())

        cached = self._recent_failure("contacts")
        if cached:
            raise WeixinSyncError(cached)

        def work():
            wx = self._import_wx()
            friends = wx["Contacts"].get_friends_info(close_weixin=False, is_maximize=False) or []
            groups: list[str] = []
            try:
                groups = wx["Contacts"].get_groups_info(close_weixin=False, is_maximize=False) or []
            except Exception:
                groups = []
            return friends, groups

        try:
            friends, groups = self.runner.run(work, timeout=180)
            self._clear_failure("contacts")
        except Exception as ex:
            # Never fabricate contacts here.  This used to invent a
            # "文件传输助手" row and return it as a normal success, so the phone
            # showed "通讯录：1 个联系人" and the user believed the sync had
            # finished.  Re-raise instead and let the route report the failure.
            self._note_failure("contacts", str(ex))
            raise WeixinSyncError(str(ex)) from ex

        with self.lock:
            self.groups = {self.display_name(g) for g in groups if g}
            for name in friends:
                if not name:
                    continue
                raw = self.display_name(name)
                key = self.canon_user(raw, raw in self.groups)
                prev = self.contacts.get(key, {})
                self.contacts[key] = self._contact(raw, prev.get("NickName") or raw, prev.get("Remark") or "", raw in self.groups)
            for name in self.groups:
                key = self.canon_user(name, True)
                prev = self.contacts.get(key, {})
                self.contacts[key] = self._contact(name, prev.get("NickName") or name, prev.get("Remark") or "", True)
            return list(self.contacts.values())

    def contact_detail(self, name: str) -> dict[str, Any]:
        if not name:
            return fail("userName 为空")
        key = self.canon_user(name, self._is_group(name))
        shown = self.display_name(name)
        if self.mock:
            c = self.contacts.get(key) or self._contact(shown, shown, group=self._is_group(name))
            return ok(c)

        def work():
            wx = self._import_wx()
            return wx["Contacts"].get_friend_profile(friend=shown, close_weixin=False, is_maximize=False) or {}

        try:
            raw = self.runner.run(work, timeout=60)
        except Exception as ex:
            return fail(str(ex))

        nick = raw.get("昵称") or shown
        remark = raw.get("备注") or ""
        if remark in ("无", "无备注"):
            remark = ""
        sig = raw.get("个性签名") or ""
        if sig == "无":
            sig = ""
        region = raw.get("地区") or ""
        if region == "无":
            region = ""
        parts = [p for p in region.replace("，", " ").split() if p]
        dto = self._contact(shown, nick, remark, self._is_group(name))
        dto["Signature"] = sig
        dto["Province"] = parts[0] if parts else ""
        dto["City"] = parts[1] if len(parts) > 1 else ""
        dto["Alias"] = raw.get("微信号") or ""
        with self.lock:
            self.contacts[dto["UserName"]] = dto
        return ok(dto)

    def group_member_count(self, name: str) -> int:
        shown = self.display_name(name)
        if self.mock:
            return 8 if shown in self.groups else 0

        def work():
            wx = self._import_wx()
            members = wx["Contacts"].get_groupMembers_info(group=shown, close_weixin=False, is_maximize=False) or []
            return len(members)

        try:
            return int(self.runner.run(work, timeout=80) or 0)
        except Exception:
            return 0

    def send_text(self, to: str, content: str, client_msg_id: str = "") -> dict[str, Any]:
        if not to or not content:
            return fail("收件人或内容为空")
        target = self.display_name(to)
        session = self.canon_user(to, self._is_group(to))
        client_msg_id = (client_msg_id or "").strip() or uuid.uuid4().hex
        if self.mock:
            with self.lock:
                self.inbox.append(self._msg(self.self_info["wxid"], session, content, 1, client_msg_id))
            return ok(
                {
                    "ToWxid": session,
                    "Target": target,
                    "ClientMsgId": client_msg_id,
                    "Transport": "mock",
                    "OperationCompleted": True,
                },
                "sent",
            )

        def work():
            wx = self._import_wx()
            wxid = wx["Tools"].get_current_wxid() or ""
            if self._canvas_ui.is_logged_in(wxid):
                result = self._canvas_ui.send_text(target, content)
                return {
                    "transport": "visible-ui",
                    "operation_completed": bool(result and result.get("operationCompleted")),
                    "verified": (result or {}).get("verified") or "",
                }

            wx["Messages"].send_messages_to_friend(
                friend=target,
                messages=[content],
                close_weixin=False,
                is_maximize=False,
            )
            return {"transport": "pyweixin", "operation_completed": True, "verified": "pyweixin-uia"}

        try:
            # Every Weixin UI operation must pass through the same queue.  The
            # previous canvas branch called pyautogui directly from the HTTP
            # worker, so polling/status/contact requests could interleave with
            # a send and move the editor to another conversation.
            operation = self.runner.run(work, timeout=60)
            if not operation or not operation.get("operation_completed"):
                return fail("微信界面操作未完成")
            return ok(
                {
                    "ToWxid": session,
                    "Target": target,
                    "ClientMsgId": client_msg_id,
                    "Transport": operation.get("transport") or "unknown",
                    "OperationCompleted": True,
                    # 说明这次“完成”是靠什么证明的。visible-ui 的
                    # editor-readback 只证明编辑框里确实是这段文字且按了发送键，
                    # 不等于微信服务器已经落库。
                    "Verified": operation.get("verified") or "",
                },
                "sent",
            )
        except Exception as ex:
            return fail("发送失败：" + str(ex))

    def send_file(self, to: str, path: str) -> dict[str, Any]:
        if not to or not path or not os.path.exists(path):
            return fail("文件不存在")
        target = self.display_name(to)
        session = self.canon_user(to, self._is_group(to))

        def work():
            wx = self._import_wx()
            wxid = wx["Tools"].get_current_wxid() or ""
            if self._canvas_ui.is_logged_in(wxid):
                # No visible-UI fallback exists for attachments: the file
                # picker dialog cannot be driven safely by blind coordinates.
                # Fail loudly instead of pretending the file went out.
                raise RuntimeError(
                    "当前微信是 Qt 画布版，pyweixin 读不到控件树，"
                    "而文件发送没有可见 UI 兜底路径。请在电脑微信里手动发送。"
                )
            wx["Files"].send_files_to_friend(friend=target, files=[path], close_weixin=False, is_maximize=False)
            return True

        try:
            self.runner.run(work, timeout=80)
            return ok({"ToWxid": session, "Target": target, "Transport": "pyweixin"}, "sent")
        except Exception as ex:
            return fail("发文件失败：" + str(ex))

    def send_quote(self, to: str, content: str, quote: str, client_msg_id: str = "") -> dict[str, Any]:
        fallback = "「%s」\n- - - - - - - - - -\n%s" % (quote, content)
        if self.mock:
            return self.send_text(to, fallback, client_msg_id)
        if not quote:
            return self.send_text(to, content, client_msg_id)
        target = self.display_name(to)
        session = self.canon_user(to, self._is_group(to))

        def work():
            wx = self._import_wx()
            wxid = wx["Tools"].get_current_wxid() or ""
            if self._canvas_ui.is_logged_in(wxid):
                # Qt canvas: reply_with_quote needs a readable control tree we
                # do not have.  Report "not applicable" so the caller degrades
                # to a single verified text send instead of half-driving the UI
                # here and then sending again.
                return "needs-fallback"
            wx["Messages"].reply_with_quote(
                friend=target,
                quote_text=quote,
                reply_messages=[content],
                close_weixin=False,
                is_maximize=False,
            )
            return "quoted"

        try:
            outcome = self.runner.run(work, timeout=70)
        except Exception as ex:
            # The UI may have been partially driven before failing; do NOT
            # blindly send again. Report the failure and let the phone retry.
            return fail("引用回复失败：" + str(ex))

        if outcome == "quoted":
            return ok(
                {
                    "ToWxid": session,
                    "Target": target,
                    "ClientMsgId": client_msg_id,
                    "Transport": "pyweixin",
                    "OperationCompleted": True,
                },
                "sent",
            )

        # Nothing was typed yet, so a plain verified text send is safe.
        return self.send_text(to, fallback, client_msg_id)

    def set_remark(self, name: str, remark: str) -> dict[str, Any]:
        key = self.canon_user(name, self._is_group(name))
        shown = self.display_name(name)
        if self.mock:
            if key in self.contacts:
                self.contacts[key]["Remark"] = remark
            return ok({"UserName": key, "Remark": remark})

        def work():
            wx = self._import_wx()
            wx["FriendSettings"].change_remark(friend=shown, remark=remark, close_weixin=False, is_maximize=False)
            return True

        try:
            self.runner.run(work, timeout=50)
            return ok({"UserName": key, "Remark": remark})
        except Exception as ex:
            return fail(str(ex))

    def sync_messages(self) -> list[dict[str, Any]]:
        if self.mock:
            with self.lock:
                fresh = [m for m in self.inbox if m["MsgId"] not in self.seen_msg_ids]
                for m in fresh:
                    self.seen_msg_ids.add(m["MsgId"])
                self._trim_history()
                return fresh

        cached = self._recent_failure("sync")
        if cached:
            raise WeixinSyncError(cached)

        def work():
            wx = self._import_wx()
            return wx["Messages"].check_new_messages(close_weixin=False, is_maximize=False) or {}

        try:
            raw = self.runner.run(work, timeout=90)
            self._clear_failure("sync")
        except Exception as ex:
            # "微信窗口被挡住 / RPA 超时" must not look identical to "没有新消息".
            # Returning [] here made the two indistinguishable on the phone.
            self._note_failure("sync", str(ex))
            raise WeixinSyncError(str(ex)) from ex

        out: list[dict[str, Any]] = []
        me = self.self_info["wxid"]
        if not isinstance(raw, dict):
            return out

        for friend, items in raw.items():
            if not items:
                continue
            rows = items if isinstance(items, list) else [items]
            session = self.canon_user(friend, self._is_group(friend))

            # Count occurrences of (sender, content, stamp) within this batch
            # so the same message appearing at different batch indices due to
            # list scrolling keeps its stable id.
            occurrence_count: dict[tuple[str, str, str], int] = {}

            for row in rows:
                if not isinstance(row, dict):
                    continue
                sender = row.get("消息发送人") or self.display_name(friend)
                content = row.get("消息内容") or ""
                hinted = str(row.get("消息类型") or "")
                stamp = str(row.get("消息时间") or row.get("时间") or "")

                # Stable tuple for this message
                key = (sender, content, stamp)
                occurrence_index = occurrence_count.get(key, 0)
                occurrence_count[key] = occurrence_index + 1

                mid = self._stable_msg_id(session, sender, content, stamp, occurrence_index)
                mine = sender in ("我", self.self_info.get("NickName"), me)
                if self._is_group(friend):
                    frm = session if not mine else me
                    to = me if not mine else session
                    body = ("%s:\n%s" % (sender, content)) if not mine else content
                else:
                    frm = me if mine else session
                    to = session if mine else me
                    body = content
                out.append(self._msg(frm, to, body, self._guess_msg_type(content, hinted), mid))

        fresh: list[dict[str, Any]] = []
        with self.lock:
            for m in out:
                if m["MsgId"] in self.seen_msg_ids:
                    continue
                self.seen_msg_ids.add(m["MsgId"])
                self.inbox.append(m)
                fresh.append(m)
            self._trim_history()
        return fresh

    @staticmethod
    def _stable_msg_id(session: str, sender: str, content: str, stamp: str, index: int) -> str:
        """Content-addressed id so repeated polls collapse to one message.

        pyweixin's check_new_messages does not expose the server msgId, so the
        best available key is the tuple that identifies the message inside its
        conversation.  ``index`` disambiguates a genuine duplicate send of the
        same text inside one batch.
        """
        raw = "\u0001".join([session or "", sender or "", content or "", stamp or "", str(index)])
        return hashlib.sha1(raw.encode("utf-8", "replace")).hexdigest()

    def _trim_history(self) -> None:
        """Caller must hold self.lock."""
        if len(self.inbox) > INBOX_CAP:
            del self.inbox[: len(self.inbox) - INBOX_CAP]
        if len(self.seen_msg_ids) > INBOX_CAP * 4:
            # Keep the ids still represented in the inbox; drop the rest.
            self.seen_msg_ids = {m["MsgId"] for m in self.inbox}

    def moments_list(self) -> list[dict[str, Any]]:
        if self.mock:
            return [
                {
                    "Id": "m_mock_1",
                    "Content": "pyweixin 网关 mock 朋友圈",
                    "CreateTime": now_unix() - 3600,
                    "NickName": "苏晚",
                    "UserName": "苏晚",
                    "LikeList": ["阿KEN"],
                    "CommentList": [],
                    "MediaList": [],
                }
            ]

        cached = self._recent_failure("moments")
        if cached:
            raise WeixinSyncError(cached)

        def work():
            wx = self._import_wx()
            return wx["Moments"].dump_recent_posts(recent="Today", number=10, close_weixin=False, is_maximize=False) or []

        try:
            posts = self.runner.run(work, timeout=120)
            self._clear_failure("moments")
        except Exception as ex:
            # Same rule as contacts/sync: a failed read is not an empty timeline.
            self._note_failure("moments", str(ex))
            raise WeixinSyncError(str(ex)) from ex
        out = []
        for i, p in enumerate(posts or []):
            if not isinstance(p, dict):
                continue

            # Parse pyweixin's 发布时间 (e.g. "3分钟前", "2小时前", "昨天", "5天前")
            # into a Unix timestamp. pyweixin gives relative time strings, not absolutes.
            post_time_str = p.get("发布时间") or ""
            create_time = self._parse_moment_time(post_time_str)

            out.append(
                {
                    "Id": p.get("Id") or f"m_{i}_{p.get('发布时间') or now_unix()}",
                    "Content": p.get("内容") or "",
                    "CreateTime": create_time,
                    "NickName": p.get("好友") or "好友",
                    "UserName": p.get("好友") or "好友",
                    "LikeList": [],
                    "CommentList": [],
                    "MediaList": [],
                }
            )
        return out

    @staticmethod
    def _parse_moment_time(time_str: str) -> int:
        """Convert pyweixin's relative time string to Unix timestamp.

        Supports Chinese formats: "3分钟前", "2小时前", "昨天", "5天前"
        Falls back to now if parsing fails.
        """
        now = now_unix()
        text = (time_str or "").strip()

        # "3分钟前" / "3 minute(s) ago"
        match = re.search(r'(\d+)\s*分钟前', text)
        if not match:
            match = re.search(r'(\d+)\s*minute', text, re.IGNORECASE)
        if match:
            minutes = int(match.group(1))
            return now - minutes * 60

        # "2小时前" / "2 hour(s) ago"
        match = re.search(r'(\d+)\s*小时前', text)
        if not match:
            match = re.search(r'(\d+)\s*hour', text, re.IGNORECASE)
        if match:
            hours = int(match.group(1))
            return now - hours * 3600

        # "昨天" / "Yesterday"
        if "昨天" in text or "yesterday" in text.lower():
            return now - 86400

        # "5天前" / "5 day(s) ago"
        match = re.search(r'(\d+)\s*天前', text)
        if not match:
            match = re.search(r'(\d+)\s*day', text, re.IGNORECASE)
        if match:
            days = int(match.group(1))
            return now - days * 86400

        # Fallback: unrecognized format, return now
        return now

    def publish_moment(self, text: str) -> dict[str, Any]:
        if self.mock:
            return ok({"Content": text}, "published")

        def work():
            wx = self._import_wx()
            wx["Moments"].post_moments(text=text, medias=[], close_weixin=False, is_maximize=False)
            return True

        try:
            self.runner.run(work, timeout=80)
            return ok({"Content": text}, "published")
        except Exception as ex:
            return fail(str(ex))

    def save_temp_file(self, raw_b64: str, name: str) -> str:
        DATA_DIR.mkdir(parents=True, exist_ok=True)
        blob = raw_b64 or ""
        if blob.startswith("data:") and "," in blob:
            blob = blob.split(",", 1)[1]
        data = base64.b64decode(blob)
        # Callers currently pass a literal ("image.jpg"/"file.bin"), but treat the
        # name as untrusted anyway: a path-bearing name would otherwise let a
        # write escape DATA_DIR entirely (..\..\Startup\x.bat).
        safe_name = os.path.basename(str(name or "").replace("\\", "/")).strip() or "file.bin"
        safe_name = re.sub(r'[^A-Za-z0-9._一-鿿-]', "_", safe_name)[:80].lstrip(".") or "file.bin"
        path = (DATA_DIR / f"{int(time.time())}_{safe_name}").resolve()
        if not str(path).startswith(str(DATA_DIR.resolve())):
            raise ValueError("文件名不合法")
        path.write_bytes(data)
        return str(path)


class App:
    def __init__(self, mock: bool) -> None:
        self.cfg = load_config()
        self.backend = WeixinBackend(mock=mock)

    def authorized(self, token: str | None) -> bool:
        expected = self.cfg.get("token") or ""
        return bool(token) and bool(expected) and secrets.compare_digest(str(token), str(expected))

    def is_admin(self, key: str | None) -> bool:
        expected = self.cfg.get("admin_key") or ""
        return bool(key) and bool(expected) and secrets.compare_digest(str(key), str(expected))

    def gen_token(self) -> dict[str, Any]:
        token = secrets.token_hex(16)
        self.cfg["token"] = token
        save_config(self.cfg)
        return ok([token], "generated")


APP: App


#: Largest request body accepted.  The body is read *before* the token check, so
#: an unauthenticated client could otherwise declare Content-Length: 8GB and make
#: the gateway allocate it.  Base64 image uploads are the biggest legitimate body.
MAX_BODY_BYTES = 32 * 1024 * 1024


def read_json_body(handler: BaseHTTPRequestHandler) -> dict[str, Any]:
    try:
        length = int(handler.headers.get("Content-Length") or 0)
    except (TypeError, ValueError):
        # Malformed header: this used to raise outside the caller's try block
        # and kill the connection with no response at all.
        return {}
    if length <= 0:
        return {}
    if length > MAX_BODY_BYTES:
        return {}
    raw = handler.rfile.read(length)
    if not raw:
        return {}
    try:
        data = json.loads(raw.decode("utf-8"))
        return data if isinstance(data, dict) else {}
    except Exception:
        return {}


def extract_token(handler: BaseHTTPRequestHandler, query: dict[str, list[str]]) -> str:
    for name in ("X-Access-Token", "X-Admin-Token"):
        val = handler.headers.get(name)
        if val:
            return val.strip()
    keys = query.get("key") or []
    return (keys[0] if keys else "").strip()


def pick(body: dict[str, Any], *names: str, default: Any = "") -> Any:
    for n in names:
        if n in body and body[n] not in (None, ""):
            return body[n]
    return default


#: ?key=<token> / &key=<token> anywhere in a logged request line.
_SECRET_QUERY_RE = re.compile(r"([?&](?:key|token|access_token|adminKey)=)[^&\s]+", re.IGNORECASE)


def show_secret(value: str, reveal: bool) -> str:
    """Mask a credential for console output unless explicitly asked to reveal.

    Operators routinely run the gateway with stdout redirected to a file; the
    banner used to write the token and admin key straight into it.
    """
    value = value or ""
    if reveal:
        return value
    if len(value) <= 8:
        return "*" * len(value)
    return value[:4] + "…" + value[-4:] + "  (已掩码)"


def scrub_secrets(text: str) -> str:
    """Strip credentials out of anything headed for stdout.

    BaseHTTPRequestHandler logs the raw request line, which includes the query
    string.  Clients that pass ?key=TOKEN would otherwise print the token to
    the console on every single request.
    """
    return _SECRET_QUERY_RE.sub(r"\1***", text or "")


class Handler(BaseHTTPRequestHandler):
    server_version = "MoonWeChat-PyWeixin/1.0"

    def log_message(self, fmt: str, *args: Any) -> None:
        print("[%s] %s" % (self.log_date_time_string(), scrub_secrets(fmt % args)))

    def _send(self, payload: dict[str, Any], http_status: int = 200) -> None:
        raw = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(http_status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        # No CORS headers: the phone client is not a browser and never sends a
        # preflight.  `Access-Control-Allow-Origin: *` used to let any website
        # the user visited read this LAN service's responses cross-origin, which
        # turns a leaked token into a directly usable attack surface.
        self.end_headers()
        self.wfile.write(raw)

    def do_OPTIONS(self) -> None:  # noqa: N802
        # Answer the method without advertising cross-origin access.
        self.send_response(204)
        self.send_header("Allow", "GET,POST,OPTIONS")
        self.send_header("Content-Length", "0")
        self.end_headers()

    def do_GET(self) -> None:  # noqa: N802
        self._dispatch()

    def do_POST(self) -> None:  # noqa: N802
        self._dispatch()

    def _host_allowed(self) -> bool:
        """Reject requests whose Host is a name rather than this server's address.

        The phone client always dials an IP literal (http://192.168.x.y:18765).
        A browser lured to attacker.example that resolves to this LAN address
        would arrive with Host: attacker.example — DNS rebinding.  Requiring an
        IP/localhost Host closes that without affecting the real client.
        """
        raw = (self.headers.get("Host") or "").strip()
        if not raw:
            # HTTP/1.0 clients may omit Host; nothing to rebind through.
            return True

        # Strip the port.  Bracketed IPv6 ("[::1]:18765") must be handled before
        # any colon counting, or the address itself gets mistaken for a port.
        if raw.startswith("["):
            host = raw[1:].split("]", 1)[0]
        elif raw.count(":") == 1:
            host = raw.rsplit(":", 1)[0]
        else:
            host = raw  # bare IPv6 without brackets, or no port

        host = host.strip().lower()
        if host == "localhost":
            return True
        try:
            ipaddress.ip_address(host)
            return True
        except ValueError:
            return False

    def _dispatch(self) -> None:
        if not self._host_allowed():
            self._send(fail("Host 头不是本机地址，已拒绝（防 DNS rebinding）", -2), 403)
            return

        parsed = urlparse(self.path)
        path = parsed.path or "/"
        query = parse_qs(parsed.query or "")
        body = read_json_body(self) if self.command == "POST" else {}
        token = extract_token(self, query)

        try:
            payload = self._route(path, query, body, token)
        except Exception as ex:
            traceback.print_exc()
            payload = fail("网关异常：" + str(ex), -1)
        self._send(payload, 200 if payload.get("Code") != -1 else 500)

    def _need_token(self, token: str) -> dict[str, Any] | None:
        if APP.authorized(token):
            return None
        return fail("token 不存在", -2)

    def _route(self, path: str, query: dict[str, list[str]], body: dict[str, Any], token: str) -> dict[str, Any]:
        if path in ("/", "/index.html", "/swagger", "/swagger/index.html"):
            return ok(
                {
                    "name": "MoonWeChat pyweixin gateway",
                    "backend": "pyweixin",
                    "mock": APP.backend.mock,
                },
                "pyweixin gateway",
            )

        if path.lower() in ("/admin/genauthkey", "/admin/genauthkey1"):
            admin = (query.get("key") or [token])[0]
            if not APP.is_admin(admin):
                return fail("adminKey 无效", -2)
            return APP.gen_token()

        denied = self._need_token(token)
        if denied:
            return denied

        key = path.rstrip("/").lower()

        if key in ("/login/getqrwinuwp", "/login/getqrmac", "/login/getqrpad", "/login/getqrpadx",
                   "/login/getqrwin", "/login/getqrwinunified", "/login/getqrx", "/login/getqr"):
            return APP.backend.capture_qr()

        if key in ("/login/checkqr", "/login/checkmacqr"):
            qid = (query.get("uuid") or [pick(body, "uuid", "Uuid")])[0]
            return APP.backend.check_qr(qid)

        if key in ("/login/newinit", "/login/twiceautoauth", "/login/awaken", "/login/heartbeat",
                   "/login/heartbeatlong", "/login/autoheartbeat"):
            st = APP.backend.status()
            if not st.get("online"):
                return fail(st.get("reason") or "未登录")
            return ok(APP.backend.self_info, "ok")

        if key in ("/login/getcacheinfo", "/login/getloginstatus", "/api/login/getloginstatus",
                   "/user/getcontractprofile", "/user/getonlineinfo", "/user/getallonline"):
            return APP.backend.profile_payload()

        # 客户端每个逻辑调用都会顺序试一串候选路径，并把**最后一次**的 Message 交给用户
        # （见 GetSyncMsgAsync / PostFirstOkAsync）。所以这些别名必须在这里落地：
        # 否则真正的诊断（「窗口被挡住」）会被列表末尾那个 404「未实现的路径」覆盖掉。
        if key in ("/friend/getcontractlist", "/friend/getcontactlist", "/friend/getfriendlist"):
            try:
                contacts = APP.backend.refresh_contacts()
            except WeixinSyncError as ex:
                # 如实上报：手机会显示同步失败，而不是「通讯录：1 个联系人」。
                return fail("通讯录同步失败：" + str(ex), UI_UNAVAILABLE, {"uiError": str(ex)})
            return ok({"ContactList": contacts, "FriendList": contacts})

        if key in ("/group/grouplist", "/friend/grouplist", "/group/getallgrouplist"):
            try:
                refreshed = APP.backend.refresh_contacts()
            except WeixinSyncError as ex:
                return fail("群列表同步失败：" + str(ex), UI_UNAVAILABLE, {"uiError": str(ex)})
            contacts = [c for c in refreshed if c.get("IsChatroom")]
            return ok({"ContactList": contacts, "List": contacts})

        if key in ("/group/getchatroommemberdetail", "/group/getchatroominfodetail", "/group/getchatroominfo"):
            name = pick(body, "ChatRoomName", "chatRoomName", "UserName")
            n = APP.backend.group_member_count(name)
            return ok({"MemberCount": n, "ChatRoomMember": [{"UserName": f"m{i}"} for i in range(n)]})

        if key in ("/friend/getcontractdetail", "/friend/getcontactdetailslist"):
            name = pick(body, "UserName", "userName", "Wxid", "ToUserName")
            return APP.backend.contact_detail(name)

        if key in ("/friend/search", "/friend/searchcontact"):
            kw = str(pick(body, "UserName", "userName")).strip()
            with APP.backend.lock:
                snapshot = list(APP.backend.contacts.values())
            hits = [c for c in snapshot if kw and kw in (c.get("UserName") or "") + (c.get("NickName") or "")]
            return ok({"ContactList": hits, "FriendList": hits})

        if key in ("/friend/setremarks", "/user/modifyremark"):
            return APP.backend.set_remark(pick(body, "UserName", "userName"), pick(body, "Remark", "remark"))

        if key == "/msg/sendtxt":
            return APP.backend.send_text(
                pick(body, "ToWxid", "ToUserName", "to_wxid"),
                pick(body, "Content", "content"),
                pick(body, "ClientMsgId", "clientMsgId", "MsgId", "msgId"),
            )

        if key == "/msg/sharelocation":
            name = pick(body, "Poiname", "Label") or "位置"
            addr = pick(body, "Label") or ""
            to = pick(body, "ToWxid", "ToUserName")
            return APP.backend.send_text(to, f"[位置] {name} {addr}".strip())

        if key == "/msg/sharecard":
            to = pick(body, "ToWxid", "ToUserName")
            card = pick(body, "CardNickName", "CardWxId") or "名片"
            return APP.backend.send_text(to, f"[名片] {card}")

        if key == "/msg/sharelink":
            to = pick(body, "ToWxid", "ToUserName")
            title = pick(body, "Xml") or "链接"
            return APP.backend.send_text(to, str(title)[:200])

        if key == "/msg/uploadimg":
            to = pick(body, "ToWxid", "ToUserName")
            b64 = pick(body, "Base64", "base64")
            if APP.backend.mock:
                return ok({"FileId": "mock", "AESKey": "mock"})
            try:
                path = APP.backend.save_temp_file(b64, "image.jpg")
                sent = APP.backend.send_file(to, path)
                sent["Data"] = {"FileId": Path(path).name, "Content": path}
                return sent
            except Exception as ex:
                return fail(str(ex))

        if key == "/msg/sendcdnimg":
            to = pick(body, "ToWxid", "ToUserName")
            content = pick(body, "Content")
            # Only allow paths inside DATA_DIR to prevent arbitrary file reads.
            # The client should use /Msg/UploadImg for real images; this path
            # exists for protocol compatibility with backends that store server-side.
            # BOTH sides must be resolved: comparing a resolved candidate against
            # an unresolved DATA_DIR rejects legitimate files whenever the base
            # path contains a symlink or an 8.3 short name (common under %TEMP%).
            if content:
                try:
                    candidate = Path(str(content)).resolve()
                    if candidate.is_relative_to(DATA_DIR.resolve()) and candidate.is_file():
                        return APP.backend.send_file(to, str(candidate))
                except (ValueError, OSError):
                    pass
            if APP.backend.mock:
                return ok({"ToWxid": to, "Transport": "mock"}, "sent")
            # 这里以前无条件 return ok(...,"sent")：什么都没做却报成功。
            # 图片的真实发送发生在 /Msg/UploadImg，这一步只是协议对齐。
            return fail("SendCDNImg 需要一个 data/ 目录内的文件路径；图片请走 /Msg/UploadImg。")

        if key == "/msg/sendcdnfile":
            to = pick(body, "ToWxid", "ToUserName")
            raw = pick(body, "Content")
            if APP.backend.mock:
                return ok({"ToWxid": to}, "sent")
            try:
                path = APP.backend.save_temp_file(str(raw), "file.bin")
                return APP.backend.send_file(to, path)
            except Exception as ex:
                return fail(str(ex))

        if key == "/msg/sendemoji":
            return APP.backend.send_text(pick(body, "ToWxid"), pick(body, "Md5") or "[表情]")

        if key == "/msg/quote":
            return APP.backend.send_quote(
                pick(body, "to_wxid", "ToWxid", "ToUserName"),
                pick(body, "content", "Content"),
                pick(body, "quote_content", "QuoteContent", "quoteContent"),
                pick(body, "ClientMsgId", "clientMsgId"),
            )

        if key == "/msg/revoke":
            return fail("pyweixin 暂不支持撤回")

        if key in ("/group/sendpat", "/msg/sendapp"):
            return fail("pyweixin 暂不支持拍一拍")

        if key == "/group/setchatroomannouncement":
            return fail("pyweixin 暂不支持改群公告")

        if key in ("/msg/sync", "/msg/startautosync", "/message/httpsyncmsg"):
            try:
                msgs = APP.backend.sync_messages()
            except WeixinSyncError as ex:
                # 轮询失败必须和「没有新消息」区分开，否则手机会一直安静地
                # 以为一切正常。客户端 Tick 只在 sync.Ok 时才处理消息。
                return fail("拉取新消息失败：" + str(ex), UI_UNAVAILABLE, {"uiError": str(ex)})
            return ok({"AddMsgs": msgs, "List": msgs})

        # /sns/* 是客户端 GetMomentsListAsync 候选列表的尾部（含 SendSnsTimeLine——
        # 它在客户端那里是用 {fristpagemd5, maxid} 拉列表，不是发布）。同样为了不让
        # 末尾的 404 覆盖真实诊断，这里一并落地；读路径有 READ_FAIL_COOLDOWN 兜底。
        if key in ("/friendcircle/getlist", "/friendcircle/mmsnssync",
                   "/sns/getsnssync", "/sns/sendsnstimeline", "/sns/sendfriendcircle"):
            try:
                return ok({"ObjectList": APP.backend.moments_list()})
            except WeixinSyncError as ex:
                return fail("朋友圈读取失败：" + str(ex), UI_UNAVAILABLE, {"uiError": str(ex)})

        if key == "/friendcircle/messages":
            text = pick(body, "Content", "content", "Text")
            if text:
                return APP.backend.publish_moment(str(text))
            try:
                return ok({"ObjectList": APP.backend.moments_list()})
            except WeixinSyncError as ex:
                return fail("朋友圈读取失败：" + str(ex), UI_UNAVAILABLE, {"uiError": str(ex)})

        if key in ("/friendcircle/operation", "/friendcircle/comment", "/friendcircle/pushcommnet",
                   "/friendcircle/getdetail", "/friendcircle/getiddetail", "/friendcircle/getcommnet",
                   "/friendcircle/upload"):
            return fail("朋友圈互动请在电脑微信里操作；列表/发文字已接 pyweixin")

        return fail("未实现的路径：" + path, 404)


def main() -> None:
    parser = argparse.ArgumentParser(description="MoonWeChat pyweixin HTTP 网关")
    parser.add_argument("--host", default=None)
    parser.add_argument("--port", type=int, default=None)
    parser.add_argument("--mock", action="store_true", help="不调用本机微信，返回演示数据")
    parser.add_argument(
        "--show-secrets",
        action="store_true",
        help="在启动横幅里打印完整 token / adminKey（默认只打掩码，避免重定向到日志文件时泄漏）",
    )
    args = parser.parse_args()

    global APP
    APP = App(mock=args.mock)
    host = args.host or APP.cfg.get("host") or "0.0.0.0"
    port = int(args.port or APP.cfg.get("port") or DEFAULT_PORT)
    APP.cfg["host"] = host
    APP.cfg["port"] = port
    save_config(APP.cfg)

    print("MoonWeChat pyweixin gateway")
    print("  listen : http://%s:%s" % (host, port))
    print("  token  : %s" % show_secret(APP.cfg["token"], args.show_secrets))
    print("  admin  : %s" % show_secret(APP.cfg["admin_key"], args.show_secrets))
    print("  mock   : %s" % APP.backend.mock)
    if not args.show_secrets:
        print("  （完整凭证见 %s，或加 --show-secrets 启动）" % CONFIG_PATH)
    print("手机填本机局域网 IP，例如 http://192.168.1.10:%s" % port)
    httpd = ThreadingHTTPServer((host, port), Handler)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nbye")


if __name__ == "__main__":
    main()
