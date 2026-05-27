using System;
using System.Windows;
using System.Windows.Media;

namespace MagicKeyboardMonitor
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow()
        {
            InitializeComponent();
            PositionAtBottomRight();
        }

        // 精準計算右下角位置
        private void PositionAtBottomRight()
        {
            var workArea = SystemParameters.WorkArea;
            // 扣除掉陰影的 Margin，稍微往上往左移一點
            this.Left = workArea.Right - this.Width;
            this.Top = workArea.Bottom - this.Height;
        }

        // 供外部呼叫更新電量的方法
        public void UpdateBattery(int batteryLevel)
        {
            // 使用 WPF 專屬的 BrushConverter 來轉換色碼
            var brushConverter = new System.Windows.Media.BrushConverter();

            if (batteryLevel >= 0)
            {
                BatteryLevelText.Text = $"{batteryLevel}%";

                // 電量大於 20% 顯示綠色 (#4ADE80)，否則顯示紅色 (#F87171)
                string colorHex = batteryLevel > 20 ? "#4ADE80" : "#F87171";
                BatteryLevelText.Foreground = (System.Windows.Media.Brush)brushConverter.ConvertFromString(colorHex)!;
            }
            else
            {
                BatteryLevelText.Text = "Disconnected";
                BatteryLevelText.Foreground = (System.Windows.Media.Brush)brushConverter.ConvertFromString("#F87171")!;
            }
        }

        // 當視窗失去焦點（使用者點了其他地方）時，自動隱藏視窗
        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Hide();
        }
    }
}