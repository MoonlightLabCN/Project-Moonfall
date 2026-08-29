# WeChatPadProMAX v8 数据模型参考

> 由 `swagger/swagger.json`（构建 v8_m4.1.12.29_p8.0.75.53 (build 20260809_030557)）生成，生成日期 2026-08-16。共 190 个模型。
> 字段类型中 `` `ModelName` `` 表示引用同文件中的另一个模型。

## 目录

- [`Admin.DelayAuthKeyModel`](#admin-delayauthkeymodel)
- [`Admin.DeleteAuthKeyModel`](#admin-deleteauthkeymodel)
- [`Admin.GenAuthKeyModel`](#admin-genauthkeymodel)
- [`Algorithm.AndroidDeviceInfo`](#algorithm-androiddeviceinfo)
- [`Customized.WXCTDUniftyAuthParmDoc`](#customized-wxctduniftyauthparmdoc)
- [`Favor.DelParamDoc`](#favor-delparamdoc)
- [`Favor.GetFavItemParamDoc`](#favor-getfavitemparamdoc)
- [`Favor.SyncParamDoc`](#favor-syncparamdoc)
- [`Finder.CommentParamDoc`](#finder-commentparamdoc)
- [`Finder.DecryptParamDoc`](#finder-decryptparamdoc)
- [`Finder.DefaultParamDoc`](#finder-defaultparamdoc)
- [`Finder.FinderGetMsgSessionIdParamDoc`](#finder-findergetmsgsessionidparamdoc)
- [`Finder.FinderGetTopicListParamDoc`](#finder-findergettopiclistparamdoc)
- [`Finder.FinderJoinLiveParamDoc`](#finder-finderjoinliveparamdoc)
- [`Finder.FinderLiveDetailParamDoc`](#finder-finderlivedetailparamdoc)
- [`Finder.FinderSendTextParamDoc`](#finder-findersendtextparamdoc)
- [`Finder.GetCommentDetailParamDoc`](#finder-getcommentdetailparamdoc)
- [`Finder.LikeParamDoc`](#finder-likeparamdoc)
- [`Finder.TargetUserPageParamDoc`](#finder-targetuserpageparamdoc)
- [`Friend.BlacklistParamDoc`](#friend-blacklistparamdoc)
- [`Friend.DefaultParamDoc`](#friend-defaultparamdoc)
- [`Friend.FriendRelationParamDoc`](#friend-friendrelationparamdoc)
- [`Friend.GetContractDetailparameterDoc`](#friend-getcontractdetailparameterdoc)
- [`Friend.GetContractListparameterDoc`](#friend-getcontractlistparameterdoc)
- [`Friend.LbsFindParamDoc`](#friend-lbsfindparamdoc)
- [`Friend.PassVerifyParamDoc`](#friend-passverifyparamdoc)
- [`Friend.SearchParamDoc`](#friend-searchparamdoc)
- [`Friend.SendRequestParamDoc`](#friend-sendrequestparamdoc)
- [`Friend.SetRemarksParamDoc`](#friend-setremarksparamdoc)
- [`Friend.UploadParamDoc`](#friend-uploadparamdoc)
- [`FriendCircle.CdnSnsImageUploadParamDoc`](#friendcircle-cdnsnsimageuploadparamdoc)
- [`FriendCircle.CommentParamDoc`](#friendcircle-commentparamdoc)
- [`FriendCircle.DownloadMediaModelDoc`](#friendcircle-downloadmediamodeldoc)
- [`FriendCircle.GetCommnetParamDoc`](#friendcircle-getcommnetparamdoc)
- [`FriendCircle.GetDetailparameterDoc`](#friendcircle-getdetailparameterdoc)
- [`FriendCircle.GetIdDetailParamDoc`](#friendcircle-getiddetailparamdoc)
- [`FriendCircle.GetListParamDoc`](#friendcircle-getlistparamdoc)
- [`FriendCircle.MessagearameterDoc`](#friendcircle-messagearameterdoc)
- [`FriendCircle.MmSnsSyncParamDoc`](#friendcircle-mmsnssyncparamdoc)
- [`FriendCircle.OperationParamDoc`](#friendcircle-operationparamdoc)
- [`FriendCircle.PrivacySettingsParamDoc`](#friendcircle-privacysettingsparamdoc)
- [`FriendCircle.RequestParamsDoc`](#friendcircle-requestparamsdoc)
- [`FriendCircle.SnsPostItemDoc`](#friendcircle-snspostitemdoc)
- [`FriendCircle.SnsUploadParamDoc`](#friendcircle-snsuploadparamdoc)
- [`FriendCircle.SnsUploadVideoParamDoc`](#friendcircle-snsuploadvideoparamdoc)
- [`Group.AddChatRoomParamDoc`](#group-addchatroomparamdoc)
- [`Group.ConsentToJoinParamDoc`](#group-consenttojoinparamdoc)
- [`Group.CreateChatRoomParamDoc`](#group-createchatroomparamdoc)
- [`Group.FacingCreateChatRoomParamDoc`](#group-facingcreatechatroomparamdoc)
- [`Group.GetChatRoomParamDoc`](#group-getchatroomparamdoc)
- [`Group.MoveContractListParamDoc`](#group-movecontractlistparamdoc)
- [`Group.OperateChatRoomAdminParamDoc`](#group-operatechatroomadminparamdoc)
- [`Group.OperateChatRoomInfoParamDoc`](#group-operatechatroominfoparamdoc)
- [`Group.QuitGroupParamDoc`](#group-quitgroupparamdoc)
- [`Group.ScanIntoGroupParamDoc`](#group-scanintogroupparamdoc)
- [`Group.SendPatParamDoc`](#group-sendpatparamdoc)
- [`Group.SetChatroomAccessVerifyParamDoc`](#group-setchatroomaccessverifyparamdoc)
- [`Group.TransferGroupOwnerParamDoc`](#group-transfergroupownerparamdoc)
- [`Label.AddParamDoc`](#label-addparamdoc)
- [`Label.DeleteParamDoc`](#label-deleteparamdoc)
- [`Label.UpdateListParamDoc`](#label-updatelistparamdoc)
- [`Label.UpdateNameParamDoc`](#label-updatenameparamdoc)
- [`Login.A16LoginParam`](#login-a16loginparam)
- [`Login.Data62LoginReq`](#login-data62loginreq)
- [`Login.Data62SMSAgainReq`](#login-data62smsagainreq)
- [`Login.Data62SMSVerifyReq`](#login-data62smsverifyreq)
- [`Login.ExtDeviceLoginConfirmParam`](#login-extdeviceloginconfirmparam)
- [`Login.GetQRReq`](#login-getqrreq)
- [`Login.MaccodeParam`](#login-maccodeparam)
- [`Login.VerificationcodeParam`](#login-verificationcodeparam)
- [`Msg.DefaultParamDoc`](#msg-defaultparamdoc)
- [`Msg.QuoteContextDoc`](#msg-quotecontextdoc)
- [`Msg.QuoteDoc`](#msg-quotedoc)
- [`Msg.QuoteResponseDoc`](#msg-quoteresponsedoc)
- [`Msg.QuoteSendReplyContext`](#msg-quotesendreplycontext)
- [`Msg.QuoteSendResult`](#msg-quotesendresult)
- [`Msg.RevokeMsgParamDoc`](#msg-revokemsgparamdoc)
- [`Msg.SendAppMsgParamDoc`](#msg-sendappmsgparamdoc)
- [`Msg.SendEmojiParamDoc`](#msg-sendemojiparamdoc)
- [`Msg.SendGroupMassMsgTextParamDoc`](#msg-sendgroupmassmsgtextparamdoc)
- [`Msg.SendImageMsgParamDoc`](#msg-sendimagemsgparamdoc)
- [`Msg.SendNewMsgParamDoc`](#msg-sendnewmsgparamdoc)
- [`Msg.SendVideoMsgParamDoc`](#msg-sendvideomsgparamdoc)
- [`Msg.SendVoiceMessageParamDoc`](#msg-sendvoicemessageparamdoc)
- [`Msg.ShareCardParamDoc`](#msg-sharecardparamdoc)
- [`Msg.ShareLocationParamDoc`](#msg-sharelocationparamdoc)
- [`Msg.ShareVideoMsgParamDoc`](#msg-sharevideomsgparamdoc)
- [`Msg.SyncParam2Doc`](#msg-syncparam2doc)
- [`Msg.SyncParamDoc`](#msg-syncparamdoc)
- [`OfficialAccounts.AuthMpLoginParam`](#officialaccounts-authmploginparam)
- [`OfficialAccounts.DefaultParam`](#officialaccounts-defaultparam)
- [`OfficialAccounts.GetMpHistoryMsgParam`](#officialaccounts-getmphistorymsgparam)
- [`OfficialAccounts.GetkeyParam`](#officialaccounts-getkeyparam)
- [`OfficialAccounts.QRConnectParam`](#officialaccounts-qrconnectparam)
- [`OfficialAccounts.ReadParam`](#officialaccounts-readparam)
- [`QWContact.AddWxAppRecordParam`](#qwcontact-addwxapprecordparam)
- [`QWContact.QWAddContactParam`](#qwcontact-qwaddcontactparam)
- [`QWContact.QWApplyAddContactParam`](#qwcontact-qwapplyaddcontactparam)
- [`SayHello.Model1Param`](#sayhello-model1param)
- [`SayHello.Model2Param`](#sayhello-model2param)
- [`SayHello.SendRequestParam1`](#sayhello-sendrequestparam1)
- [`Search.AIFirstPageRequest`](#search-aifirstpagerequest)
- [`Search.CGICallRequest`](#search-cgicallrequest)
- [`Search.Request`](#search-request)
- [`Search.VerticalFirstPageRequest`](#search-verticalfirstpagerequest)
- [`TenPay.CollectmoneyModel`](#tenpay-collectmoneymodel)
- [`TenPay.ConfirmPreTransfer`](#tenpay-confirmpretransfer)
- [`TenPay.GeMaSkdPayQCodeParam`](#tenpay-gemaskdpayqcodeparam)
- [`TenPay.GeneratePayQCodeModel`](#tenpay-generatepayqcodemodel)
- [`TenPay.HongBaoDetail`](#tenpay-hongbaodetail)
- [`TenPay.HongBaoParam`](#tenpay-hongbaoparam)
- [`TenPay.HongBaoTailParam`](#tenpay-hongbaotailparam)
- [`TenPay.OpenwxhbParam`](#tenpay-openwxhbparam)
- [`TenPay.QrydetailwxhbParam`](#tenpay-qrydetailwxhbparam)
- [`TenPay.ReceivewxhbParam`](#tenpay-receivewxhbparam)
- [`TenPay.RedPacket`](#tenpay-redpacket)
- [`TenPay.SjSkdPayQCodeParam`](#tenpay-sjskdpayqcodeparam)
- [`Tools.CdnDownloadImageParamDoc`](#tools-cdndownloadimageparamdoc)
- [`Tools.DownloadAppAttachParamDoc`](#tools-downloadappattachparamdoc)
- [`Tools.DownloadParamDoc`](#tools-downloadparamdoc)
- [`Tools.DownloadSectionDoc`](#tools-downloadsectiondoc)
- [`Tools.DownloadVoiceParamDoc`](#tools-downloadvoiceparamdoc)
- [`Tools.GetA8KeyParamDoc`](#tools-geta8keyparamdoc)
- [`Tools.HelperVerificationParamDoc`](#tools-helperverificationparamdoc)
- [`Tools.OauthSdkAppParamDoc`](#tools-oauthsdkappparamdoc)
- [`Tools.SetProxyParamDoc`](#tools-setproxyparamdoc)
- [`Tools.SetStepParamDoc`](#tools-setstepparamdoc)
- [`Tools.ThirdAppGrantParamDoc`](#tools-thirdappgrantparamdoc)
- [`Tools.UploadParamDoc`](#tools-uploadparamdoc)
- [`Translate.SendRequest`](#translate-sendrequest)
- [`Translate.TextRequest`](#translate-textrequest)
- [`User.BindMobileParam`](#user-bindmobileparam)
- [`User.BindQQParam`](#user-bindqqparam)
- [`User.DelSafetyInfoParam`](#user-delsafetyinfoparam)
- [`User.EmailParam`](#user-emailparam)
- [`User.GetQRCodeParam`](#user-getqrcodeparam)
- [`User.NewSetPasswdParam`](#user-newsetpasswdparam)
- [`User.NewVerifyPasswdParam`](#user-newverifypasswdparam)
- [`User.PrivacySettingsParam`](#user-privacysettingsparam)
- [`User.ReportMotionParam`](#user-reportmotionparam)
- [`User.SendVerifyMobileParam`](#user-sendverifymobileparam)
- [`User.SetAlisaParam`](#user-setalisaparam)
- [`User.UpdateProfileParam`](#user-updateprofileparam)
- [`User.UploadHeadImageParam`](#user-uploadheadimageparam)
- [`Voice.DecimalInt64`](#voice-decimalint64)
- [`Voice.MessageRequest`](#voice-messagerequest)
- [`Voice.Request`](#voice-request)
- [`Voice.ResultRequest`](#voice-resultrequest)
- [`Wxapp.AddAvatarImgParamDoc`](#wxapp-addavatarimgparamdoc)
- [`Wxapp.AddAvatarParamDoc`](#wxapp-addavatarparamdoc)
- [`Wxapp.AddWxAppRecordParamDoc`](#wxapp-addwxapprecordparamdoc)
- [`Wxapp.CheckVerifyCodeDataDoc`](#wxapp-checkverifycodedatadoc)
- [`Wxapp.CloudCallParamDoc`](#wxapp-cloudcallparamdoc)
- [`Wxapp.DefaultParam`](#wxapp-defaultparam)
- [`Wxapp.DefaultParamDoc`](#wxapp-defaultparamdoc)
- [`Wxapp.DelMobileDataDoc`](#wxapp-delmobiledatadoc)
- [`Wxapp.DellAvatarParamDoc`](#wxapp-dellavatarparamdoc)
- [`Wxapp.GETCreditScoreParam`](#wxapp-getcreditscoreparam)
- [`Wxapp.GetUserOpenIdParamDoc`](#wxapp-getuseropenidparamdoc)
- [`Wxapp.GetWxAppRecordParamDoc`](#wxapp-getwxapprecordparamdoc)
- [`Wxapp.GetpullPayParamDoc`](#wxapp-getpullpayparamdoc)
- [`Wxapp.JSOperateWxParamDoc`](#wxapp-jsoperatewxparamdoc)
- [`Wxapp.OauthListParamDoc`](#wxapp-oauthlistparamdoc)
- [`Wxapp.QrcodeAuthLoginParamDoc`](#wxapp-qrcodeauthloginparamdoc)
- [`Wxapp.SessionidQRParamDoc`](#wxapp-sessionidqrparamdoc)
- [`Wxapp.UnionpayDataDoc`](#wxapp-unionpaydatadoc)
- [`XiaoWei.BuluHistoryItemRequest`](#xiaowei-buluhistoryitemrequest)
- [`XiaoWei.BuluUserHistoryRequest`](#xiaowei-buluuserhistoryrequest)
- [`XiaoWei.CardScreenshotMediaRequest`](#xiaowei-cardscreenshotmediarequest)
- [`XiaoWei.CardScreenshotSecurityCheckRequest`](#xiaowei-cardscreenshotsecuritycheckrequest)
- [`XiaoWei.CardWrapRequest`](#xiaowei-cardwraprequest)
- [`XiaoWei.ChatBubbleExtraInfoRequest`](#xiaowei-chatbubbleextrainforequest)
- [`XiaoWei.DeleteHistoryItemListRequest`](#xiaowei-deletehistoryitemlistrequest)
- [`XiaoWei.DeleteHistoryItemRequest`](#xiaowei-deletehistoryitemrequest)
- [`XiaoWei.DeleteXiaoweiChatHistoryRequest`](#xiaowei-deletexiaoweichathistoryrequest)
- [`XiaoWei.GetA2AChatListRequest`](#xiaowei-geta2achatlistrequest)
- [`XiaoWei.GetChatHistoryListRequest`](#xiaowei-getchathistorylistrequest)
- [`XiaoWei.GetHalfScreenSuggestionsRequest`](#xiaowei-gethalfscreensuggestionsrequest)
- [`XiaoWei.GetRedDotRequest`](#xiaowei-getreddotrequest)
- [`XiaoWei.GetUserCardListRequest`](#xiaowei-getusercardlistrequest)
- [`XiaoWei.InviteUsersRequest`](#xiaowei-inviteusersrequest)
- [`XiaoWei.MarkRedDotReadValidRequest`](#xiaowei-markreddotreadvalidrequest)
- [`XiaoWei.PageContextRequest`](#xiaowei-pagecontextrequest)
- [`businesscfg.BusinessConfig`](#businesscfg-businessconfig)
- [`models.EmptyObject`](#models-emptyobject)
- [`models.ProxyInfo`](#models-proxyinfo)
- [`models.ResponseResult`](#models-responseresult)
- [`models.ResponseResult2`](#models-responseresult2)
- [`models.WebhookTestRequest`](#models-webhooktestrequest)
- [`webhook.WebhookConfig`](#webhook-webhookconfig)

---

## Admin.DelayAuthKeyModel

标题：DelayAuthKeyModel

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `authcode` | string |  |  |  |
| `days` | integer(int64) |  |  |  |

---

## Admin.DeleteAuthKeyModel

标题：DeleteAuthKeyModel

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `authcode` | string |  |  |  |

---

## Admin.GenAuthKeyModel

标题：GenAuthKeyModel

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `count` | integer(int64) |  |  |  |
| `days` | integer(int64) |  |  |  |
| `remark` | string |  |  |  |

---

## Algorithm.AndroidDeviceInfo

标题：AndroidDeviceInfo

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `AndriodBssId` | string |  |  |  |
| `AndriodFsId` | string |  |  |  |
| `AndriodId` | string |  |  |  |
| `AndriodSsId` | string |  |  |  |
| `Androidversion` | string |  |  |  |
| `Arch` | string |  |  |  |
| `BuildBoard` | string |  |  |  |
| `BuildFP` | string |  |  |  |
| `BuildID` | string |  |  |  |
| `Features` | string |  |  |  |
| `Hardware` | string |  |  |  |
| `Imei` | string |  |  |  |
| `KernelReleaseNumber` | string |  |  |  |
| `Manufacturer` | string |  |  |  |
| `PackageSign` | string |  |  |  |
| `PhoneModel` | string |  |  |  |
| `PhoneSerial` | string |  |  |  |
| `RadioVersion` | string |  |  |  |
| `SbMD5` | string |  |  |  |
| `SfArm64MD5` | string |  |  |  |
| `SfArmMD5` | string |  |  |  |
| `SfMD5` | string |  |  |  |
| `WLanAddress` | string |  |  |  |
| `WidevineDeviceID` | string |  |  |  |
| `WidevineProvisionID` | string |  |  |  |
| `WifiFullName` | string |  |  |  |
| `WifiName` | string |  |  |  |

---

## Customized.WXCTDUniftyAuthParmDoc

标题：WXCTDUniftyAuthParmDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Username` | string |  |  |  |

---

## Favor.DelParamDoc

标题：DelParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FavId` | integer(int32) |  |  |  |

---

## Favor.GetFavItemParamDoc

标题：GetFavItemParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FavId` | integer(int32) |  |  |  |

---

## Favor.SyncParamDoc

标题：SyncParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Keybuf` | string |  |  |  |

---

## Finder.CommentParamDoc

标题：CommentParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `CommentId` | integer(int64) |  |  |  |
| `Content` | string |  |  |  |
| `Id` | integer(int64) |  |  |  |
| `ObjectNonceId` | string |  |  |  |
| `OpType` | integer(int32) |  |  |  |
| `ReplyCommentId` | integer(int64) |  |  |  |
| `ReplyUsername` | string |  |  |  |
| `RootCommentId` | integer(int64) |  |  |  |
| `Scene` | integer(int32) |  |  |  |
| `SessionBuffer` | string |  |  |  |
| `Username` | string |  |  |  |

---

## Finder.DecryptParamDoc

标题：DecryptParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |

---

## Finder.DefaultParamDoc

标题：DefaultParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Value` | string |  |  |  |

---

## Finder.FinderGetMsgSessionIdParamDoc

标题：FinderGetMsgSessionIdParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |

---

## Finder.FinderGetTopicListParamDoc

标题：FinderGetTopicListParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LastBuffer` | string |  |  |  |
| `TopTitle` | string |  |  |  |

---

## Finder.FinderJoinLiveParamDoc

标题：FinderJoinLiveParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DetId` | integer(int64) |  |  |  |
| `FbrKey` | string |  |  |  |
| `FinderUser` | string |  |  |  |
| `Id` | integer(int64) |  |  |  |
| `ObjectNonceId` | string |  |  |  |

---

## Finder.FinderLiveDetailParamDoc

标题：FinderLiveDetailParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderNonceID` | string |  |  |  |
| `FinderObjectID` | integer(int64) |  |  |  |

---

## Finder.FinderSendTextParamDoc

标题：FinderSendTextParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Text` | string |  |  |  |

---

## Finder.GetCommentDetailParamDoc

标题：GetCommentDetailParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Id` | integer(int64) |  |  |  |
| `LastBuffer` | string |  |  |  |
| `ObjectNonceId` | string |  |  |  |
| `RootCommentId` | integer(int64) |  |  |  |

---

## Finder.LikeParamDoc

标题：LikeParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Id` | integer(int64) |  |  |  |
| `ObjectNonceId` | string |  |  |  |
| `SessionBuffer` | string |  |  |  |

---

## Finder.TargetUserPageParamDoc

标题：TargetUserPageParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LastBuffer` | string |  |  |  |
| `Target` | string |  |  |  |

---

## Friend.BlacklistParamDoc

标题：BlacklistParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `toWxid` | string |  |  |  |
| `val` | integer(int32) |  |  |  |

---

## Friend.DefaultParamDoc

标题：DefaultParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `toWxid` | string |  |  |  |

---

## Friend.FriendRelationParamDoc

标题：FriendRelationParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `opCode` | integer(int32) |  |  |  |
| `toWxid` | string |  |  |  |

---

## Friend.GetContractDetailparameterDoc

标题：GetContractDetailparameterDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `userName` | string |  |  |  |

---

## Friend.GetContractListparameterDoc

标题：GetContractListparameterDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `currentChatRoomContactSeq` | integer(int32) |  |  |  |
| `currentWxcontactSeq` | integer(int32) |  |  |  |

---

## Friend.LbsFindParamDoc

标题：LbsFindParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `latitude` | number(float) |  |  |  |
| `longitude` | number(float) |  |  |  |
| `opCode` | integer(int32) |  |  |  |

---

## Friend.PassVerifyParamDoc

标题：PassVerifyParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `opcode` | integer(int32) |  |  |  |
| `scene` | integer(int32) |  |  |  |
| `v1` | string |  |  |  |
| `v2` | string |  |  |  |

---

## Friend.SearchParamDoc

标题：SearchParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `fromScene` | integer(int32) |  |  |  |
| `keyword` | string |  |  |  |
| `searchScene` | integer(int32) |  |  |  |

---

## Friend.SendRequestParamDoc

标题：SendRequestParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `v1` | string |  |  |  |
| `v2` | string |  |  |  |

---

## Friend.SetRemarksParamDoc

标题：SetRemarksParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `remarks` | string |  |  |  |
| `toWxid` | string |  |  |  |

---

## Friend.UploadParamDoc

标题：UploadParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `currentPhoneNo` | string |  |  |  |
| `opcode` | integer(int32) |  |  |  |
| `phoneNo` | string |  |  |  |

---

## FriendCircle.CdnSnsImageUploadParamDoc

标题：CdnSnsImageUploadParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `imageData` | string |  |  |  |

---

## FriendCircle.CommentParamDoc

标题：CommentParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `content` | string |  |  |  |
| `id` | string |  |  |  |
| `replyCommnetId` | integer(int32) |  |  |  |
| `type` | integer(int32) |  |  |  |

---

## FriendCircle.DownloadMediaModelDoc

标题：DownloadMediaModelDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `key` | string |  |  |  |
| `url` | string |  |  |  |

---

## FriendCircle.GetCommnetParamDoc

标题：GetCommnetParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `xmlData` | string |  |  |  |

---

## FriendCircle.GetDetailparameterDoc

标题：GetDetailparameterDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `fristpagemd5` | string |  |  |  |
| `maxid` | integer(int64) |  |  |  |
| `towxid` | string |  |  |  |

---

## FriendCircle.GetIdDetailParamDoc

标题：GetIdDetailParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `id` | integer(int64) |  |  |  |
| `towxid` | string |  |  |  |

---

## FriendCircle.GetListParamDoc

标题：GetListParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `fristpagemd5` | string |  |  |  |
| `maxid` | integer(int64) |  |  |  |

---

## FriendCircle.MessagearameterDoc

标题：MessagearameterDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `blackList` | string |  |  |  |
| `content` | string |  |  |  |
| `withUserList` | string |  |  |  |

---

## FriendCircle.MmSnsSyncParamDoc

标题：MmSnsSyncParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `synckey` | string |  |  |  |

---

## FriendCircle.OperationParamDoc

标题：OperationParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `commnetId` | integer(int32) |  |  |  |
| `id` | string |  |  |  |
| `type` | integer(int32) |  |  |  |

---

## FriendCircle.PrivacySettingsParamDoc

标题：PrivacySettingsParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `function` | integer(int32) |  |  |  |
| `value` | integer(int32) |  |  |  |

---

## FriendCircle.RequestParamsDoc

标题：RequestParamsDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `forwardAddr` | string |  |  |  |
| `id` | string |  |  |  |

---

## FriendCircle.SnsPostItemDoc

标题：SnsPostItemDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `blackList` | string |  |  |  |
| `private` | integer(int64) |  |  |  |
| `thumbmd5` | string |  |  |  |
| `thumburl` | string |  |  |  |
| `title` | string |  |  |  |
| `totalSize` | string |  |  |  |
| `videomd5` | string |  |  |  |
| `videourl` | string |  |  |  |
| `withUserList` | string |  |  |  |

---

## FriendCircle.SnsUploadParamDoc

标题：SnsUploadParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `base64` | string |  |  |  |

---

## FriendCircle.SnsUploadVideoParamDoc

标题：SnsUploadVideoParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `thumbData` | string |  |  |  |
| `videoData` | string |  |  |  |

---

## Group.AddChatRoomParamDoc

标题：AddChatRoomParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ChatRoomName` | string |  |  |  |
| `ToWxids` | string |  |  |  |

---

## Group.ConsentToJoinParamDoc

标题：ConsentToJoinParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |

---

## Group.CreateChatRoomParamDoc

标题：CreateChatRoomParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ToWxids` | string |  |  |  |

---

## Group.FacingCreateChatRoomParamDoc

标题：FacingCreateChatRoomParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Latitude` | number(float) |  |  |  |
| `Longitude` | number(float) |  |  |  |
| `OpCode` | integer(int32) |  |  |  |
| `Password` | string |  |  |  |

---

## Group.GetChatRoomParamDoc

标题：GetChatRoomParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |

---

## Group.MoveContractListParamDoc

标题：MoveContractListParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |
| `Val` | integer(int32) |  |  |  |

---

## Group.OperateChatRoomAdminParamDoc

标题：OperateChatRoomAdminParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |
| `ToWxids` | string |  |  |  |
| `Val` | integer(int32) |  |  |  |

---

## Group.OperateChatRoomInfoParamDoc

标题：OperateChatRoomInfoParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `QID` | string |  |  |  |

---

## Group.QuitGroupParamDoc

标题：QuitGroupParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |

---

## Group.ScanIntoGroupParamDoc

标题：ScanIntoGroupParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |

---

## Group.SendPatParamDoc

标题：SendPatParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |
| `Scene` | integer(int64) |  |  |  |
| `ToUserName` | string |  |  |  |

---

## Group.SetChatroomAccessVerifyParamDoc

标题：SetChatroomAccessVerifyParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Enable` | boolean |  |  |  |
| `QID` | string |  |  |  |

---

## Group.TransferGroupOwnerParamDoc

标题：TransferGroupOwnerParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `NewOwnerUserName` | string |  |  |  |
| `QID` | string |  |  |  |

---

## Label.AddParamDoc

标题：AddParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LabelName` | string |  |  |  |

---

## Label.DeleteParamDoc

标题：DeleteParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LabelID` | string |  |  |  |

---

## Label.UpdateListParamDoc

标题：UpdateListParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LabelID` | string |  |  |  |
| `ToWxids` | string |  |  |  |

---

## Label.UpdateNameParamDoc

标题：UpdateNameParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LabelID` | integer(int32) |  |  |  |
| `NewName` | string |  |  |  |

---

## Login.A16LoginParam

标题：A16LoginParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `A16` | string |  |  |  |
| `DeviceName` | string |  |  |  |
| `Extend` | `Algorithm.AndroidDeviceInfo` |  |  |  |
| `Password` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `UserName` | string |  |  |  |

---

## Login.Data62LoginReq

标题：Data62LoginReq

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Data62` | string |  |  |  |
| `DeviceName` | string |  |  |  |
| `Password` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `UserName` | string |  |  |  |

---

## Login.Data62SMSAgainReq

标题：Data62SMSAgainReq

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Cookie` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `Url` | string |  |  |  |

---

## Login.Data62SMSVerifyReq

标题：Data62SMSVerifyReq

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Cookie` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `Sms` | string |  |  |  |
| `Url` | string |  |  |  |

---

## Login.ExtDeviceLoginConfirmParam

标题：ExtDeviceLoginConfirmParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |

---

## Login.GetQRReq

标题：GetQRReq

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

---

## Login.MaccodeParam

标题：MaccodeParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `authcode` | string |  | 旧客户端兼容授权码；推荐使用 X-Access-Token 请求头 |  |
| `deviceID` | string |  | GetMacQR 返回的设备 ID；留空时服务尝试根据 uuid 恢复 | device_id_from_login |
| `uuid` | string |  | GetMacQR 返回的 UUID | uuid_from_qr_response |

---

## Login.VerificationcodeParam

标题：VerificationcodeParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Code` | string |  |  |  |
| `Data62` | string |  |  |  |
| `Ticket` | string |  |  |  |
| `Uuid` | string |  |  |  |

---

## Msg.DefaultParamDoc

标题：DefaultParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `ToWxid` | string |  |  |  |

---

## Msg.QuoteContextDoc

标题：QuoteContextDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `chat_user_id` | string |  |  |  |
| `conversation_id` | string |  |  |  |
| `from_user_id` | string |  |  |  |
| `msg_id` | integer(int32) |  |  |  |
| `msg_type` | integer(int32) |  |  |  |
| `new_msg_id` | string |  |  |  |
| `quote_content` | string |  |  |  |
| `sequence` | integer(int32) |  |  |  |
| `svr_id` | string |  |  |  |
| `to_wxid` | string |  |  |  |

---

## Msg.QuoteDoc

标题：QuoteDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `chat_user_id` | string |  |  |  |
| `content` | string |  |  |  |
| `display_name` | string |  |  |  |
| `from_user_id` | string |  |  |  |
| `msg_type` | integer(int32) |  |  |  |
| `new_msg_id` | string |  |  |  |
| `quote_content` | string |  |  |  |
| `reply_context` | `Msg.QuoteContextDoc` |  |  |  |
| `sequence` | string |  |  |  |
| `svr_id` | string |  |  |  |
| `to_wxid` | string |  |  |  |

---

## Msg.QuoteResponseDoc

标题：QuoteResponseDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Code` | integer(int64) |  |  |  |
| `CodeValue` | string |  |  |  |
| `Data` | `Msg.QuoteSendResult` |  |  |  |
| `Message` | string |  |  |  |
| `Success` | boolean |  |  |  |
| `request_id` | string |  |  |  |

---

## Msg.QuoteSendReplyContext

标题：QuoteSendReplyContext

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `chat_user_id` | string |  |  |  |
| `conversation_id` | string |  |  |  |
| `from_user_id` | string |  |  |  |
| `msg_id` | integer(int32) |  |  |  |
| `msg_type` | integer(int32) |  |  |  |
| `new_msg_id` | string |  |  |  |
| `quote_content` | string |  |  |  |
| `svr_id` | string |  |  |  |
| `to_wxid` | string |  |  |  |

---

## Msg.QuoteSendResult

标题：QuoteSendResult

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `app_message_type` | integer(int32) |  |  |  |
| `client_msg_id` | string |  |  |  |
| `content` | string |  |  |  |
| `created_at` | integer(int64) |  |  |  |
| `from_user_id` | string |  |  |  |
| `id` | string |  |  |  |
| `local_id` | integer(int32) |  |  |  |
| `message_type` | integer(int32) |  |  |  |
| `msg_id` | integer(int32) |  |  |  |
| `new_msg_id` | string |  |  |  |
| `referenced_message_type` | integer(int32) |  |  |  |
| `referenced_svr_id` | string |  |  |  |
| `reply_context` | `Msg.QuoteSendReplyContext` |  |  |  |
| `svr_id` | string |  |  |  |
| `to_wxid` | string |  |  |  |

---

## Msg.RevokeMsgParamDoc

标题：RevokeMsgParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ClientMsgId` | integer(int64) |  |  |  |
| `CreateTime` | integer(int64) |  |  |  |
| `NewMsgId` | integer(int64) |  |  |  |
| `ToUserName` | string |  |  |  |

---

## Msg.SendAppMsgParamDoc

标题：SendAppMsgParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ToWxid` | string |  |  |  |
| `Type` | integer(int32) |  |  |  |
| `Xml` | string |  |  |  |

---

## Msg.SendEmojiParamDoc

标题：SendEmojiParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Md5` | string |  |  |  |
| `ToWxid` | string |  |  |  |
| `TotalLen` | integer(int32) |  |  |  |

---

## Msg.SendGroupMassMsgTextParamDoc

标题：SendGroupMassMsgTextParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `ToIds` | array<string> |  |  |  |

---

## Msg.SendImageMsgParamDoc

标题：SendImageMsgParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Base64` | string |  |  |  |
| `ToWxid` | string |  |  |  |

---

## Msg.SendNewMsgParamDoc

标题：SendNewMsgParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `At` | string |  |  |  |
| `Content` | string |  |  |  |
| `ToWxid` | string |  |  |  |
| `Type` | integer(int64) |  |  |  |

---

## Msg.SendVideoMsgParamDoc

标题：SendVideoMsgParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Base64` | string |  |  |  |
| `ImageBase64` | string |  |  |  |
| `PlayLength` | integer(int32) |  |  |  |
| `ToWxid` | string |  |  |  |

---

## Msg.SendVoiceMessageParamDoc

标题：SendVoiceMessageParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Base64` | string |  |  |  |
| `ToWxid` | string |  |  |  |
| `Type` | integer(int32) |  |  |  |
| `VoiceTime` | integer(int32) |  |  |  |

---

## Msg.ShareCardParamDoc

标题：ShareCardParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `CardAlias` | string |  |  |  |
| `CardNickName` | string |  |  |  |
| `CardWxId` | string |  |  |  |
| `ToWxid` | string |  |  |  |

---

## Msg.ShareLocationParamDoc

标题：ShareLocationParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Infourl` | string |  |  |  |
| `Label` | string |  |  |  |
| `Poiname` | string |  |  |  |
| `Scale` | number(double) |  |  |  |
| `ToWxid` | string |  |  |  |
| `X` | number(double) |  |  |  |
| `Y` | number(double) |  |  |  |

---

## Msg.ShareVideoMsgParamDoc

标题：ShareVideoMsgParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ToWxid` | string |  |  |  |
| `Xml` | string |  |  |  |

---

## Msg.SyncParam2Doc

标题：SyncParam2Doc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `TargetURL` | string |  |  |  |

---

## Msg.SyncParamDoc

标题：SyncParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Scene` | integer(int32) |  |  |  |
| `Synckey` | string |  |  |  |

---

## OfficialAccounts.AuthMpLoginParam

标题：AuthMpLoginParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `scene` | integer(int32) |  |  |  |
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

---

## OfficialAccounts.DefaultParam

标题：DefaultParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `wxid` | string |  |  |  |

---

## OfficialAccounts.GetMpHistoryMsgParam

标题：GetMpHistoryMsgParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

---

## OfficialAccounts.GetkeyParam

标题：GetkeyParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  | 公众号 AppID | wx1234567890abcdef |
| `url` | string |  | 需要 JSAPI 权限校验的完整页面 URL | https://example.com/article |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

---

## OfficialAccounts.QRConnectParam

标题：QRConnectParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

---

## OfficialAccounts.ReadParam

标题：ReadParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

---

## QWContact.AddWxAppRecordParam

标题：AddWxAppRecordParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Username` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## QWContact.QWAddContactParam

标题：QWAddContactParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Username` | string |  |  |  |
| `V1` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## QWContact.QWApplyAddContactParam

标题：QWApplyAddContactParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Context` | string |  |  |  |
| `Username` | string |  |  |  |
| `V1` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## SayHello.Model1Param

标题：Model1Param

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |
| `VerifyContent` | string |  |  |  |

---

## SayHello.Model2Param

标题：Model2Param

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `FromScene` | integer(int32) |  |  |  |
| `Scene` | integer(int64) |  |  |  |
| `SearchScene` | integer(int32) |  |  |  |
| `ToUserName` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## SayHello.SendRequestParam1

标题：SendRequestParam1

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Scene` | integer(int64) |  |  |  |
| `V3` | string |  |  |  |
| `V4` | string |  |  |  |
| `VerifyContent` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## Search.AIFirstPageRequest

标题：AIFirstPageRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `model` | string |  | 搜索模型；留空使用服务默认模型 | hy3-preview-proxy |
| `query` | string | 是 | AI 搜索问题，至少 2 个字符 | 深圳有哪些值得关注的科技公司 |
| `turn` | integer(int64) |  | 首轮固定为 0 | 0 |

---

## Search.CGICallRequest

标题：CGICallRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否在响应中附带原始协议数据 | False |
| `method` | string |  | 仅 HTTP 类服务需要；留空使用 Services 返回的默认方法 | POST |
| `payload` | object |  | 服务要求的 JSON 请求对象；字段由 Services 返回的具体能力决定 |  |
| `payload_base64` | string |  | 原始二进制请求的 Base64；与 payload、payload_hex 三选一 |  |
| `payload_hex` | string |  | 原始二进制请求的十六进制；与 payload、payload_base64 三选一 |  |

---

## Search.Request

标题：Request

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `a8_scene` | integer(int32) |  | GetA8Key 场景；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `category` | string |  | 搜索分类；Query 接口必填，独立分类接口由路由自动设置 | all |
| `code_type` | integer(int32) |  | GetA8Key 代码类型；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `code_version` | integer(int32) |  | GetA8Key 代码版本；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `cursor` | string |  | 续页游标；首页留空，续页原样传回上一页的 cursor |  |
| `include_raw` | boolean |  | 是否在响应中附带微信原始数据；调试时才建议开启 | False |
| `limit` | integer(int64) |  | 每页数量，建议 10，最大值由微信服务决定 | 10 |
| `offset` | integer(int64) |  | 结果偏移量；首页传 0，续页传上一页返回的 next_offset | 0 |
| `opcode` | integer(int32) |  | GetA8Key 操作码；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `path` | string |  | 网页网关路径；仅 Gateway 使用，留空采用默认搜索路径 | page/search/mobile_jump |
| `protocol_scene` | integer(int64) |  | 协议场景值；普通搜索使用 0 让服务自动选择 | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |
| `scene` | integer(int64) |  | 网页网关场景；仅 Gateway 使用，默认 4812 | 4812 |
| `search_id` | string |  | 续页标识；首页留空，续页原样传回上一页的 search_id |  |
| `type` | integer(int64) |  | 网页网关搜索类型；仅 Gateway 使用，默认 53 | 53 |

---

## Search.VerticalFirstPageRequest

标题：VerticalFirstPageRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string | 是 | 搜索关键词，至少 2 个字符 | 深圳科技 |

---

## TenPay.CollectmoneyModel

标题：CollectmoneyModel

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `invalidTime` | string |  | 收款请求失效时间 | 0 |
| `toUserName` | string |  | 付款方微信标识 | wxid_payer |
| `transFerId` | string |  | 转账标识 | transfer_id_from_message |
| `transactionId` | string |  | 交易标识 | transaction_id_from_message |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

---

## TenPay.ConfirmPreTransfer

标题：ConfirmPreTransfer

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `bankSerial` | string |  | 预支付响应中的银行卡序列号 | bank_serial_from_pre_transfer |
| `bankType` | string |  | 预支付响应中的银行类型 | CFT |
| `payPassword` | string |  | 支付密码；只通过 HTTPS 提交，不记录日志 | your_pay_password |
| `reqKey` | string |  | 预支付响应中的请求密钥 | req_key_from_pre_transfer |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

---

## TenPay.GeMaSkdPayQCodeParam

标题：GeMaSkdPayQCodeParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Money` | string |  |  |  |
| `Name` | string |  |  |  |
| `Remark` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## TenPay.GeneratePayQCodeModel

标题：GeneratePayQCodeModel

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `money` | string |  | 收款金额，单位元，最多两位小数 | 1.00 |
| `name` | string |  | 收款项目名称 | 商品款 |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

---

## TenPay.HongBaoDetail

标题：HongBaoDetail

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `offset` | integer(int64) |  | 领取记录分页偏移 | 0 |
| `size` | integer(int64) |  | 领取记录分页数量 | 20 |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |
| `xml` | string |  | 红包消息中的原始 XML | <msg></msg> |

---

## TenPay.HongBaoParam

标题：HongBaoParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `SendUserName` | string |  |  |  |
| `Wxid` | string |  |  |  |
| `Xml` | string |  |  |  |

---

## TenPay.HongBaoTailParam

标题：HongBaoTailParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `SendId` | string |  |  |  |
| `SendUserName` | string |  |  |  |
| `TimingIdentifier` | string |  |  |  |
| `Wxid` | string |  |  |  |
| `Xml` | string |  |  |  |

---

## TenPay.OpenwxhbParam

标题：OpenwxhbParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Encrypt_key` | string |  |  |  |
| `Encrypt_userinfo` | string |  |  |  |
| `SendUserName` | string |  |  |  |
| `TimingIdentifier` | string |  |  |  |
| `Wxid` | string |  |  |  |
| `Xml` | string |  |  |  |

---

## TenPay.QrydetailwxhbParam

标题：QrydetailwxhbParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Encrypt_key` | string |  |  |  |
| `Encrypt_userinfo` | string |  |  |  |
| `Wxid` | string |  |  |  |
| `Xml` | string |  |  |  |

---

## TenPay.ReceivewxhbParam

标题：ReceivewxhbParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Encrypt_key` | string |  |  |  |
| `Encrypt_userinfo` | string |  |  |  |
| `InWay` | string |  |  |  |
| `Wxid` | string |  |  |  |
| `Xml` | string |  |  |  |

---

## TenPay.RedPacket

标题：RedPacket

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `amount` | integer(int32) |  | 红包总金额，单位分 | 100 |
| `content` | string |  | 红包祝福语 | 恭喜发财 |
| `count` | integer(int32) |  | 红包个数 | 1 |
| `from` | integer(int32) |  | 红包来源场景 | 0 |
| `redType` | integer(int32) |  | 红包类型 | 0 |
| `username` | string |  | 接收人微信标识；群红包填写群 ID | wxid_recipient |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

---

## TenPay.SjSkdPayQCodeParam

标题：SjSkdPayQCodeParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Money` | string |  |  |  |
| `Name` | string |  |  |  |
| `Remark` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## Tools.CdnDownloadImageParamDoc

标题：CdnDownloadImageParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `file_aes_key` | string |  |  |  |
| `file_no` | string |  |  |  |

---

## Tools.DownloadAppAttachParamDoc

标题：DownloadAppAttachParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `app_id` | string |  |  |  |
| `attach_id` | string |  |  |  |
| `data_len` | integer(int64) |  |  |  |
| `section` | `Tools.DownloadSectionDoc` |  |  |  |
| `user_name` | string |  |  |  |

---

## Tools.DownloadParamDoc

标题：DownloadParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `compress_type` | integer(int64) |  |  |  |
| `data_len` | integer(int64) |  |  |  |
| `msg_id` | integer(int32) |  |  |  |
| `section` | `Tools.DownloadSectionDoc` |  |  |  |
| `to_wxid` | string |  |  |  |

---

## Tools.DownloadSectionDoc

标题：DownloadSectionDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `data_len` | integer(int32) |  |  |  |
| `start_pos` | integer(int32) |  |  |  |

---

## Tools.DownloadVoiceParamDoc

标题：DownloadVoiceParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `bufid` | string |  |  |  |
| `fromUserName` | string |  |  |  |
| `length` | integer(int64) |  |  |  |
| `msgId` | integer(int32) |  |  |  |

---

## Tools.GetA8KeyParamDoc

标题：GetA8KeyParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `codeType` | integer(int32) |  |  |  |
| `codeVersion` | integer(int32) |  |  |  |
| `cookieBase64` | string |  |  |  |
| `flag` | integer(int32) |  |  |  |
| `netType` | string |  |  |  |
| `opCode` | integer(int32) |  |  |  |
| `reqUrl` | string |  |  |  |
| `scene` | integer(int32) |  |  |  |

---

## Tools.HelperVerificationParamDoc

标题：HelperVerificationParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `gcc` | string |  |  |  |
| `mobile` | string |  |  |  |

---

## Tools.OauthSdkAppParamDoc

标题：OauthSdkAppParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `avatarId` | integer(int32) |  |  |  |
| `opt` | integer(int32) |  |  |  |
| `packageName` | string |  |  |  |
| `state` | string |  |  |  |

---

## Tools.SetProxyParamDoc

标题：SetProxyParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `proxy` | string |  |  |  |

---

## Tools.SetStepParamDoc

标题：SetStepParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `step` | integer(int64) |  |  |  |

---

## Tools.ThirdAppGrantParamDoc

标题：ThirdAppGrantParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `url` | string |  |  |  |

---

## Tools.UploadParamDoc

标题：UploadParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `base64` | string |  |  |  |

---

## Translate.SendRequest

标题：SendRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `at` | string |  |  |  |
| `source_lang` | string |  |  |  |
| `target_lang` | string |  |  |  |
| `text` | string |  |  |  |
| `to_wxid` | string |  |  |  |

---

## Translate.TextRequest

标题：TextRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `source_lang` | string |  |  |  |
| `target_lang` | string |  |  |  |
| `text` | string |  |  |  |

---

## User.BindMobileParam

标题：BindMobileParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Mobile` | string |  |  |  |
| `Verifycode` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.BindQQParam

标题：BindQQParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Account` | integer(int32) |  |  |  |
| `Password` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.DelSafetyInfoParam

标题：DelSafetyInfoParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Uuid` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.EmailParam

标题：EmailParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Email` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.GetQRCodeParam

标题：GetQRCodeParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Style` | integer(int32) |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.NewSetPasswdParam

标题：NewSetPasswdParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `NewPassword` | string |  |  |  |
| `Ticket` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.NewVerifyPasswdParam

标题：NewVerifyPasswdParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Password` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.PrivacySettingsParam

标题：PrivacySettingsParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Function` | integer(int32) |  |  |  |
| `Value` | integer(int32) |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.ReportMotionParam

标题：ReportMotionParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceId` | string |  |  |  |
| `DeviceType` | string |  |  |  |
| `StepCount` | integer(int64) |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.SendVerifyMobileParam

标题：SendVerifyMobileParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Mobile` | string |  |  |  |
| `Opcode` | integer(int32) |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.SetAlisaParam

标题：SetAlisaParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Alisa` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.UpdateProfileParam

标题：UpdateProfileParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `City` | string |  |  |  |
| `Country` | string |  |  |  |
| `NickName` | string |  |  |  |
| `Province` | string |  |  |  |
| `Sex` | integer(int32) |  |  |  |
| `Signature` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## User.UploadHeadImageParam

标题：UploadHeadImageParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Base64` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## Voice.DecimalInt64

标题：DecimalInt64

_（无字段 / 基础类型）_

---

## Voice.MessageRequest

标题：MessageRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `bits_per_sample` | integer(int32) |  |  |  |
| `chat_room_name` | string |  |  |  |
| `client_msg_id` | string |  |  |  |
| `encode_type` | integer(int32) |  |  |  |
| `file_type` | integer(int32) |  |  |  |
| `from_user_name` | string |  |  |  |
| `length` | integer(int64) |  |  |  |
| `master_buf_id` | `Voice.DecimalInt64` |  |  |  |
| `msg_id` | integer(int32) |  |  |  |
| `new_msg_id` | `Voice.DecimalInt64` |  |  |  |
| `poll_interval_ms` | integer(int64) |  |  |  |
| `sample_rate` | integer(int32) |  |  |  |
| `scene` | integer(int32) |  |  |  |
| `to_user_name` | string |  |  |  |
| `voice_id` | string |  |  |  |
| `wait_seconds` | integer(int64) |  |  |  |

---

## Voice.Request

标题：Request

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `audio_base64` | string |  |  |  |
| `bits_per_sample` | integer(int32) |  |  |  |
| `chunk_size` | integer(int64) |  |  |  |
| `encode_type` | integer(int32) |  |  |  |
| `file_type` | integer(int32) |  |  |  |
| `from_user_name` | string |  |  |  |
| `poll_interval_ms` | integer(int64) |  |  |  |
| `sample_rate` | integer(int32) |  |  |  |
| `scene` | integer(int32) |  |  |  |
| `to_user_name` | string |  |  |  |
| `voice_id` | string |  |  |  |
| `wait_seconds` | integer(int64) |  |  |  |

---

## Voice.ResultRequest

标题：ResultRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `voice_id` | string |  |  |  |

---

## Wxapp.AddAvatarImgParamDoc

标题：AddAvatarImgParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `jpgLink` | string |  |  |  |

---

## Wxapp.AddAvatarParamDoc

标题：AddAvatarParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `aFilekey` | string |  |  |  |
| `appid` | string |  |  |  |
| `nickName` | string |  |  |  |

---

## Wxapp.AddWxAppRecordParamDoc

标题：AddWxAppRecordParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `username` | string |  |  |  |

---

## Wxapp.CheckVerifyCodeDataDoc

标题：CheckVerifyCodeDataDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `mobile` | string |  |  |  |
| `verifyCode` | string |  |  |  |

---

## Wxapp.CloudCallParamDoc

标题：CloudCallParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `data` | string |  |  |  |

---

## Wxapp.DefaultParam

标题：DefaultParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Appid` | string |  |  |  |
| `Wxid` | string |  |  |  |

---

## Wxapp.DefaultParamDoc

标题：DefaultParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |

---

## Wxapp.DelMobileDataDoc

标题：DelMobileDataDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `mobile` | string |  |  |  |
| `opcode` | integer(int64) |  |  |  |

---

## Wxapp.DellAvatarParamDoc

标题：DellAvatarParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `avatarId` | integer(int32) |  |  |  |

---

## Wxapp.GETCreditScoreParam

标题：GETCreditScoreParam

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Wxid` | string |  |  |  |

---

## Wxapp.GetUserOpenIdParamDoc

标题：GetUserOpenIdParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `toWxId` | string |  |  |  |

---

## Wxapp.GetWxAppRecordParamDoc

标题：GetWxAppRecordParamDoc

_（无字段 / 基础类型）_

---

## Wxapp.GetpullPayParamDoc

标题：GetpullPayParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `nonceStr` | string |  |  |  |
| `package` | string |  |  |  |
| `paySign` | string |  |  |  |
| `sessionid` | string |  |  |  |
| `timeStamp` | string |  |  |  |

---

## Wxapp.JSOperateWxParamDoc

标题：JSOperateWxParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `data` | string |  |  |  |
| `opt` | integer(int64) |  |  |  |

---

## Wxapp.OauthListParamDoc

标题：OauthListParamDoc

_（无字段 / 基础类型）_

---

## Wxapp.QrcodeAuthLoginParamDoc

标题：QrcodeAuthLoginParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `uuid` | string |  |  |  |

---

## Wxapp.SessionidQRParamDoc

标题：SessionidQRParamDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `nonceStr` | string |  |  |  |
| `package` | string |  |  |  |
| `paySign` | string |  |  |  |
| `sessionid` | string |  |  |  |
| `timeStamp` | string |  |  |  |

---

## Wxapp.UnionpayDataDoc

标题：UnionpayDataDoc

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `nonceStr` | string |  |  |  |
| `package` | string |  |  |  |
| `paySign` | string |  |  |  |
| `sessionid` | string |  |  |  |
| `timeStamp` | string |  |  |  |

---

## XiaoWei.BuluHistoryItemRequest

标题：BuluHistoryItemRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `answer_cards` | array<`XiaoWei.CardWrapRequest`> |  |  |  |
| `dialogue_id` | integer(int64) |  |  |  |
| `question_cards` | array<`XiaoWei.CardWrapRequest`> |  |  |  |
| `timestamp` | integer(int64) |  |  |  |
| `trace_id` | string |  |  |  |

---

## XiaoWei.BuluUserHistoryRequest

标题：BuluUserHistoryRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `items` | array<`XiaoWei.BuluHistoryItemRequest`> |  |  |  |
| `operation_type` | integer(int32) |  |  |  |
| `test` | boolean |  |  |  |

---

## XiaoWei.CardScreenshotMediaRequest

标题：CardScreenshotMediaRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `aes_key` | string |  |  |  |
| `app_type` | integer(int32) |  |  |  |
| `file_id` | string |  |  |  |
| `file_type` | integer(int32) |  |  |  |
| `image_url` | string |  |  |  |
| `msg_type` | string |  |  |  |

---

## XiaoWei.CardScreenshotSecurityCheckRequest

标题：CardScreenshotSecurityCheckRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `app_id` | string |  |  |  |
| `media` | array<`XiaoWei.CardScreenshotMediaRequest`> |  |  |  |
| `message_id` | string |  |  |  |
| `trace_message_id` | string |  |  |  |

---

## XiaoWei.CardWrapRequest

标题：CardWrapRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `interactive_card_json` | string |  |  |  |
| `protobuf_base64` | string |  |  |  |
| `type` | integer(int32) |  |  |  |
| `xml` | string |  |  |  |

---

## XiaoWei.ChatBubbleExtraInfoRequest

标题：ChatBubbleExtraInfoRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `dialogue_id` | integer(int64) |  |  |  |
| `message_id` | integer(int64) |  |  |  |
| `timestamp` | integer(int64) |  |  |  |
| `trace_id` | string |  |  |  |

---

## XiaoWei.DeleteHistoryItemListRequest

标题：DeleteHistoryItemListRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `items` | array<`XiaoWei.DeleteHistoryItemRequest`> |  |  |  |

---

## XiaoWei.DeleteHistoryItemRequest

标题：DeleteHistoryItemRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `answers` | array<string> |  |  |  |
| `checked_ids` | array<string> |  |  |  |
| `dialogue_id` | string |  |  |  |
| `query` | string |  |  |  |
| `timestamp` | integer(int64) |  |  |  |
| `trace_id` | string |  |  |  |
| `unchecked_ids` | array<string> |  |  |  |

---

## XiaoWei.DeleteXiaoweiChatHistoryRequest

标题：DeleteXiaoweiChatHistoryRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `delete_item_lists` | array<`XiaoWei.DeleteHistoryItemListRequest`> |  |  |  |

---

## XiaoWei.GetA2AChatListRequest

标题：GetA2AChatListRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `limit` | integer(int32) |  |  |  |
| `page_context` | string |  |  |  |

---

## XiaoWei.GetChatHistoryListRequest

标题：GetChatHistoryListRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `clicked_bubble` | `XiaoWei.ChatBubbleExtraInfoRequest` |  |  |  |
| `down_context` | `XiaoWei.PageContextRequest` |  |  |  |
| `scroll_type` | integer(int32) |  |  |  |
| `up_context` | `XiaoWei.PageContextRequest` |  |  |  |

---

## XiaoWei.GetHalfScreenSuggestionsRequest

标题：GetHalfScreenSuggestionsRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `share_type` | integer(int32) |  |  |  |
| `ui_state` | integer(int32) |  |  |  |

---

## XiaoWei.GetRedDotRequest

标题：GetRedDotRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `debug_info` | string |  |  |  |

---

## XiaoWei.GetUserCardListRequest

标题：GetUserCardListRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `card_type` | integer(int32) |  |  |  |
| `page_context` | `XiaoWei.PageContextRequest` |  |  |  |

---

## XiaoWei.InviteUsersRequest

标题：InviteUsersRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `wxids` | array<string> |  |  |  |

---

## XiaoWei.MarkRedDotReadValidRequest

标题：MarkRedDotReadValidRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `debug_info` | string |  |  |  |
| `last_read_timestamp` | integer(int64) |  |  |  |
| `reddot_id` | integer(int64) |  |  |  |

---

## XiaoWei.PageContextRequest

标题：PageContextRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `has_more` | boolean |  |  |  |
| `limit_count` | integer(int32) |  |  |  |
| `offset` | integer(int32) |  |  |  |
| `time_cursor` | integer(int64) |  |  |  |

---

## businesscfg.BusinessConfig

标题：BusinessConfig

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `eventUrl` | string |  |  |  |
| `logoutUrl` | string |  |  |  |
| `syncMessageUrl` | string |  |  |  |

---

## models.EmptyObject

标题：EmptyObject

_（无字段 / 基础类型）_

---

## models.ProxyInfo

标题：ProxyInfo

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ProxyIp` | string |  |  |  |
| `ProxyPassword` | string |  |  |  |
| `ProxyUser` | string |  |  |  |

---

## models.ResponseResult

标题：ResponseResult

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Code` | integer(int64) |  |  |  |
| `CodeValue` | string |  |  |  |
| `Data` | object |  | 业务响应数据；结构由具体接口决定 |  |
| `Data62` | string |  |  |  |
| `Debug` | string |  |  |  |
| `ID` | integer(int64) |  |  |  |
| `Message` | string |  |  |  |
| `Success` | boolean |  |  |  |
| `request_id` | string |  |  |  |

---

## models.ResponseResult2

标题：ResponseResult2

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Code` | integer(int64) |  |  |  |
| `CodeValue` | string |  |  |  |
| `Data` | object |  | 业务响应数据；结构由具体接口决定 |  |
| `Data62` | string |  |  |  |
| `DeviceId` | string |  |  |  |
| `Message` | string |  |  |  |
| `Success` | boolean |  |  |  |
| `request_id` | string |  |  |  |

---

## models.WebhookTestRequest

标题：WebhookTestRequest

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `MessageType` | string |  |  |  |
| `TestData` | object |  | 业务响应数据；结构由具体接口决定 |  |

---

## webhook.WebhookConfig

标题：WebhookConfig

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `enabled` | boolean |  |  |  |
| `enabledSet` | boolean |  |  |  |
| `includeSelfMessage` | boolean |  |  |  |
| `messageTypes` | array<string> |  |  |  |
| `retryCount` | integer(int64) |  |  |  |
| `retryCountSet` | boolean |  |  |  |
| `secret` | string |  |  |  |
| `timeout` | integer(int64) |  |  |  |
| `url` | string |  |  |  |

---
