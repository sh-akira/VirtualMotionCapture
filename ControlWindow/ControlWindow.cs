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
    }
}
