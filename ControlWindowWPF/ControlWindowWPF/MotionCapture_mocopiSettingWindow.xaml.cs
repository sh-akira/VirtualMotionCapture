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
    /// MotionCapture_mocopiSettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MotionCapture_mocopiSettingWindow : Window
    {
        private bool IsSetting = false;

        public MotionCapture_mocopiSettingWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.mocopi_GetSetting(), d =>
            {
                var data = (PipeCommands.mocopi_SetSetting)d;
                Dispatcher.Invoke(() => mocopi_SetSetting(data));
            });
        }

        private void mocopi_SetSetting(PipeCommands.mocopi_SetSetting setting)
        {
            IsSetting = true;
            UsemocopiCheckBox.IsChecked = setting.enable;
            ReceivePortTextBox.Text = setting.port.ToString();
            HeadCheckBox.IsChecked = setting.ApplyHead;
            ChestCheckBox.IsChecked = setting.ApplyChest;
            RightArmCheckBox.IsChecked = setting.ApplyRightArm;
            LeftArmCheckBox.IsChecked = setting.ApplyLeftArm;
            SpineCheckBox.IsChecked = setting.ApplySpine;
            RightHandCheckBox.IsChecked = setting.ApplyRightHand;
            LeftHandCheckBox.IsChecked = setting.ApplyLeftHand;
            RightLegCheckBox.IsChecked = setting.ApplyRightLeg;
            LeftLegCheckBox.IsChecked = setting.ApplyLeftLeg;
            RightFootCheckBox.IsChecked = setting.ApplyRightFoot;
            LeftFootCheckBox.IsChecked = setting.ApplyLeftFoot;
            RootPositionCheckBox.IsChecked = setting.ApplyRootPosition;
            RootRotationCheckBox.IsChecked = setting.ApplyRootRotation;
            IsSetting = false;
        }

        private async void OnCheckChanged(object sender, RoutedEventArgs e)
        {
            await ApplySetting();
        }

        private async void PortApplyButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplySetting();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async Task ApplySetting()
        {
            if (IsSetting) return;

            var port = TextBoxTryParse(ReceivePortTextBox);
            if (port.HasValue)
            {
                await Globals.Client?.SendCommandAsync(new PipeCommands.mocopi_SetSetting
                {
                    enable = UsemocopiCheckBox.IsChecked.Value,
                    port = port.Value,
                    ApplyHead = HeadCheckBox.IsChecked.Value,
                    ApplyChest = ChestCheckBox.IsChecked.Value,
                    ApplyRightArm = RightArmCheckBox.IsChecked.Value,
                    ApplyLeftArm = LeftArmCheckBox.IsChecked.Value,
                    ApplySpine = SpineCheckBox.IsChecked.Value,
                    ApplyRightHand = RightHandCheckBox.IsChecked.Value,
                    ApplyLeftHand = LeftHandCheckBox.IsChecked.Value,
                    ApplyRightLeg = RightLegCheckBox.IsChecked.Value,
                    ApplyLeftLeg = LeftLegCheckBox.IsChecked.Value,
                    ApplyRightFoot = RightFootCheckBox.IsChecked.Value,
                    ApplyLeftFoot = LeftFootCheckBox.IsChecked.Value,
                    ApplyRootPosition = RootPositionCheckBox.IsChecked.Value,
                    ApplyRootRotation = RootRotationCheckBox.IsChecked.Value,
                });
            }
            else
            {
                MessageBox.Show("Error: Port", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private int? TextBoxTryParse(TextBox textBox)
        {
            textBox.Background = new SolidColorBrush(Colors.White);
            if (int.TryParse(textBox.Text, out int value))
            {
                return value;
            }
            else
            {
                textBox.Background = new SolidColorBrush(Colors.Pink);
                return null;
            }
        }

        private async void ResetCenterButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandAsync(new PipeCommands.mocopi_Recenter { });
        }

        private async void mocopiOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            IsSetting = true;
            HeadCheckBox.IsChecked = true;
            ChestCheckBox.IsChecked = true;
            RightArmCheckBox.IsChecked = true;
            LeftArmCheckBox.IsChecked = true;
            SpineCheckBox.IsChecked = true;
            RightHandCheckBox.IsChecked = true;
            LeftHandCheckBox.IsChecked = true;
            RightLegCheckBox.IsChecked = true;
            LeftLegCheckBox.IsChecked = true;
            RightFootCheckBox.IsChecked = true;
            LeftFootCheckBox.IsChecked = true;
            RootPositionCheckBox.IsChecked = true;
            RootRotationCheckBox.IsChecked = true;
            IsSetting = false;
            await ApplySetting();
        }

        private async void WithVRDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            IsSetting = true;
            HeadCheckBox.IsChecked = false;
            ChestCheckBox.IsChecked = true;
            RightArmCheckBox.IsChecked = false;
            LeftArmCheckBox.IsChecked = false;
            SpineCheckBox.IsChecked = true;
            RightHandCheckBox.IsChecked = false;
            LeftHandCheckBox.IsChecked = false;
            RightLegCheckBox.IsChecked = true;
            LeftLegCheckBox.IsChecked = true;
            RightFootCheckBox.IsChecked = true;
            LeftFootCheckBox.IsChecked = true;
            RootPositionCheckBox.IsChecked = true;
            RootRotationCheckBox.IsChecked = true;
            IsSetting = false;
            await ApplySetting();
        }
    }
}
