using System;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MoonWeChat.Services
{
    /// <summary>
    /// WP8.1 不能用 PickSingleFileAsync，必须 PickSingleFileAndContinue + OnActivated 续接。
    /// 选图/选文件前登记回调，App 激活时分发。
    /// </summary>
    public static class FilePickerContinuation
    {
        public const string OperationImage = "send-image";
        public const string OperationFile = "send-file";

        private static Action<StorageFile, string> _handler;

        public static void SetHandler(Action<StorageFile, string> handler)
        {
            _handler = handler;
        }

        public static void ClearHandler()
        {
            _handler = null;
        }

        public static void PickImage()
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".bmp");
            picker.ContinuationData["op"] = OperationImage;
            picker.PickSingleFileAndContinue();
        }

        public static void PickFile()
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");
            picker.ContinuationData["op"] = OperationFile;
            picker.PickSingleFileAndContinue();
        }

        public static void Continue(FileOpenPickerContinuationEventArgs args)
        {
            string op = null;
            if (args != null && args.ContinuationData != null && args.ContinuationData.ContainsKey("op"))
            {
                op = args.ContinuationData["op"] as string;
            }

            var handler = _handler;
            if (handler != null)
            {
                var file = args != null && args.Files != null && args.Files.Count > 0 ? args.Files[0] : null;
                handler(file, op);
            }
        }
    }
}
