using System;
using System.Collections.Generic;
using System.IO;
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
using UnityMemoryMappedFile;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// PhotoWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class PhotoWindow : Window
    {
        private string defaultPath = Path.Combine(Directory.GetParent(Globals.GetCurrentAppDir().Substring(0, Globals.GetCurrentAppDir().Length - 1)).FullName, "Photos");
        public PhotoWindow()
        {
            InitializeComponent();
            var savedPath = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnPhotoFileDialog);
            PathTextBox.Text = savedPath ?? defaultPath;
            if (Directory.Exists(PathTextBox.Text) == false)
            {
                PathTextBox.Text = defaultPath;
                Directory.CreateDirectory(defaultPath);
            }
        }

        private bool cancelShot = false;
        private bool continuous = false;
        private Brush oldBrush = null;

        private async void TakePhoto()
        {
            if (ContinuousCheckBox.IsChecked == true)
            {
                if (continuous)
                {
                    continuous = false;
                    TakePhotoButton.Background = oldBrush;
                    ContinuousCheckBox.IsEnabled = true;
                    cancelShot = true;
                }
                else
                {
                    continuous = true;
                    oldBrush = TakePhotoButton.Background;
                    TakePhotoButton.Background = new SolidColorBrush(Colors.LightPink);
                    ContinuousCheckBox.IsEnabled = false;
                    while(cancelShot == false)
                    {
                        await TakePhotoOneShot();
                    }
                    cancelShot = false;
                    TimerTextBlock.Text = "";
                }
            }
            else
            {
                TakePhotoButton.IsEnabled = false;
                await TakePhotoOneShot();
                TakePhotoButton.IsEnabled = true;
            }
        }

        private async Task TakePhotoOneShot()
        {
            if (int.TryParse(TimerSecondsTextBox.Text, out var timerSeconds) == false)
            {
                MessageBox.Show(LanguageSelector.Get("PhotoWindow_ErrorTimer"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (int.TryParse(ResolutionWidthTextBox.Text, out var width) == false || width <= 0)
            {
                MessageBox.Show(LanguageSelector.Get("PhotoWindow_ErrorWidth"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (Directory.Exists(PathTextBox.Text) == false)
            {
                MessageBox.Show(LanguageSelector.Get("PhotoWindow_ErrorFolderExist"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            while (timerSeconds > 0)
            {
                TimerTextBlock.Text = timerSeconds.ToString();
                await Task.Delay(1000);
                timerSeconds--;
                if (cancelShot) break;
            }
            if (cancelShot == false)
            {
                await Globals.Client.SendCommandAsync(new PipeCommands.TakePhoto { Width = width, TransparentBackground = TransparentCheckBox.IsChecked == true, Directory = PathTextBox.Text });
            }
            TimerTextBlock.Text = "";
        }

        private void TakePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            TakePhoto();
        }

        private void PathSelectButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();

            dlg.Description = LanguageSelector.Get("PhotoWindow_FolderSelect");
            if (string.IsNullOrWhiteSpace(PathTextBox.Text) == false && Directory.Exists(PathTextBox.Text))
            {
                dlg.SelectedPath = PathTextBox.Text;
            }
            else
            {
                dlg.SelectedPath = defaultPath;
            }

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PathTextBox.Text = dlg.SelectedPath;

                if (Globals.CurrentCommonSettingsWPF.CurrentPathOnPhotoFileDialog != PathTextBox.Text)
                {
                    Globals.CurrentCommonSettingsWPF.CurrentPathOnPhotoFileDialog = PathTextBox.Text;
                    Globals.SaveCommonSettings();
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.StartKeySend { });
            Globals.Client.ReceivedEvent += Client_Received;
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
                        TakePhoto();
                    });
                }
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
            await Globals.Client.SendCommandAsync(new PipeCommands.EndKeySend { });
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(PathTextBox.Text) == false)
            {
                MessageBox.Show(LanguageSelector.Get("PhotoWindow_ErrorFolderExist"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            System.Diagnostics.Process.Start(PathTextBox.Text);
        }
    }
}
