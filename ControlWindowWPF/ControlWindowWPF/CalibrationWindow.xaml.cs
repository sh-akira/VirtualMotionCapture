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
using UnityMemoryMappedFile;

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
            await Globals.Client.SendCommandAsync(new PipeCommands.InitializeCalibration { });
            Globals.Client.ReceivedEvent += Client_Received;
            await Globals.Client.SendCommandAsync(new PipeCommands.StartKeyConfig { });

            Globals.LoadCommonSettings();
            var calibrateType = Globals.CurrentCommonSettingsWPF.LastCalibrateType;
            if (calibrateType == PipeCommands.CalibrateType.Ipose) CalibrateIposeRadioButton.IsChecked = true;
            else if (calibrateType == PipeCommands.CalibrateType.Tpose) CalibrateTposeRadioButton.IsChecked = true;
            else if (calibrateType == PipeCommands.CalibrateType.FixedHand) CalibrateFixedHandRadioButton.IsChecked = true;
            else if (calibrateType == PipeCommands.CalibrateType.FixedHandWithGround) CalibrateFixedHandWithGroundRadioButton.IsChecked = true;
            else if (calibrateType == PipeCommands.CalibrateType.Default) CalibrateDefaultRadioButton.IsChecked = true;
            CalibrationEndSoundCheckBox.IsChecked = Globals.CurrentCommonSettingsWPF.EnableCalibrationEndSound;
        }

        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.KeyDown))
            {
                var d = (PipeCommands.KeyDown)e.Data;
                if (d.Config.keyName == "ClickTrigger" && d.Config.type == KeyTypes.Controller && d.Config.isTouch == false) //タッチに反応しないように(Oculus Touch)
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
            StatusTextBlock.Text = LanguageSelector.Get("CalibrationWindow_Status_Calibrating");
            var calibrateType = SelectCalibrateType();
            Globals.CurrentCommonSettingsWPF.LastCalibrateType = calibrateType;
            await Globals.Client.SendCommandAsync(new PipeCommands.Calibrate { CalibrateType = calibrateType });
            await Task.Delay(1000);
            StatusTextBlock.Text = LanguageSelector.Get("CalibrationWindow_Status_Finish");
            Globals.CurrentCommonSettingsWPF.EnableCalibrationEndSound = CalibrationEndSoundCheckBox.IsChecked.Value;
            if (Globals.CurrentCommonSettingsWPF.EnableCalibrationEndSound)
            {
                System.Media.SystemSounds.Beep.Play();
            }

            Globals.SaveCommonSettings();
            await Task.Delay(1000);
            Close();
        }

        private PipeCommands.CalibrateType SelectCalibrateType() =>
            CalibrateDefaultRadioButton?.IsChecked == true ? PipeCommands.CalibrateType.Default : (
            CalibrateFixedHandRadioButton?.IsChecked == true ? PipeCommands.CalibrateType.FixedHand : (
            CalibrateFixedHandWithGroundRadioButton?.IsChecked == true ? PipeCommands.CalibrateType.FixedHandWithGround : (
            CalibrateTposeRadioButton?.IsChecked == true ? PipeCommands.CalibrateType.Tpose : PipeCommands.CalibrateType.Ipose)));

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
            await Globals.Client.SendCommandAsync(new PipeCommands.EndCalibrate());
            await Globals.Client.SendCommandAsync(new PipeCommands.EndKeyConfig { });
        }

        private async void CalibrateRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SelectCalibrateMode { CalibrateType = SelectCalibrateType() });
        }
    }
}
