using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ControlWindow
{
    public partial class ControlWindow : Form
    {
        public ControlWindow()
        {
            InitializeComponent();
        }

        public void ShowWindow()
        {
            this.Show();
            WindowLoader.Instance.LoadCustomBackgroundColor = LoadCustomBackgroundColor;
            WindowLoader.Instance.LoadHideBorder = LoadHideBorder;
            WindowLoader.Instance.LoadIsTopMost = LoadIsTopMost;
            WindowLoader.Instance.LoadShowCameraGrid = LoadShowCameraGrid;
            WindowLoader.Instance.LoadSetWindowClickThrough = LoadSetWindowClickThrough;
            WindowLoader.Instance.LoadLipSyncEnable = LoadLipSyncEnable;
            WindowLoader.Instance.LoadLipSyncDevice = LoadLipSyncDevice;
            WindowLoader.Instance.LoadLipSyncGain = LoadLipSyncGain;
            WindowLoader.Instance.LoadLipSyncDevice = LoadLipSyncDevice;
            WindowLoader.Instance.LoadLipSyncGain = LoadLipSyncGain;
            WindowLoader.Instance.LoadLipSyncMaxWeightEnable = LoadLipSyncMaxWeightEnable;
            WindowLoader.Instance.LoadLipSyncWeightThreashold = LoadLipSyncWeightThreashold;
            WindowLoader.Instance.LoadLipSyncMaxWeightEmphasis = LoadLipSyncMaxWeightEmphasis;
            WindowLoader.Instance.LoadAutoBlinkEnable = LoadAutoBlinkEnable;
            WindowLoader.Instance.LoadBlinkTimeMin = LoadBlinkTimeMin;
            WindowLoader.Instance.LoadBlinkTimeMax = LoadBlinkTimeMax;
            WindowLoader.Instance.LoadCloseAnimationTime = LoadCloseAnimationTime;
            WindowLoader.Instance.LoadOpenAnimationTime = LoadOpenAnimationTime;
            WindowLoader.Instance.LoadClosingTime = LoadClosingTime;
            WindowLoader.Instance.LoadDefaultFace = LoadDefaultFace;
            
            GetLipSyncDevices();
        }

        private void ImportVRMButton_Click(object sender, EventArgs e)
        {
            var win = new VRMImportWindow();
            win.Show();
        }

        private void CalibrationButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(WindowLoader.Instance.CurrentVRMFilePath))
            {
                MessageBoxWindow.Show("VRMモデルが読み込まれていません。先に読み込んでください。", "エラー");
                //return;
            }
            var win = new CalibrationWindow();
            win.Show();
        }

        private void ColorGreenButton_Click(object sender, EventArgs e)
        {
            WindowLoader.Instance.ChangeBackgroundColor?.Invoke(0.0f, 1.0f, 0.0f, false);
        }

        private void ColorBlueButton_Click(object sender, EventArgs e)
        {

            WindowLoader.Instance.ChangeBackgroundColor?.Invoke(0.0f, 0.0f, 1.0f, false);
        }

        private void ColorWhiteButton_Click(object sender, EventArgs e)
        {
            WindowLoader.Instance.ChangeBackgroundColor?.Invoke(0.9375f, 0.9375f, 0.9375f, false);
        }

        private Color customColor1 = Color.FromArgb(174, 212, 255);

        private void ColorCustom1Button_Click(object sender, EventArgs e)
        {
            WindowLoader.Instance.ChangeBackgroundColor?.Invoke(customColor1.R / 255f, customColor1.G / 255f, customColor1.B / 255f, true);
        }

        private void ColorCustom1Button_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var dialog = new ColorDialog();
                dialog.AllowFullOpen = true;
                dialog.AnyColor = true;
                dialog.Color = customColor1;
                dialog.FullOpen = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    customColor1 = dialog.Color;
                    ColorCustom1Button.BackColor = customColor1;
                }
            }
        }

        private void LoadCustomBackgroundColor(float r, float g, float b)
        {
            customColor1 = Color.FromArgb((int)(r * 255f), (int)(g * 255f), (int)(b * 255f));
            ColorCustom1Button.BackColor = customColor1;
        }

        private void ColorTransparentButton_Click(object sender, EventArgs e)
        {
            if (WindowBorderCheckBox.Checked == false) WindowBorderCheckBox.Checked = true;
            WindowLoader.Instance.SetBackgroundTransparent();
        }

        private void WindowBorderCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            WindowLoader.Instance.SetWindowBorder?.Invoke(WindowBorderCheckBox.Checked);
        }

        private void TopMostCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            WindowLoader.Instance.SetWindowTopMost?.Invoke(TopMostCheckBox.Checked);
        }

        private void SilentChangeChecked(CheckBox checkBox, bool enable, EventHandler handler)
        {
            checkBox.CheckedChanged -= handler;
            checkBox.Checked = enable;
            checkBox.CheckedChanged += handler;
        }

        private void LoadScrollBar(float setvalue, float multiply, ScrollBar scrollbar)
        {
            int min = scrollbar.Minimum;
            int max = scrollbar.Maximum;
            int value = (int)Math.Round(setvalue * multiply);
            if (value < min) value = min;
            if (value > max) value = max;
            scrollbar.Value = value;
        }

        void ScrollBarValueChanged(ScrollBar scrollbar, Label label, float multiple, Action<float> action)
        {
            float value = scrollbar.Value / multiple;
            label.Text = value.ToString("#." + multiple.ToString().Substring(1));
            action?.Invoke(value);
        }

        private void LoadHideBorder(bool enable)
        {
            SilentChangeChecked(WindowBorderCheckBox, enable, WindowBorderCheckBox_CheckedChanged);
        }

        private void LoadIsTopMost(bool enable)
        {
            SilentChangeChecked(TopMostCheckBox, enable, TopMostCheckBox_CheckedChanged);
        }

        private void FrontCameraButton_Click(object sender, EventArgs e)
        {
            WindowLoader.Instance.ChangeCamera?.Invoke(CameraTypes.Front);
        }

        private void BackCameraButton_Click(object sender, EventArgs e)
        {
            WindowLoader.Instance.ChangeCamera?.Invoke(CameraTypes.Back);
        }

        private void FreeCameraButton_Click(object sender, EventArgs e)
        {
            WindowLoader.Instance.ChangeCamera?.Invoke(CameraTypes.Free);
        }

        private void LoadSettingsButton_Click(object sender, EventArgs e)
        {
            WindowLoader.Instance.LoadSettings?.Invoke();
        }

        private void SaveSettingsButton_Click(object sender, EventArgs e)
        {
            WindowLoader.Instance.SaveSettings?.Invoke();
        }

        private void CameraGridCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            WindowLoader.Instance.SetGridVisible?.Invoke(CameraGridCheckBox.Checked);
        }

        void LoadShowCameraGrid(bool enable)
        {
            SilentChangeChecked(CameraGridCheckBox, enable, CameraGridCheckBox_CheckedChanged);
        }

        private void WindowClickThroughCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            WindowLoader.Instance.SetWindowClickThrough?.Invoke(WindowClickThroughCheckBox.Checked);
            if (TopMostCheckBox.Checked == false) TopMostCheckBox.Checked = true;
        }

        void LoadSetWindowClickThrough(bool enable)
        {
            SilentChangeChecked(WindowClickThroughCheckBox, enable, WindowClickThroughCheckBox_CheckedChanged);
        }

        private void GetLipSyncDevices()
        {
            LipSyncDeviceComboBox.SelectedIndexChanged -= LipSyncDeviceComboBox_SelectedIndexChanged;
            var devices = WindowLoader.Instance.GetLipSyncDevices?.Invoke();
            var selectedItem = LipSyncDeviceComboBox.SelectedItem;
            LipSyncDeviceComboBox.Items.Clear();
            if (devices != null)
            {
                LipSyncDeviceComboBox.Items.AddRange(devices);
                if (selectedItem != null) LoadLipSyncDevice(selectedItem.ToString());
            }
            LipSyncDeviceComboBox.SelectedIndexChanged += LipSyncDeviceComboBox_SelectedIndexChanged;
        }

        private void LipSyncCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            WindowLoader.Instance.SetLipSyncEnable?.Invoke(LipSyncCheckBox.Checked);
        }

        void LoadLipSyncEnable(bool enable)
        {
            SilentChangeChecked(LipSyncCheckBox, enable, LipSyncCheckBox_CheckedChanged);
        }

        private void LipSyncDeviceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (LipSyncDeviceComboBox.SelectedItem == null) return;
            if (LipSyncDeviceComboBox.SelectedItem.ToString().StartsWith("エラー:")) return;
            WindowLoader.Instance.SetLipSyncDevice?.Invoke(LipSyncDeviceComboBox.SelectedItem.ToString());
        }

        void LoadLipSyncDevice(string device)
        {
            if (string.IsNullOrEmpty(device)) return;
            if (LipSyncDeviceComboBox.Items.Contains(device))
            {
                LipSyncDeviceComboBox.SelectedItem = device;
            }
            else
            {
                LipSyncDeviceComboBox.Items.Insert(0, device.StartsWith("エラー:") ? device : "エラー:" + device);
            }
        }

        private void GainScrollBar_ValueChanged(object sender, EventArgs e)
        {
            ScrollBarValueChanged(GainScrollBar, GainLabel, 10.0f, WindowLoader.Instance.SetLipSyncGain);
        }

        void LoadLipSyncGain(float gain)
        {
            LoadScrollBar(gain, 10.0f, GainScrollBar);
        }

        private void LipSyncDeviceRefreshButton_Click(object sender, EventArgs e)
        {
            GetLipSyncDevices();
        }

        private void MaxWeightCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            WindowLoader.Instance.SetLipSyncMaxWeightEnable?.Invoke(MaxWeightCheckBox.Checked);
        }

        void LoadLipSyncMaxWeightEnable(bool enable)
        {
            SilentChangeChecked(MaxWeightCheckBox, enable, MaxWeightCheckBox_CheckedChanged);
        }

        private void WeightThreasholdScrollBar_ValueChanged(object sender, EventArgs e)
        {
            ScrollBarValueChanged(WeightThreasholdScrollBar, WeightThreasholdLabel, 1000.0f, WindowLoader.Instance.SetLipSyncWeightThreashold);
        }

        void LoadLipSyncWeightThreashold(float threashold)
        {
            LoadScrollBar(threashold, 1000.0f, WeightThreasholdScrollBar);
        }

        private void MaxWeightEmphasisCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            WindowLoader.Instance.SetLipSyncMaxWeightEmphasis?.Invoke(MaxWeightEmphasisCheckBox.Checked);
        }

        void LoadLipSyncMaxWeightEmphasis(bool enable)
        {
            SilentChangeChecked(MaxWeightEmphasisCheckBox, enable, MaxWeightEmphasisCheckBox_CheckedChanged);
        }

        private void AutoBlinkCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            WindowLoader.Instance.SetAutoBlinkEnable?.Invoke(AutoBlinkCheckBox.Checked);
        }

        void LoadAutoBlinkEnable(bool enable)
        {
            SilentChangeChecked(AutoBlinkCheckBox, enable, AutoBlinkCheckBox_CheckedChanged);
        }

        private void BlinkTimeMinScrollBar_ValueChanged(object sender, EventArgs e)
        {
            ScrollBarValueChanged(BlinkTimeMinScrollBar, BlinkTimeMinLabel, 10.0f, WindowLoader.Instance.SetBlinkTimeMin);
        }

        void LoadBlinkTimeMin(float time)
        {
            LoadScrollBar(time, 10.0f, BlinkTimeMinScrollBar);
        }

        private void BlinkTimeMaxScrollBar_ValueChanged(object sender, EventArgs e)
        {
            ScrollBarValueChanged(BlinkTimeMaxScrollBar, BlinkTimeMaxLabel, 10.0f, WindowLoader.Instance.SetBlinkTimeMax);
        }

        void LoadBlinkTimeMax(float time)
        {
            LoadScrollBar(time, 10.0f, BlinkTimeMaxScrollBar);
        }

        private void CloseAnimationTimeScrollBar_ValueChanged(object sender, EventArgs e)
        {
            ScrollBarValueChanged(CloseAnimationTimeScrollBar, CloseAnimationTimeLabel, 100.0f, WindowLoader.Instance.SetCloseAnimationTime);
        }

        void LoadCloseAnimationTime(float time)
        {
            LoadScrollBar(time, 100.0f, CloseAnimationTimeScrollBar);
        }

        private void OpenAnimationTimeScrollBar_ValueChanged(object sender, EventArgs e)
        {
            ScrollBarValueChanged(OpenAnimationTimeScrollBar, OpenAnimationTimeLabel, 100.0f, WindowLoader.Instance.SetOpenAnimationTime);
        }

        void LoadOpenAnimationTime(float time)
        {
            LoadScrollBar(time, 100.0f, OpenAnimationTimeScrollBar);
        }

        private void ClosingTimeScrollBar_ValueChanged(object sender, EventArgs e)
        {
            ScrollBarValueChanged(ClosingTimeScrollBar, ClosingTimeLabel, 100.0f, WindowLoader.Instance.SetClosingTime);
        }

        void LoadClosingTime(float time)
        {
            LoadScrollBar(time, 100.0f, ClosingTimeScrollBar);
        }

        private void DefaultFaceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DefaultFaceComboBox.SelectedItem == null) return;
            WindowLoader.Instance.SetDefaultFace?.Invoke(DefaultFaceComboBox.SelectedItem.ToString());
        }

        void LoadDefaultFace(string face)
        {
            if (string.IsNullOrEmpty(face)) return;
            if (DefaultFaceComboBox.Items.Contains(face))
            {
                DefaultFaceComboBox.SelectedItem = face;
            }
            else
            {
                DefaultFaceComboBox.Items.Insert(0, face);
            }
        }
    }
}
