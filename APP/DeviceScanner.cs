using System.Collections.Generic;
using System.Linq;
using HidLibrary;

namespace MagicKeyboardMonitor
{
    public class DeviceScanner
    {
        // Apple 設備的專屬 Vendor ID
        private const int AppleVendorId_Bluetooth = 0x004C;
        private const int AppleVendorId_USB = 0x05AC;

        // 建立 PID 與設備名稱的映射表
        private readonly Dictionary<int, string> _knownDevices = new Dictionary<int, string>
        {
            { 0x0267, "Magic Keyboard" },
            { 0x026C, "Magic Keyboard with Numeric Keypad" },
            { 0x029C, "Magic Keyboard (Mac Studio Edition)" },
            { 0x029A, "Magic Keyboard (Standard)" },
            { 0x0322, "Magic Keyboard with Touch ID" },
            { 0x0273, "Magic Keyboard (USB Edition)" },
            { 0x020E, "Apple Wireless Keyboard" },
            { 0x020F, "Apple Wireless Keyboard" },
            { 0x0257, "Magic Keyboard (Bluetooth Edition)" },
            { 0x110B, "Apple EarPods (USB-C, NO need)" },
        };

        public List<AppleDeviceModel> ScanForAppleDevices()
        {
            var resultList = new List<AppleDeviceModel>();

            // 呼叫 HidLibrary 取得所有 VID 為 0x004C 的設備
            var hidDevices = new List<HidDevice>();
            hidDevices.AddRange(HidDevices.Enumerate(AppleVendorId_Bluetooth));
            hidDevices.AddRange(HidDevices.Enumerate(AppleVendorId_USB));

            foreach (var device in hidDevices)
            {
                int pid = device.Attributes.ProductId;

                // 檢查是否為我們已知的鍵盤型號，若不是則顯示其 PID
                string deviceName = _knownDevices.ContainsKey(pid)
                    ? _knownDevices[pid]
                    : $"Unknown Apple Device (PID: 0x{pid:X4})";

                resultList.Add(new AppleDeviceModel
                {
                    DisplayName = deviceName,
                    ProductId = pid,
                    DevicePath = device.DevicePath
                });
            }

            // 移除重複的設備
            // 這裡改用 ProductId 來分組，確保設定選單中同一個型號的鍵盤只會出現一個選項
            var uniqueDevices = resultList
                .GroupBy(d => d.ProductId)
                .Select(g => g.First())
                .ToList();

            return uniqueDevices;
        }
    }
}