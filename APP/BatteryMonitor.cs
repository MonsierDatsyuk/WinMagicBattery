using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles; // 加入這個來安全管理 Handle
using HidLibrary;
// 記得在檔案最上面加入這行，才能使用 Debug 類別
using System.Diagnostics;

namespace MagicKeyboardMonitor
{
    public class BatteryMonitor
    {
        // ==========================================
        //  Windows 原生 API 宣告 (完美還原你的 C++ 邏輯)
        // ==========================================
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetInputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        // ==========================================

        private const int AppleVendorId = 0x004C;
        private bool _isMonitoring = false;

        public event Action<int>? OnBatteryUpdated;
        public event Action? OnDeviceLost;

        public async Task StartMonitoringAsync(int targetPid)
        {
            _isMonitoring = true;
            int consecutiveFailures = 0;
            const int MAX_FAILURES_ALLOWED = 3; 

            while (_isMonitoring)
            {
                int batteryLevel = GetBatteryLevel(targetPid);
                Debug.WriteLine($"[Monitor] battery: {batteryLevel}");
                if (batteryLevel >= 0)
                {
                    Debug.WriteLine("[Monitor] -> OnBatteryUpdated event");
                    // get battery -> reset
                    consecutiveFailures = 0;
                    OnBatteryUpdated?.Invoke(batteryLevel);
                }
                else
                {
                    // fail
                    consecutiveFailures++;

                    // 只有當「連續失敗次數」達到我們設定的上限時，才真正觸發斷線 UI
                    if (consecutiveFailures >= MAX_FAILURES_ALLOWED)
                    {
                        Debug.WriteLine("[Monitor] Can't find device -> OnDeviceLost event");
                        OnDeviceLost?.Invoke();
                        // avoid infinite
                        consecutiveFailures = MAX_FAILURES_ALLOWED;
                    }
                }

                // 5sec/time
                await Task.Delay(5000);
            }
        }


        public void StopMonitoring()
        {
            _isMonitoring = false;
        }

        private int GetBatteryLevel(int pid)
        {
            // 找出所有符合 PID 的設備通道
            var devices = HidDevices.Enumerate(AppleVendorId, pid).ToList();

            foreach (var device in devices)
            {
                // 使用 Windows API 親自開啟設備路徑 (取代 device.OpenDevice)
                using (SafeFileHandle handle = CreateFile(
                    device.DevicePath,
                    GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero))
                {
                    // 檢查是否成功開啟
                    if (!handle.IsInvalid)
                    {
                        byte[] buffer = new byte[3];

                        // 重試機制
                        for (int i = 0; i < 3; i++)
                        {
                            buffer[0] = 0x90;

                            // 傳入我們親自取得的 handle
                            bool success = HidD_GetInputReport(handle, buffer, buffer.Length);

                            if (success && buffer[0] == 0x90)
                            {
                                return buffer[2]; // 成功讀到電量！
                            }
                            Thread.Sleep(50);
                        }
                    }
                } // 使用 using 區塊，程式會自動幫我們呼叫 CloseHandle，防止記憶體洩漏！
            }

            return -1;
        }
    }
}