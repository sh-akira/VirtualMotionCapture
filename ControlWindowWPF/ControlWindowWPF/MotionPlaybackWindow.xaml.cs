using System;
using System.Collections.Generic;
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
            public string ShortcutStr { get; set; } //割り当て済みショートカットキー
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
                    UpdateShortcuts();
                });
            });
        }

        /// <summary>
        /// 各モーションに割り当て済みのショートカットキーを一覧に反映する
        /// </summary>
        private void UpdateShortcuts()
        {
            foreach (var item in MotionItems)
            {
                item.ShortcutStr = BuildShortcutStr(item.FilePath);
            }
            MotionsDataGrid.Items.Refresh();
        }

        private string BuildShortcutStr(string filePath)
        {
            if (Globals.KeyActions == null) return "";
            var parts = new List<string>();
            foreach (var ka in Globals.KeyActions)
            {
                if (ka.MotionAction == false) continue;
                if (ka.MotionPlayType == 2) continue; //解除はモーション非依存のため一覧には出さない
                if (ka.MotionFilePath != filePath) continue;
                var keys = string.Join("+", ka.KeyConfigs.Select(k => k.ToString()));
                var typ = ka.MotionPlayType == 1 ? LanguageSelector.Get("MotionKeyAddWindow_TypePose") : LanguageSelector.Get("MotionKeyAddWindow_TypePlay");
                parts.Add($"{typ}:{keys}");
            }
            return string.Join(", ", parts);
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

                    //時間に加えて現在フレーム/総フレームも表示する(ポーズ登録位置の目安)
                    var frameStr = "";
                    if (d.Index >= 0 && d.Index < MotionItems.Count && MotionItems[d.Index].FrameRate > 0)
                    {
                        var m = MotionItems[d.Index];
                        var total = Math.Max(0, m.FrameCount - 1);
                        var frame = Math.Max(0, Math.Min((int)Math.Round(d.Time * m.FrameRate), total));
                        frameStr = $"  [{frame}/{total} F]";
                    }
                    TimeTextBlock.Text = $"{d.Time:0.00} / {d.Length:0.00}{frameStr}";
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

        private void MotionsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //行のダブルクリックでもショートカット設定を開く
            if (MotionsDataGrid.SelectedItem is MotionItem)
            {
                OpenShortcutSetting();
            }
        }

        private void ShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            OpenShortcutSetting();
        }

        private async void OpenShortcutSetting()
        {
            var item = MotionsDataGrid.SelectedItem as MotionItem;
            if (item == null)
            {
                MessageBox.Show(this, LanguageSelector.Get("MotionPlaybackWindow_SelectMotionFirst"), LanguageSelector.Get("MotionPlaybackWindow_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            //選択中のモーションが再生画面でプレビュー中(=シークバーが対応)なら、現在のフレームを取り込む。
            //ポーズ適用のフレーム番号を手入力せず、見えている姿勢をそのまま登録できるようにする
            int currentFrame = 0;
            if (playingIndex == MotionsDataGrid.SelectedIndex && item.FrameRate > 0)
            {
                var total = Math.Max(0, item.FrameCount - 1);
                currentFrame = Math.Max(0, Math.Min((int)Math.Round(SeekSlider.Value * item.FrameRate), total));
            }

            await Globals.Client.SendCommandAsync(new PipeCommands.StartKeyConfig { });
            var win = new MotionKeyAddWindow(item.FilePath, currentFrame, item.FrameCount);
            win.Owner = this;
            var result = win.ShowDialog();
            await Globals.Client.SendCommandAsync(new PipeCommands.EndKeyConfig { });
            if (result == true)
            {
                UpdateShortcuts();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
