"""Keyboard/mouse fallback for Weixin builds whose UIA tree is empty.

Weixin 4.1.12 can expose the login window through UIA but render the logged-in
main window as a Qt canvas.  This module deliberately stays at the visible UI
layer: it finds the foreground Weixin window, uses its search box, and pastes
text into the chat editor.  It does not inspect process memory or network
traffic.

Safety model
------------
Driving a chat client by clicking fixed window-relative coordinates is
inherently risky: if a click lands somewhere unexpected, the following paste
and send shortcut could deliver the message to the wrong conversation.  Two
mitigations are enforced here:

1. ``ALLOWED_TARGETS`` — only conversations on this allowlist may be driven.
2. ``_verify_editor_contains`` — after pasting, the editor content is read back
   through the clipboard and compared with what we intended to send.  The send
   shortcut is only pressed when the readback matches exactly.  This is what
   makes ``operationCompleted`` mean something rather than "no exception was
   raised".

Neither of these proves the message reached Tencent's servers.  They only prove
a specific visible-UI sequence completed against an editor that verifiably held
our exact text.
"""

from __future__ import annotations

import ctypes
import time
from typing import Any

import psutil
import pyautogui
import win32clipboard
import win32con
import win32gui
import win32process


class WeixinUiError(RuntimeError):
    """A visible UI operation could not be completed safely."""


