using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using UnityNamedPipe;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// CalibrationWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class CalibrationWindow : Window
    {
        public CalibrationWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.ImportVRM { Path = Globals.CurrentVRMFilePath, ImportForCalibration = true, UseCurrentFixSetting = true });
            Globals.Client.ReceivedEvent += Client_Received;
            await Globals.Client.SendCommandAsync(new PipeCommands.StartKeyConfig { });

        }

        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.KeyDown))
            {
                var d = (PipeCommands.KeyDown)e.Data;
                if (d.Config.keyCode == (int)EVRButtonId.k_EButton_SteamVR_Trigger && d.Config.type == KeyTypes.Controller && d.Config.isTouch == false) //タッチに反応しないように(Oculus Touch)
                {
                    Dispatcher.Invoke(() =>
                    {
                        CalibrationButton_Click(null, null);
                    });
                }
            }
        }

        private async void CalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            if (CalibrationButton.IsEnabled == false) return; //何度も実行しないように
            CalibrationButton.IsEnabled = false;
            int timercount = 5;
            do
            {
                StatusTextBlock.Text = timercount.ToString();
                await Task.Delay(1000);
            } while (timercount-- > 0);
            StatusTextBlock.Text = "取得中";
            await Globals.Client.SendCommandAsync(new PipeCommands.Calibrate());
            await Task.Delay(1000);
            StatusTextBlock.Text = "完了";
            await Task.Delay(1000);
            Close();
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
            await Globals.Client.SendCommandAsync(new PipeCommands.EndCalibrate());
            await Globals.Client.SendCommandAsync(new PipeCommands.EndKeyConfig { });
        }
    }
}
