//using System;
//using System.Windows;
//using System.Windows.Forms;
//using Application = System.Windows.Application;
//// 加入這個命名空間以使用 Debug.WriteLine
//using System.Diagnostics;

//namespace MagicKeyboardMonitor
//{
//    public partial class App : Application
//    {
//        private NotifyIcon _notifyIcon;

//        protected override void OnStartup(StartupEventArgs e)
//        {
//            base.OnStartup(e);

//            _notifyIcon = new NotifyIcon();
//            _notifyIcon.Icon = System.Drawing.SystemIcons.Information;
//            _notifyIcon.Visible = true;
//            _notifyIcon.Text = "Monitoring Magic Keyboard...";

//            var contextMenu = new ContextMenuStrip();
//            contextMenu.Items.Add("Settings", null, OnSettingsClicked);
//            contextMenu.Items.Add("-");
//            contextMenu.Items.Add("Exit", null, OnExitClicked);

//            _notifyIcon.ContextMenuStrip = contextMenu;

//            // ==========================================
//            //  在這裡加入選項 1 的測試代碼 (Test Scanner)
//            // ==========================================

//        //    // 實例化我們剛剛寫的掃描器
//        //    var scanner = new DeviceScanner();
//        //    // 取得所有過濾後的 Apple 設備
//        //    var devices = scanner.ScanForAppleDevices();

//        //    Debug.WriteLine("=== Apple Device Scan Started ===");

//        //    if (devices.Count == 0)
//        //    {
//        //        Debug.WriteLine("[!] No Apple devices found. Check Bluetooth/USB connection.");
//        //    }
//        //    else
//        //    {
//        //        // 迴圈印出所有找到的設備詳細資訊
//        //        foreach (var device in devices)
//        //        {
//        //            Debug.WriteLine($"[+] Device Found: {device.DisplayName}");
//        //            // 使用 :X4 將整數格式化為 4 位數的十六進位 (例如 0x029C)
//        //            Debug.WriteLine($"    PID: 0x{device.ProductId:X4}");
//        //            Debug.WriteLine($"    Path: {device.DevicePath}");
//        //            Debug.WriteLine("-----------------------------------");
//        //        }
//        //    }
//        //    Debug.WriteLine("=== Apple Device Scan Ended ===");
//        }

//        private void OnSettingsClicked(object sender, EventArgs e)
//        {
//            //System.Windows.MessageBox.Show("Settings window coming soon!", "Notification");
//            // 實例化並顯示設定視窗
//            var settingsWindow = new MainWindow();
//            settingsWindow.Show();
//        }

//        private void OnExitClicked(object sender, EventArgs e)
//        {
//            _notifyIcon.Dispose();
//            Current.Shutdown();
//        }
//    }
//}