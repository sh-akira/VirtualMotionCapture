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
using VirtualMotionCaptureControlPanel.Properties;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// MotionCapture_mocopiSettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class VMCProtocolReceiverSettingWindow : Window
    {
        private bool IsSetting = false;

        public int Index = -1;
        public string ReceiverName;
        public int Port;

        public VMCProtocolReceiverSettingWindow(int index)
        {
            InitializeComponent();
            Index = index;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetVMCProtocolReceiverSetting { Index = Index }, d =>
            {
                var data = (PipeCommands.SetVMCProtocolReceiverSetting)d;
                Dispatcher.Invoke(() => SetSetting(data));
            });
        }

        private void SetSetting(PipeCommands.SetVMCProtocolReceiverSetting setting)
        {
            IsSetting = true;
            Index = setting.Index;
            IndexTextBlock.Text = Index.ToString();
            EnableCheckBox.IsChecked = setting.Enable;
            ReceivePortTextBox.Text = setting.Port.ToString();
            Port = setting.Port;
            CustomNameTextBox.Text = setting.Name;
            ReceiverName = setting.Name;

            EyeCheckBox.IsChecked = setting.ApplyEye;
            HeadCheckBox.IsChecked = setting.ApplyHead;
            ChestCheckBox.IsChecked = setting.ApplyChest;
            RightArmCheckBox.IsChecked = setting.ApplyRightArm;
            LeftArmCheckBox.IsChecked = setting.ApplyLeftArm;
            SpineCheckBox.IsChecked = setting.ApplySpine;
            RightHandCheckBox.IsChecked = setting.ApplyRightHand;
            LeftHandCheckBox.IsChecked = setting.ApplyLeftHand;
            RightFingerCheckBox.IsChecked = setting.ApplyRightFinger;
            LeftFingerCheckBox.IsChecked = setting.ApplyLeftFinger;
            RightLegCheckBox.IsChecked = setting.ApplyRightLeg;
            LeftLegCheckBox.IsChecked = setting.ApplyLeftLeg;
            RightFootCheckBox.IsChecked = setting.ApplyRightFoot;
            LeftFootCheckBox.IsChecked = setting.ApplyLeftFoot;
            RootPositionCheckBox.IsChecked = setting.ApplyRootPosition;
            RootRotationCheckBox.IsChecked = setting.ApplyRootRotation;

            DelayMsTextbox.Text = setting.DelayMs.ToString();

            FixHandBoneCheckBox.IsChecked = setting.FixHandBone;
            UseBonePositionCheckBox.IsChecked = setting.UseBonePosition;

            BlendShapeCheckBox.IsChecked = setting.ApplyBlendShape;
            LookAtCheckBox.IsChecked = setting.ApplyLookAt;
            TrackerCheckBox.IsChecked = setting.ApplyTracker;
            CameraCheckBox.IsChecked = setting.ApplyCamera;
            LightCheckBox.IsChecked = setting.ApplyLight;
            MIDICheckBox.IsChecked = setting.ApplyMidi;
            StatusCheckBox.IsChecked = setting.ApplyStatus;
            ControlCheckBox.IsChecked = setting.ApplyControl;
            SettingCheckBox.IsChecked = setting.ApplySetting;
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
            if (port.HasValue == false)
            {
                MessageBox.Show("Error: Port", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            var delayMs = TextBoxTryParse(DelayMsTextbox);
            if (delayMs.HasValue == false)
            {
                MessageBox.Show("Error: Delay", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Port = port.Value;
            ReceiverName = CustomNameTextBox.Text;

            await Globals.Client?.SendCommandAsync(new PipeCommands.SetVMCProtocolReceiverSetting
            {
                Index = Index,
                Enable = EnableCheckBox.IsChecked.Value,
                Port = port.Value,
                Name = ReceiverName,

                ApplyEye = EyeCheckBox.IsChecked.Value,
                ApplyHead = HeadCheckBox.IsChecked.Value,
                ApplyChest = ChestCheckBox.IsChecked.Value,
                ApplyRightArm = RightArmCheckBox.IsChecked.Value,
                ApplyLeftArm = LeftArmCheckBox.IsChecked.Value,
                ApplySpine = SpineCheckBox.IsChecked.Value,
                ApplyRightHand = RightHandCheckBox.IsChecked.Value,
                ApplyLeftHand = LeftHandCheckBox.IsChecked.Value,
                ApplyRightFinger = RightFingerCheckBox.IsChecked.Value,
                ApplyLeftFinger = LeftFingerCheckBox.IsChecked.Value,
                ApplyRightLeg = RightLegCheckBox.IsChecked.Value,
                ApplyLeftLeg = LeftLegCheckBox.IsChecked.Value,
                ApplyRightFoot = RightFootCheckBox.IsChecked.Value,
                ApplyLeftFoot = LeftFootCheckBox.IsChecked.Value,
                ApplyRootPosition = RootPositionCheckBox.IsChecked.Value,
                ApplyRootRotation = RootRotationCheckBox.IsChecked.Value,

                DelayMs = delayMs.Value,

                FixHandBone = FixHandBoneCheckBox.IsChecked.Value,
                UseBonePosition = UseBonePositionCheckBox.IsChecked.Value,

                ApplyBlendShape = BlendShapeCheckBox.IsChecked.Value,
                ApplyLookAt = LookAtCheckBox.IsChecked.Value,
                ApplyTracker = TrackerCheckBox.IsChecked.Value,
                ApplyCamera = CameraCheckBox.IsChecked.Value,
                ApplyLight = LightCheckBox.IsChecked.Value,
                ApplyMidi = MIDICheckBox.IsChecked.Value,
                ApplyStatus = StatusCheckBox.IsChecked.Value,
                ApplyControl = ControlCheckBox.IsChecked.Value,
                ApplySetting = SettingCheckBox.IsChecked.Value,

            });
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
            await Globals.Client?.SendCommandAsync(new PipeCommands.VMCProtocolReceiverRecenter { Index = Index });
        }

        private async void FullbodyButton_Click(object sender, RoutedEventArgs e)
        {
            IsSetting = true;
            EyeCheckBox.IsChecked = true;
            HeadCheckBox.IsChecked = true;
            ChestCheckBox.IsChecked = true;
            RightArmCheckBox.IsChecked = true;
            LeftArmCheckBox.IsChecked = true;
            SpineCheckBox.IsChecked = true;
            RightHandCheckBox.IsChecked = true;
            LeftHandCheckBox.IsChecked = true;
            RightFingerCheckBox.IsChecked = true;
            LeftFingerCheckBox.IsChecked = true;
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
            EyeCheckBox.IsChecked = true;
            HeadCheckBox.IsChecked = false;
            ChestCheckBox.IsChecked = true;
            RightArmCheckBox.IsChecked = false;
            LeftArmCheckBox.IsChecked = false;
            SpineCheckBox.IsChecked = true;
            RightHandCheckBox.IsChecked = false;
            LeftHandCheckBox.IsChecked = false;
            RightFingerCheckBox.IsChecked = true;
            LeftFingerCheckBox.IsChecked = true;
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
