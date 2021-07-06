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
    /// DebugLogWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class DebugLogWindow : Window
    {
        private bool AutoScroll = true;
        private bool ShowStackTrace = false;

        public DebugLogWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Globals.Client.ReceivedEvent += Client_Received;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetLogNotifyLevel { type = NotifyLogTypes.Log });
        }
        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.CommandType == typeof(PipeCommands.LogNotify))
                {
                    var d = (PipeCommands.LogNotify)e.Data;
                    AddLog(d);
                }
            });
        }

        private void AutoScrollCheckBox_Checked(object sender, RoutedEventArgs e) => AutoScroll = true;

        private void AutoScrollCheckBox_Unchecked(object sender, RoutedEventArgs e) => AutoScroll = false;

        private void ShowStackTraceCheckBox_Checked(object sender, RoutedEventArgs e) => ShowStackTrace = true;

        private void ShowStackTraceCheckBox_Unchecked(object sender, RoutedEventArgs e) => ShowStackTrace = false;

        private void AddLog(PipeCommands.LogNotify log)
        {
            AddLog($"[{log.type}] {log.condition}\n");
            if (ShowStackTrace && string.IsNullOrWhiteSpace(log.stackTrace) == false)
            {
                AddLog($"StackTrace:\n{log.stackTrace}");
            }
        }

        private void AddLog(string message)
        {
            logTextBox.AppendText(message);
            if (AutoScroll)
            {
                logTextBox.CaretIndex = logTextBox.Text.Length;
                logTextBox.ScrollToEnd();
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetLogNotifyLevel { type = NotifyLogTypes.Warning });
            Globals.Client.ReceivedEvent -= Client_Received;
        }
    }
}
