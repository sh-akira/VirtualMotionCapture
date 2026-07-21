using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UnityMemoryMappedFile;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// MotionPlaybackWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MotionPlaybackWindow : Window
    {
        public class MotionItem
        {
            public string Name { get; set; }
            public string LengthStr { get; set; }
            public string FpsStr { get; set; }
            public string FormatStr { get; set; }
            public string FilePath { get; set; }
            public float Length { get; set; }
            public float FrameRate { get; set; }
            public int FrameCount { get; set; }

            public static MotionItem Create(MotionFileInfo info)
            {
                return new MotionItem
                {
                    Name = info.Name,
                    LengthStr = info.Length.ToString("0.00") + "s",
                    FpsStr = info.FrameRate.ToString("0.##"),
                    FormatStr = info.IsVrma ? "VRMA" : "BVH",
                    FilePath = info.FilePath,
                    Length = info.Length,
                    FrameRate = info.FrameRate,
                    FrameCount = info.FrameCount,
                };
            }
        }

        public ObservableCollection<MotionItem> MotionItems { get; } = new ObservableCollection<MotionItem>();

        private bool IsSetting = false;
        private bool IsSliderSetting = false;
        private PipeCommands.Motion_SetSetting CurrentSetting = null;
        private int playingIndex = -1;

        public MotionPlaybackWindow()
        {
            InitializeComponent();
            MotionsDataGrid.ItemsSource = MotionItems;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Globals.Client.ReceivedEvent += Client_Received;

            await Globals.Client.SendCommandWaitAsync(new PipeCommands.Motion_GetSetting(), d =>
            {
                var setting = (PipeCommands.Motion_SetSetting)d;
                Dispatcher.Invoke(() => ApplySettingToUI(setting));
            });

            await Globals.Client.SendCommandWaitAsync(new PipeCommands.Motion_GetFileList(), d =>
            {
                var ret = (PipeCommands.Motion_ReturnFileList)d;
                Dispatcher.Invoke(() =>
                {
                    MotionItems.Clear();
                    if (ret.Files != null)
                    {
                        foreach (var info in ret.Files)
                        {
                            MotionItems.Add(MotionItem.Create(info));
                        }
                    }
                });
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
        }

        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.Motion_PlaybackStatus))
            {
                var d = (PipeCommands.Motion_PlaybackStatus)e.Data;
                Dispatcher.Invoke(() =>
                {
                    playingIndex = d.Index;
                    IsSliderSetting = true;
                    SeekSlider.Maximum = d.Length > 0 ? d.Length : 1;
                    SeekSlider.Value = Math.Min(d.Time, SeekSlider.Maximum);
                    IsSliderSetting = false;
                    TimeTextBlock.Text = $"{d.Time:0.00} / {d.Length:0.00}";
                    if (d.State == 1 && d.Index >= 0 && d.Index < MotionItems.Count && MotionsDataGrid.SelectedIndex != d.Index)
                    {
                        MotionsDataGrid.SelectedIndex = d.Index;
                    }
                });
            }
        }

        private void ApplySettingToUI(PipeCommands.Motion_SetSetting setting)
        {
            CurrentSetting = setting;
            IsSetting = true;
            RepeatOneShotRadioButton.IsChecked = setting.RepeatMode == 0;
            RepeatOneFileRadioButton.IsChecked = setting.RepeatMode == 1;
            RepeatListRadioButton.IsChecked = setting.RepeatMode == 2;
            RootPositionCheckBox.IsChecked = setting.ApplyRootPosition;
            RootRotationCheckBox.IsChecked = setting.ApplyRootRotation;
            SpineCheckBox.IsChecked = setting.ApplySpine;
            ChestCheckBox.IsChecked = setting.ApplyChest;
            HeadCheckBox.IsChecked = setting.ApplyHead;
            LeftArmCheckBox.IsChecked = setting.ApplyLeftArm;
            RightArmCheckBox.IsChecked = setting.ApplyRightArm;
            LeftHandCheckBox.IsChecked = setting.ApplyLeftHand;
            RightHandCheckBox.IsChecked = setting.ApplyRightHand;
            LeftLegCheckBox.IsChecked = setting.ApplyLeftLeg;
            RightLegCheckBox.IsChecked = setting.ApplyRightLeg;
            LeftFootCheckBox.IsChecked = setting.ApplyLeftFoot;
            RightFootCheckBox.IsChecked = setting.ApplyRightFoot;
            LeftFingerCheckBox.IsChecked = setting.ApplyLeftFinger;
            RightFingerCheckBox.IsChecked = setting.ApplyRightFinger;
            EyeCheckBox.IsChecked = setting.ApplyEye;
            IsSetting = false;
        }

        private async void ApplySetting()
        {
            if (IsSetting) return;
            if (CurrentSetting == null) return;

            CurrentSetting.RepeatMode = RepeatOneFileRadioButton.IsChecked == true ? 1 : RepeatListRadioButton.IsChecked == true ? 2 : 0;
            CurrentSetting.ApplyRootPosition = RootPositionCheckBox.IsChecked == true;
            CurrentSetting.ApplyRootRotation = RootRotationCheckBox.IsChecked == true;
            CurrentSetting.ApplySpine = SpineCheckBox.IsChecked == true;
            CurrentSetting.ApplyChest = ChestCheckBox.IsChecked == true;
            CurrentSetting.ApplyHead = HeadCheckBox.IsChecked == true;
            CurrentSetting.ApplyLeftArm = LeftArmCheckBox.IsChecked == true;
            CurrentSetting.ApplyRightArm = RightArmCheckBox.IsChecked == true;
            CurrentSetting.ApplyLeftHand = LeftHandCheckBox.IsChecked == true;
            CurrentSetting.ApplyRightHand = RightHandCheckBox.IsChecked == true;
            CurrentSetting.ApplyLeftLeg = LeftLegCheckBox.IsChecked == true;
            CurrentSetting.ApplyRightLeg = RightLegCheckBox.IsChecked == true;
            CurrentSetting.ApplyLeftFoot = LeftFootCheckBox.IsChecked == true;
            CurrentSetting.ApplyRightFoot = RightFootCheckBox.IsChecked == true;
            CurrentSetting.ApplyLeftFinger = LeftFingerCheckBox.IsChecked == true;
            CurrentSetting.ApplyRightFinger = RightFingerCheckBox.IsChecked == true;
            CurrentSetting.ApplyEye = EyeCheckBox.IsChecked == true;

            await Globals.Client.SendCommandAsync(CurrentSetting);
        }

        private void OnCheckChanged(object sender, RoutedEventArgs e) => ApplySetting();
        private void OnRepeatChanged(object sender, RoutedEventArgs e) => ApplySetting();

        private async void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            Globals.LoadCommonSettings();

            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Motion File(*.vrma;*.bvh)|*.vrma;*.bvh|VRM Animation(*.vrma)|*.vrma|BVH File(*.bvh)|*.bvh";
            ofd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnMotionFileDialog);

            if (ofd.ShowDialog() == true)
            {
                await Globals.Client.SendCommandWaitAsync(new PipeCommands.Motion_LoadFile { Path = ofd.FileName }, d =>
                {
                    var ret = (PipeCommands.Motion_ReturnLoadFile)d;
                    Dispatcher.Invoke(() =>
                    {
                        if (ret.Success)
                        {
                            if (MotionItems.Any(item => item.FilePath == ret.Info.FilePath) == false)
                            {
                                MotionItems.Add(MotionItem.Create(ret.Info));
                            }
                        }
                        else
                        {
                            MessageBox.Show(this, ret.Error, LanguageSelector.Get("MotionPlaybackWindow_LoadError"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    });
                });
                if (Globals.CurrentCommonSettingsWPF.CurrentPathOnMotionFileDialog != System.IO.Path.GetDirectoryName(ofd.FileName))
                {
                    Globals.CurrentCommonSettingsWPF.CurrentPathOnMotionFileDialog = System.IO.Path.GetDirectoryName(ofd.FileName);
                    Globals.SaveCommonSettings();
                }
            }
        }

        private async void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            var index = MotionsDataGrid.SelectedIndex;
            if (index < 0 || index >= MotionItems.Count) return;
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_RemoveFile { Index = index });
            MotionItems.RemoveAt(index);
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            var index = MotionsDataGrid.SelectedIndex;
            if (index < 0)
            {
                if (playingIndex >= 0)
                {
                    index = playingIndex; //一時停止からの再開
                }
                else if (MotionItems.Count > 0)
                {
                    index = 0;
                }
                else
                {
                    return;
                }
            }
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_Play { Index = index });
        }

        private async void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_Pause());
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_Stop());
        }

        private async void PrevFrameButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_FrameStep { Delta = -1 });
        }

        private async void NextFrameButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_FrameStep { Delta = 1 });
        }

        private async void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsSliderSetting) return;
            await Globals.Client.SendCommandAsync(new PipeCommands.Motion_Seek { Seconds = (float)SeekSlider.Value });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
