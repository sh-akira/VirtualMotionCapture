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
    public partial class CalibrationWindow : Form
    {
        public CalibrationWindow()
        {
            InitializeComponent();
        }

        private void CalibrationWindow_Load(object sender, EventArgs e)
        {
            WindowLoader.Instance.ImportVRM?.Invoke(WindowLoader.Instance.CurrentVRMFilePath, true);
        }

        private void CalibrationButton_Click(object sender, EventArgs e)
        {
            CalibrationButton.Enabled = false;
            timercount = 5;
            Timer_Tick();
        }

        private int timercount;
        private void Timer_Tick()
        {
            if (timercount > 0)
            {
                StatusLabel.Text = timercount.ToString();
            }
            else if (timercount == 0)
            {
                StatusLabel.Text = "取得中";
                WindowLoader.Instance.Calibrate?.Invoke();
            }
            else if (timercount == -1)
            {
                StatusLabel.Text = "完了";
            }
            else
            {
                this.Close();
                return;
            }
            timercount--;
            WindowLoader.Instance.RunAfterMs(1000, Timer_Tick);
        }

        private void CalibrationWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            WindowLoader.Instance.EndCalibrate?.Invoke();
        }
    }
}
