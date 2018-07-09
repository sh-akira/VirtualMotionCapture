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
    public partial class MessageBoxWindow : Form
    {
        public MessageBoxWindow()
        {
            InitializeComponent();
        }

        public static void Show(string message, string title, string okbutton = "OK")
        {
            var win = new MessageBoxWindow();
            win.Text = title;
            win.MessageLabel.Text = message;
            win.OKButton.Text = okbutton;
            win.Show();
        }

        private void OKButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
