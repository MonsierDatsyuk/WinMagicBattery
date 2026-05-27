namespace MagicKeyboardMonitor
{
    // 用於儲存單個 Apple 藍牙/USB 設備的資訊
    public class AppleDeviceModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string DevicePath { get; set; } = string.Empty;
    }
}