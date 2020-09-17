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
    /// GraphicsOptionWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class GraphicsOptionWindow : Window
    {
        PipeCommands.SetPostProcessing postProcessingValue = new PipeCommands.SetPostProcessing();

        public GraphicsOptionWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Globals.Client.ReceivedEvent += Client_Received;

            await Globals.Client.SendCommandAsync(new PipeCommands.GetPostProcessing { });
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
        }

        private async void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.SetPostProcessing))
            {
                var d = (PipeCommands.SetPostProcessing)e.Data;
                postProcessingValue = d;
                ValueApply();
            }
        }

        private async void ValueChanged()
        {
            //セットされたpostProcessingValueを送信する
            await Globals.Client.SendCommandAsync(postProcessingValue);
        }
        private async void ValueApply()
        {
            //postProcessingValueを画面に反映する
        }

    }
}
