using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UnityMemoryMappedFile;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// MotionRecordWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MotionRecordWindow : Window
    {
        private bool IsSetting = false;
        private bool IsSliderSetting = false;
        private PipeCommands.Motion_SetRecordSetting CurrentSetting = null;
        private int recordState = 0; // 0:停止 1:カウントダウン 2:記録中 3:記録済み
        private int frameCount = 0;
        private float recordedFps = 30f;

        public MotionRecordWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Globals.Client.ReceivedEvent += Client_Received;

            await Globals.Client.SendCommandWaitAsync(new PipeCommands.Motion_GetSetting(), d =>
            {
                var setting = (PipeCommands.Motion_SetSetting)d;
                Dispatcher.Invoke(() => ApplySettingToUI(setting));
            });
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_PreviewStop());
        }

        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.Motion_RecordingStatus))
            {
                var d = (PipeCommands.Motion_RecordingStatus)e.Data;
                Dispatcher.Invoke(() => UpdateRecordingStatus(d));
            }
            else if (e.CommandType == typeof(PipeCommands.Motion_PreviewStatus))
            {
                var d = (PipeCommands.Motion_PreviewStatus)e.Data;
                Dispatcher.Invoke(() =>
                {
                    IsSliderSetting = true;
                    PreviewSlider.Value = Math.Min(d.Frame, PreviewSlider.Maximum);
                    IsSliderSetting = false;
                    UpdateFrameLabels();
                });
            }
        }

        private void UpdateRecordingStatus(PipeCommands.Motion_RecordingStatus d)
        {
            recordState = d.State;
            frameCount = d.FrameCount;
            recordedFps = d.Fps > 0 ? d.Fps : 30f;

            switch (d.State)
            {
                case 1: //カウントダウン中
                    RecordButton.Content = LanguageSelector.Get("MotionRecordWindow_StopRecording");
                    RecordStatusTextBlock.Text = $"{LanguageSelector.Get("MotionRecordWindow_CountdownStatus")} {Math.Ceiling(d.Countdown):0}";
                    RecordStatusTextBlock.Foreground = Brushes.OrangeRed;
                    CutEditGroupBox.IsEnabled = false;
                    break;
                case 2: //記録中
                    RecordButton.Content = LanguageSelector.Get("MotionRecordWindow_StopRecording");
                    RecordStatusTextBlock.Text = $"{LanguageSelector.Get("MotionRecordWindow_RecordingStatus")} {d.Time:0.0}s ({d.FrameCount} frames)";
                    RecordStatusTextBlock.Foreground = Brushes.Red;
                    CutEditGroupBox.IsEnabled = false;
                    break;
                case 3: //記録済み
                    RecordButton.Content = LanguageSelector.Get("MotionRecordWindow_StartRecording");
                    RecordStatusTextBlock.Text = $"{LanguageSelector.Get("MotionRecordWindow_RecordedStatus")} {d.Time:0.0}s ({d.FrameCount} frames)";
                    RecordStatusTextBlock.Foreground = Brushes.Green;
                    if (CutEditGroupBox.IsEnabled == false)
                    {
                        CutEditGroupBox.IsEnabled = true;
                        IsSliderSetting = true;
                        PreviewSlider.Maximum = Math.Max(0, d.FrameCount - 1);
                        PreviewSlider.Value = 0;
                        StartFrameSlider.Maximum = Math.Max(0, d.FrameCount - 1);
                        StartFrameSlider.Value = 0;
                        EndFrameSlider.Maximum = Math.Max(0, d.FrameCount - 1);
                        EndFrameSlider.Value = Math.Max(0, d.FrameCount - 1);
                        IsSliderSetting = false;
                        UpdateFrameLabels();
                    }
                    break;
                default: //停止
                    RecordButton.Content = LanguageSelector.Get("MotionRecordWindow_StartRecording");
                    RecordStatusTextBlock.Text = "";
                    break;
            }
        }

        private void UpdateFrameLabels()
        {
            PreviewFrameTextBlock.Text = $"{PreviewSlider.Value:0} / {Math.Max(0, frameCount - 1)}";
            StartFrameTextBlock.Text = $"{StartFrameSlider.Value:0} ({StartFrameSlider.Value / recordedFps:0.00}s)";
            EndFrameTextBlock.Text = $"{EndFrameSlider.Value:0} ({EndFrameSlider.Value / recordedFps:0.00}s)";
        }

        private void ApplySettingToUI(PipeCommands.Motion_SetSetting setting)
        {
            //記録設定のみを保持する(再生側の設定を上書きしないようMotion_SetRecordSettingで送信する)
            CurrentSetting = new PipeCommands.Motion_SetRecordSetting
            {
                RecordFps = setting.RecordFps,
                RecordCountdown = setting.RecordCountdown,
                RecordMotion = setting.RecordMotion,
                RecordExpressionPreset = setting.RecordExpressionPreset,
                RecordExpressionCustom = setting.RecordExpressionCustom,
                RecordLookAt = setting.RecordLookAt,
            };
            IsSetting = true;
            FpsTextBox.Text = setting.RecordFps.ToString();
            CountdownTextBox.Text = setting.RecordCountdown.ToString();
            SaveMotionCheckBox.IsChecked = setting.RecordMotion;
            SaveExpressionPresetCheckBox.IsChecked = setting.RecordExpressionPreset;
            SaveExpressionCustomCheckBox.IsChecked = setting.RecordExpressionCustom;
            SaveLookAtCheckBox.IsChecked = setting.RecordLookAt;
            IsSetting = false;
        }

        private async void ApplySetting()
        {
            if (IsSetting) return;
            if (CurrentSetting == null) return;

            if (int.TryParse(FpsTextBox.Text, out var fps) && fps > 0 && fps <= 240)
            {
                CurrentSetting.RecordFps = fps;
                FpsTextBox.Background = new SolidColorBrush(Colors.White);
            }
            else
            {
                FpsTextBox.Background = new SolidColorBrush(Colors.Pink);
            }
            if (int.TryParse(CountdownTextBox.Text, out var countdown) && countdown >= 0)
            {
                CurrentSetting.RecordCountdown = countdown;
                CountdownTextBox.Background = new SolidColorBrush(Colors.White);
            }
            else
            {
                CountdownTextBox.Background = new SolidColorBrush(Colors.Pink);
            }
            CurrentSetting.RecordMotion = SaveMotionCheckBox.IsChecked == true;
            CurrentSetting.RecordExpressionPreset = SaveExpressionPresetCheckBox.IsChecked == true;
            CurrentSetting.RecordExpressionCustom = SaveExpressionCustomCheckBox.IsChecked == true;
            CurrentSetting.RecordLookAt = SaveLookAtCheckBox.IsChecked == true;

            await Globals.Client.SendCommandAsync(CurrentSetting);
        }

        private void OnCheckChanged(object sender, RoutedEventArgs e) => ApplySetting();
        private void SettingTextBox_LostFocus(object sender, RoutedEventArgs e) => ApplySetting();

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (recordState == 1 || recordState == 2)
            {
                await Globals.Client.SendCommandAsync(new PipeCommands.Motion_StopRecording());
            }
            else
            {
                ApplySetting();
                await Globals.Client.SendCommandAsync(new PipeCommands.Motion_StartRecording());
            }
        }

        private async void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateFrameLabels();
            if (IsSliderSetting) return;
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_PreviewSeek { Frame = (int)PreviewSlider.Value });
        }

        private async void StartFrameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsSliderSetting) { UpdateFrameLabels(); return; }
            if (StartFrameSlider.Value > EndFrameSlider.Value)
            {
                EndFrameSlider.Value = StartFrameSlider.Value;
            }
            UpdateFrameLabels();
            //ドラッグ中の位置をプレビューして確認できるようにする
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_PreviewSeek { Frame = (int)StartFrameSlider.Value });
        }

        private async void EndFrameSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsSliderSetting) { UpdateFrameLabels(); return; }
            if (EndFrameSlider.Value < StartFrameSlider.Value)
            {
                StartFrameSlider.Value = EndFrameSlider.Value;
            }
            UpdateFrameLabels();
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_PreviewSeek { Frame = (int)EndFrameSlider.Value });
        }

        private async void PreviewPlayButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_PreviewPlay
            {
                StartFrame = (int)StartFrameSlider.Value,
                EndFrame = (int)EndFrameSlider.Value,
            });
        }

        private async void PreviewPauseButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_PreviewPause());
        }

        private async void PreviewStopButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_PreviewStop());
        }

        private void SaveVrmaButton_Click(object sender, RoutedEventArgs e)
        {
            SaveRecording(0, "VRM Animation(*.vrma)|*.vrma", ".vrma");
        }

        private void SaveBvhButton_Click(object sender, RoutedEventArgs e)
        {
            SaveRecording(1, "BVH File(*.bvh)|*.bvh", ".bvh");
        }

        private async void SaveRecording(int format, string filter, string extension)
        {
            if (recordState != 3) return;

            ApplySetting();
            Globals.LoadCommonSettings();

            var sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.Filter = filter;
            sfd.FileName = $"motion_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
            sfd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnMotionFileDialog);

            if (sfd.ShowDialog() == true)
            {
                await Globals.Client.SendCommandWaitAsync(new PipeCommands.Motion_SaveRecording
                {
                    Path = sfd.FileName,
                    Format = format,
                    StartFrame = (int)StartFrameSlider.Value,
                    EndFrame = (int)EndFrameSlider.Value,
                }, d =>
                {
                    var ret = (PipeCommands.Motion_ReturnSaveRecording)d;
                    Dispatcher.Invoke(() =>
                    {
                        if (ret.Success == false)
                        {
                            MessageBox.Show(this, ret.Error, LanguageSelector.Get("MotionRecordWindow_SaveError"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    });
                });
                if (Globals.CurrentCommonSettingsWPF.CurrentPathOnMotionFileDialog != System.IO.Path.GetDirectoryName(sfd.FileName))
                {
                    Globals.CurrentCommonSettingsWPF.CurrentPathOnMotionFileDialog = System.IO.Path.GetDirectoryName(sfd.FileName);
                    Globals.SaveCommonSettings();
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
