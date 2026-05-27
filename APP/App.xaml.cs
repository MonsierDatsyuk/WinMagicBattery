using System;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using System.Threading.Tasks;

namespace MagicKeyboardMonitor
{
    public partial class App : Application
    {
        private NotifyIcon? _notifyIcon;
        private BatteryMonitor? _batteryMonitor;
        private DashboardWindow? _dashboardWindow;
        private int _currentBatteryLevel = -1;
        private Icon? _appIcon;

        // Свойство для Fn Lock режима (загружаем из настроек)
        private bool IsFnLockDisabled
        {
            get => APP.Properties.Settings.Default.FnLockDisabled;
            set
            {
                APP.Properties.Settings.Default.FnLockDisabled = value;
                APP.Properties.Settings.Default.Save();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Инициализируем иконку в трее
            InitializeTrayIcon();

            // Загружаем сохраненные настройки
            int savedPid = APP.Properties.Settings.Default.TargetPid;

            // Применяем сохраненный режим Fn Lock (если устройство доступно)
            ApplySavedFnLockMode();

            // Запускаем мониторинг батареи, если PID сохранен
            if (savedPid != 0)
            {
                StartBatteryMonitor(savedPid);
            }
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon();

            try
            {
                var uri = new Uri("pack://application:,,,/logo/no-back-logo.ico");
                var resourceStream = Application.GetResourceStream(uri);
                if (resourceStream?.Stream != null)
                {
                    _appIcon = new Icon(resourceStream.Stream);
                    _notifyIcon.Icon = _appIcon;
                }
                else
                {
                    // Запасная иконка, если файл не найден
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }
            catch
            {
                // Запасная иконка при ошибке
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Magic Keyboard Monitor (Waiting...)";

            // Создаем контекстное меню
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Settings", null, OnSettingsClicked);
            contextMenu.Items.Add("Toggle Fn Lock", null, OnToggleFnLockClicked);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, OnExitClicked);
            _notifyIcon.ContextMenuStrip = contextMenu;

            // Обработчик клика по иконке
            _notifyIcon.MouseClick += OnTrayIconClicked;
        }

        private void ApplySavedFnLockMode()
        {
            try
            {
                // Проверяем, доступно ли устройство
                if (AppleKeyboardControl.IsDeviceAvailable())
                {
                    uint codeValue = IsFnLockDisabled ? 1u : 0u;
                    AppleKeyboardControl.SetFnLockMode(codeValue);

                    // Обновляем текст в меню
                    UpdateFnLockMenuItemText();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply Fn Lock mode: {ex.Message}");
            }
        }

        private void UpdateFnLockMenuItemText()
        {
            Dispatcher.Invoke(() =>
            {
                if (_notifyIcon?.ContextMenuStrip?.Items.Count > 0)
                {
                    // Находим пункт меню Toggle Fn Lock
                    for (int i = 0; i < _notifyIcon.ContextMenuStrip.Items.Count; i++)
                    {
                        if (_notifyIcon.ContextMenuStrip.Items[i].Text == "Toggle Fn Lock")
                        {
                            // Обновляем текст в зависимости от состояния
                            _notifyIcon.ContextMenuStrip.Items[i].Text = IsFnLockDisabled
                                ? "Enable Fn Lock"
                                : "Disable Fn Lock";
                            break;
                        }
                    }
                }
            });
        }

        private void OnToggleFnLockClicked(object? sender, EventArgs e)
        {
            // Переключаем режим Fn Lock
            bool newState = !IsFnLockDisabled;
            SetFnLockModeAsync(newState);
        }

        private async void SetFnLockModeAsync(bool disableFnLock)
        {
            try
            {
                uint codeValue = disableFnLock ? 1u : 0u;

                // Пробуем установить режим
                bool success = await AppleKeyboardControlAsync.SetFnLockModeAsync(codeValue);

                if (success)
                {
                    IsFnLockDisabled = disableFnLock;
                    UpdateFnLockMenuItemText();

                    // Показываем уведомление
                    string message = disableFnLock
                        ? "Fn Lock disabled. Fn keys work as media keys."
                        : "Fn Lock enabled. Fn keys work as function keys.";

                    _notifyIcon?.ShowBalloonTip(1000, "Magic Keyboard Monitor", message, ToolTipIcon.Info);
                }
                else
                {
                    _notifyIcon?.ShowBalloonTip(1000, "Magic Keyboard Monitor",
                        "Failed to change Fn Lock mode. Make sure device is connected.",
                        ToolTipIcon.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting Fn Lock: {ex.Message}");
                _notifyIcon?.ShowBalloonTip(1000, "Magic Keyboard Monitor",
                    $"Error: {ex.Message}",
                    ToolTipIcon.Error);
            }
        }

        private void OnTrayIconClicked(object? sender, MouseEventArgs e)
        {
            // Левый клик - показать дашборд
            if (e.Button == MouseButtons.Left)
            {
                ShowDashboard();
            }
        }

        private void ShowDashboard()
        {
            if (_dashboardWindow == null)
            {
                _dashboardWindow = new DashboardWindow();

                // Подписываемся на закрытие окна
                _dashboardWindow.Closed += (s, e) => _dashboardWindow = null;
            }

            // Обновляем текущую батарею
            _dashboardWindow.UpdateBattery(_currentBatteryLevel);
            _dashboardWindow.Show();
            _dashboardWindow.Activate();
        }

        public void StartBatteryMonitor(int targetPid)
        {
            _batteryMonitor?.StopMonitoring();
            _batteryMonitor = new BatteryMonitor();
            _batteryMonitor.OnBatteryUpdated += UpdateTrayText;
            _batteryMonitor.OnDeviceLost += ShowDeviceLost;

            Task.Run(() => _batteryMonitor.StartMonitoringAsync(targetPid));

            // Применяем сохраненный режим Fn Lock при запуске мониторинга
            ApplySavedFnLockMode();
        }

        private void UpdateTrayText(int batteryLevel)
        {
            _currentBatteryLevel = batteryLevel;
            Dispatcher.Invoke(() =>
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Text = batteryLevel >= 0
                        ? $"Magic Keyboard: {batteryLevel}%"
                        : "Magic Keyboard: Connecting...";
                }

                if (_dashboardWindow != null && _dashboardWindow.IsVisible)
                {
                    _dashboardWindow.UpdateBattery(batteryLevel);
                }
            });
        }

        private void ShowDeviceLost()
        {
            _currentBatteryLevel = -1;
            Dispatcher.Invoke(() =>
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Text = "Magic Keyboard: Disconnected";
                }

                if (_dashboardWindow != null && _dashboardWindow.IsVisible)
                {
                    _dashboardWindow.UpdateBattery(-1);
                }
            });
        }

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            var settingsWindow = new MainWindow();
            settingsWindow.Show();

            // Подписываемся на закрытие окна настроек, чтобы обновить состояние Fn Lock
            settingsWindow.Closed += (s, args) =>
            {
                // После закрытия окна настроек обновляем меню
                UpdateFnLockMenuItemText();

                // Переприменяем сохраненный режим Fn Lock
                ApplySavedFnLockMode();
            };
        }

        private void OnExitClicked(object? sender, EventArgs e)
        {
            _batteryMonitor?.StopMonitoring();
            _notifyIcon?.Dispose();
            _appIcon?.Dispose();
            Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _batteryMonitor?.StopMonitoring();
            _notifyIcon?.Dispose();
            _appIcon?.Dispose();
            base.OnExit(e);
        }

        // Публичный метод для переключения Fn Lock из других частей приложения
        public void ToggleFnLock()
        {
            SetFnLockModeAsync(!IsFnLockDisabled);
        }

        // Публичный метод для получения текущего состояния Fn Lock
        public bool GetFnLockState()
        {
            return IsFnLockDisabled;
        }
    }
}