class WeixinCanvasUi:
    """Minimal UI driver for the logged-in Weixin canvas window."""

    WINDOW_CLASS_PREFIX = "Qt"
    WINDOW_CLASS_SUFFIX = "QWindowIcon"
    WINDOW_TITLES = {"微信", "Weixin"}

    #: Conversations this driver is allowed to open and type into.
    #: Blind coordinate clicking must never be pointed at a real person.
    ALLOWED_TARGETS = {"文件传输助手"}

    def __init__(self, allowed_targets: set[str] | None = None) -> None:
        if allowed_targets:
            self.ALLOWED_TARGETS = set(allowed_targets)

    # -- window discovery ------------------------------------------------

    def _is_weixin_class(self, name: str) -> bool:
        # Weixin ships a Qt build whose window class embeds the Qt version
        # (e.g. Qt51514QWindowIcon for Qt 5.15.14).  Matching the exact string
        # breaks on every Weixin update, so match the stable prefix/suffix.
        return name.startswith(self.WINDOW_CLASS_PREFIX) and name.endswith(self.WINDOW_CLASS_SUFFIX)

    def _find_window(self) -> int:
        matches: list[tuple[int, str, bool]] = []

        def visit(hwnd: int, _extra: Any) -> None:
            if not win32gui.IsWindowVisible(hwnd):
                return
            if not self._is_weixin_class(win32gui.GetClassName(hwnd)):
                return
            title = win32gui.GetWindowText(hwnd)
            if title not in self.WINDOW_TITLES:
                return
            _thread_id, pid = win32process.GetWindowThreadProcessId(hwnd)
            try:
                if psutil.Process(pid).name().lower() != "weixin.exe":
                    return
            except (psutil.Error, OSError):
                return
            matches.append((hwnd, title, not win32gui.IsIconic(hwnd)))

        win32gui.EnumWindows(visit, None)
        if not matches:
            return 0
        # Prefer the visible Chinese main window and then a non-minimized one.
        matches.sort(key=lambda item: (item[1] == "微信", item[2]), reverse=True)
        return matches[0][0]

    @staticmethod
    def _ui_class(hwnd: int) -> str | None:
        """Return the UIA class name, or None when it cannot be determined."""
        try:
            from pywinauto import Desktop

            return Desktop(backend="uia").window(handle=hwnd).class_name()
        except Exception:
            return None

    def is_logged_in(self, wxid: str = "") -> bool:
        hwnd = self._find_window()
        if not hwnd or not wxid:
            return False
        cls = self._ui_class(hwnd)
        if cls is None:
            # Previously this returned True on any pywinauto failure, because
            # "" != "mmui::LoginWindow".  That turned an inspection error into a
            # false "logged in", after which send_text would blind-click.
            return False
        return cls != "mmui::LoginWindow"

    # -- primitives ------------------------------------------------------

    def _activate(self) -> int:
        hwnd = self._find_window()
        if not hwnd:
            raise WeixinUiError("未找到可见的微信主窗口")
        win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
        try:
            win32gui.SetForegroundWindow(hwnd)
        except Exception:
            pass
        if win32gui.GetForegroundWindow() != hwnd:
            # Windows may reject SetForegroundWindow from a worker thread.
            # Temporarily attach input queues, then detach immediately.
            current_thread = ctypes.windll.user32.GetCurrentThreadId()
            target_thread, _pid = win32process.GetWindowThreadProcessId(hwnd)
            attached = bool(ctypes.windll.user32.AttachThreadInput(current_thread, target_thread, True))
            try:
                win32gui.SetForegroundWindow(hwnd)
            finally:
                if attached:
                    ctypes.windll.user32.AttachThreadInput(current_thread, target_thread, False)
        if win32gui.GetForegroundWindow() != hwnd:
            raise WeixinUiError("无法将微信主窗口置于前台")
        return hwnd

    @staticmethod
    def _read_clipboard_text() -> str:
        last_error: Exception | None = None
        for _ in range(5):
            try:
                win32clipboard.OpenClipboard()
                try:
                    if not win32clipboard.IsClipboardFormatAvailable(win32con.CF_UNICODETEXT):
                        return ""
                    return win32clipboard.GetClipboardData(win32con.CF_UNICODETEXT) or ""
                finally:
                    win32clipboard.CloseClipboard()
            except Exception as ex:
                last_error = ex
                time.sleep(0.1)
        raise WeixinUiError("无法读取系统剪贴板") from last_error

    @staticmethod
    def _write_clipboard_text(text: str) -> None:
        last_error: Exception | None = None
        for _ in range(5):
            try:
                win32clipboard.OpenClipboard()
                try:
                    win32clipboard.EmptyClipboard()
                    win32clipboard.SetClipboardText(text, win32con.CF_UNICODETEXT)
                finally:
                    win32clipboard.CloseClipboard()
                return
            except Exception as ex:
                last_error = ex
                time.sleep(0.1)
        raise WeixinUiError("无法写入系统剪贴板") from last_error

    @staticmethod
    def _click(hwnd: int, x_ratio: float, y_ratio: float) -> None:
        left, top, right, bottom = win32gui.GetWindowRect(hwnd)
        x = left + round((right - left) * x_ratio)
        y = top + round((bottom - top) * y_ratio)
        pyautogui.click(x, y)

    def _require_still_foreground(self, hwnd: int, step: str) -> None:
        if win32gui.GetForegroundWindow() != hwnd:
            raise WeixinUiError("微信主窗口在「%s」后失去前台焦点，已中止发送" % step)

    # -- verification ----------------------------------------------------

    def _verify_editor_contains(self, hwnd: int, expected: str) -> None:
        """Select-all + copy the focused editor and compare with ``expected``.

        This is the check that makes the send safe.  If the earlier click
        missed the editor, or landed in a different conversation, or the paste
        silently failed, the readback will not match and we abort *before*
        pressing the send shortcut.
        """
        # Poison the clipboard first: if Ctrl+C copies nothing (no editable
        # focus), we must not read back our own paste payload and call it a
        # match.
        sentinel = "\x00moonwechat-verify\x00"
        self._write_clipboard_text(sentinel)

        pyautogui.hotkey("ctrl", "a")
        time.sleep(0.15)
        pyautogui.hotkey("ctrl", "c")
        time.sleep(0.35)

        self._require_still_foreground(hwnd, "读回编辑框内容")
        actual = self._read_clipboard_text()

        if actual == sentinel:
            raise WeixinUiError("无法读回编辑框内容（可能没有点中输入框），已中止发送")

        if actual.strip() != expected.strip():
            raise WeixinUiError(
                "编辑框内容与待发内容不一致，已中止发送（读回 %d 字符，期望 %d 字符）"
                % (len(actual.strip()), len(expected.strip()))
            )

    # -- operations ------------------------------------------------------

    def _normalize_target(self, friend: str) -> str:
        friend = (friend or "").strip()
        if friend.lower() == "filehelper":
            friend = "文件传输助手"
        if not friend:
            raise WeixinUiError("联系人为空")
        if friend not in self.ALLOWED_TARGETS:
            raise WeixinUiError(
                "目标 「%s」 不在可见 UI 自动化白名单内。当前微信为 Qt 画布，"
                "只能靠固定坐标点击，误点会把消息发给别人，因此仅允许：%s"
                % (friend, "、".join(sorted(self.ALLOWED_TARGETS)))
            )
        return friend

    def _search(self, friend: str) -> int:
        hwnd = self._activate()
        # The search box is on the upper-left of the current Weixin layout.
        self._click(hwnd, 0.16, 0.069)
        self._require_still_foreground(hwnd, "点击搜索框")
        pyautogui.hotkey("ctrl", "a")
        self._write_clipboard_text(friend)
        pyautogui.hotkey("ctrl", "v")
        time.sleep(0.8)

        # Verify focus landed in the search box by reading back its content.
        # If the click missed, Ctrl+A + Ctrl+V would have typed into the
        # active chat editor instead, leaving a draft there.
        sentinel = "\x00search-verify\x00"
        self._write_clipboard_text(sentinel)
        pyautogui.hotkey("ctrl", "a")
        time.sleep(0.1)
        pyautogui.hotkey("ctrl", "c")
        time.sleep(0.2)
        self._require_still_foreground(hwnd, "读回搜索框内容")
        actual = self._read_clipboard_text()

        if actual == sentinel:
            raise WeixinUiError("无法读回搜索框内容（可能没有点中搜索框），已中止")

        if actual.strip() != friend.strip():
            raise WeixinUiError(
                "搜索框内容与目标联系人不一致（读回 「%s」，期望 「%s」），已中止"
                % (actual.strip()[:20], friend.strip()[:20])
            )

        return hwnd

    def open_chat(self, friend: str) -> int:
        friend = self._normalize_target(friend)
        hwnd = self._search(friend)
        if friend == "文件传输助手":
            # This account is shown under the special “功能” section rather
            # than “联系人”.  Its row is the last item in the search popup.
            self._click(hwnd, 0.17, 0.46)
        else:
            # Normal contacts: an exact match appears in the first contact row.
            # Non-whitelist targets were already rejected by _normalize_target,
            # so this branch is never reached unless ALLOWED_TARGETS is modified.
            pyautogui.press("enter")
        time.sleep(1.0)
        if self._find_window() != hwnd:
            raise WeixinUiError("微信主窗口在搜索后不可用")
        self._require_still_foreground(hwnd, "打开会话")
        return hwnd

    def send_text(self, friend: str, content: str) -> dict[str, Any]:
        friend = self._normalize_target(friend)
        if not content or not content.strip():
            raise WeixinUiError("消息内容为空")

        saved_clipboard_text = None
        try:
            saved_clipboard_text = self._read_clipboard_text()
        except WeixinUiError:
            pass

        try:
            hwnd = self.open_chat(friend)
            # The editor occupies the lower-right portion of a chat window.
            self._click(hwnd, 0.72, 0.875)
            self._require_still_foreground(hwnd, "点击编辑框")
            # Replace whatever draft may already be in the editor so the
            # readback below compares against our text alone.
            pyautogui.hotkey("ctrl", "a")
            self._write_clipboard_text(content)
            pyautogui.hotkey("ctrl", "v")
            time.sleep(0.35)

            # Hard gate: prove the focused editor holds exactly our text.
            self._verify_editor_contains(hwnd, content)

            # Put the caret back at the end (Ctrl+A above left everything
            # selected) so the send shortcut acts on the full message.
            pyautogui.press("end")
            time.sleep(0.1)

            # Alt+S is Weixin's send shortcut and avoids depending on the
            # button being exposed by the current Qt renderer.
            self._require_still_foreground(hwnd, "发送前最后检查")
            pyautogui.hotkey("alt", "s")
            time.sleep(0.8)

            return {
                "target": friend,
                # Meaning: the editor verifiably contained exactly this text,
                # the Weixin window held foreground throughout, and the send
                # shortcut was pressed.  NOT a server-side delivery receipt.
                "operationCompleted": True,
                "verified": "editor-readback",
            }
        finally:
            # Only restore text clipboard. If it was CF_BITMAP/CF_HDROP, leave
            # our payload rather than destroying the user's non-text content.
            if saved_clipboard_text is not None:
                try:
                    self._write_clipboard_text(saved_clipboard_text)
                except Exception:
                    pass
