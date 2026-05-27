using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32; // 用來操作 Windows 登錄檔

namespace MagicKeyboardMonitor
{
    public partial class MainWindow : Window
    {
        // 定義註冊表路徑與我們程式的名稱
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "WinMagicBattery";

        public MainWindow()
        {
            InitializeComponent();

            // 視窗一載入，就開始掃描設備
            LoadDevices();

            // 視窗載入時，檢查目前是否已經設定為開機啟動
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false))
            {
                if (key != null)
                {
                    object? value = key.GetValue(AppName);
                    // 如果登錄檔裡面的路徑跟我們現在執行的路徑一樣，就把 CheckBox 打勾
                    if (value != null && value.ToString() == Environment.ProcessPath)
                    {
                        AutoStartCheckBox.IsChecked = true;
                    }
                }
            }

            // Загружаем сохраненное состояние Fn Lock
            LoadFnLockSetting();
        }

        private void LoadDevices()
        {
            var scanner = new DeviceScanner();
            var rawDevices = scanner.ScanForAppleDevices();

            // 關鍵：將相同 PID 的裝置分組，只取第一個。這樣畫面上就不會出現重複的鍵盤！
            var displayDevices = rawDevices
                .GroupBy(d => d.ProductId)
                .Select(g => g.First())
                .ToList();

            if (displayDevices.Count > 0)
            {
                // 將過濾後的設備綁定到下拉選單
                DeviceComboBox.ItemsSource = displayDevices;
                // 設定下拉選單要顯示模型中的哪一個字串
                DeviceComboBox.DisplayMemberPath = "DisplayName";
                // 預設選擇第一個
                DeviceComboBox.SelectedIndex = 0;

                StatusText.Text = $"Scan complete! Found {displayDevices.Count} Apple device(s).";

                // Проверяем доступность устройства для Fn Lock
                CheckFnLockAvailability();
            }
            else
            {
                StatusText.Text = "No Apple devices found. Check Bluetooth/USB connection.";
                DeviceComboBox.IsEnabled = false;
                SaveButton.IsEnabled = false;
                FnLockCheckBox.IsEnabled = false;
            }
        }

        private void LoadFnLockSetting()
        {
            // Устанавливаем состояние CheckBox из сохраненных настроек
            FnLockCheckBox.IsChecked = APP.Properties.Settings.Default.FnLockDisabled;

            // Применяем настройку к устройству, если CheckBox имеет значение
            if (FnLockCheckBox.IsChecked.HasValue)
            {
                ApplyFnLockSetting(FnLockCheckBox.IsChecked.Value);
            }
        }

        private void CheckFnLockAvailability()
        {
            try
            {
                // Проверяем доступность устройства для Fn Lock
                bool isAvailable = AppleKeyboardControl.IsDeviceAvailable();
                FnLockCheckBox.IsEnabled = isAvailable;

                if (!isAvailable)
                {
                    StatusText.Text += " Fn Lock control not available.";
                }
            }
            catch
            {
                FnLockCheckBox.IsEnabled = false;
            }
        }

        // 點擊儲存按鈕的事件
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem is AppleDeviceModel selectedDevice)
            {
                int targetPid = selectedDevice.ProductId;

                // 1. 將 PID 存入 Windows 設定檔
                APP.Properties.Settings.Default.TargetPid = targetPid;
                APP.Properties.Settings.Default.Save();

                // 處理開機自動啟動的邏輯
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null)
                    {
                        if (AutoStartCheckBox.IsChecked == true)
                        {
                            // 打勾：把目前程式的絕對路徑寫入登錄檔
                            key.SetValue(AppName, Environment.ProcessPath!);
                        }
                        else
                        {
                            // 沒打勾：從登錄檔中刪除，取消開機啟動
                            key.DeleteValue(AppName, false);
                        }
                    }
                }

                // 3. 啟動背景監控
                var app = (App)System.Windows.Application.Current;
                app.StartBatteryMonitor(targetPid);
                this.Close();
            }
        }

        private async void FnLockCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            bool isChecked = FnLockCheckBox.IsChecked ?? false;
            await ApplyFnLockSettingAsync(isChecked);
        }

        private async void FnLockCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            bool isChecked = FnLockCheckBox.IsChecked ?? false;
            await ApplyFnLockSettingAsync(isChecked);
        }

        private void ApplyFnLockSetting(bool disableFnLock)
        {
            try
            {
                // Передаем значение: 1 = отключить Fn lock, 0 = включить
                uint codeValue = disableFnLock ? 1u : 0u;

                // Пытаемся использовать современный путь к устройству
                try
                {
                    AppleKeyboardControl.SetFnLockMode(codeValue);
                }
                catch (Win32Exception)
                {
                    // Если не получилось, пробуем старый путь
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = "Trying legacy device path...";
                    });
                    AppleKeyboardControl.SetFnLockMode(codeValue, @"\\.\AppleKeyboard");
                }

                // Сохраняем настройку
                APP.Properties.Settings.Default.FnLockDisabled = disableFnLock;
                APP.Properties.Settings.Default.Save();

                // Обновляем статус
                string status = disableFnLock ?
                    "Fn Lock disabled. Fn keys work as media keys." :
                    "Fn Lock enabled. Fn keys work as function keys.";
                StatusText.Text = status;
            }
            catch (Win32Exception ex)
            {
                StatusText.Text = $"Failed to set Fn Lock mode: {ex.Message} (Error: 0x{ex.NativeErrorCode:X})";

                // При ошибке возвращаем CheckBox в прежнее состояние
                FnLockCheckBox.Checked -= FnLockCheckBox_Checked;
                FnLockCheckBox.Unchecked -= FnLockCheckBox_Unchecked;
                FnLockCheckBox.IsChecked = !disableFnLock;
                FnLockCheckBox.Checked += FnLockCheckBox_Checked;
                FnLockCheckBox.Unchecked += FnLockCheckBox_Unchecked;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Unexpected error: {ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task ApplyFnLockSettingAsync(bool disableFnLock)
        {
            // Блокируем CheckBox на время операции
            FnLockCheckBox.IsEnabled = false;

            try
            {
                uint codeValue = disableFnLock ? 1u : 0u;

                // ИСПРАВЛЕНО: используем AppleKeyboardControlAsync, а не AppleKeyboardControl
                bool result = await AppleKeyboardControlAsync.SetFnLockModeAsync(codeValue);

                if (result)
                {
                    // Сохраняем настройку
                    APP.Properties.Settings.Default.FnLockDisabled = disableFnLock;
                    APP.Properties.Settings.Default.Save();

                    string status = disableFnLock ?
                        "Fn Lock disabled. Fn keys work as media keys." :
                        "Fn Lock enabled. Fn keys work as function keys.";
                    StatusText.Text = status;
                }
                else
                {
                    StatusText.Text = "Failed to set Fn Lock mode";
                    // Возвращаем CheckBox в прежнее состояние
                    FnLockCheckBox.Checked -= FnLockCheckBox_Checked;
                    FnLockCheckBox.Unchecked -= FnLockCheckBox_Unchecked;
                    FnLockCheckBox.IsChecked = !disableFnLock;
                    FnLockCheckBox.Checked += FnLockCheckBox_Checked;
                    FnLockCheckBox.Unchecked += FnLockCheckBox_Unchecked;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                FnLockCheckBox.IsEnabled = true;
            }
        }
    }
}