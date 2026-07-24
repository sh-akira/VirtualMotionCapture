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
    /// MotionKeyAddWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MotionKeyAddWindow : Window
    {
        private class MotionFileItem
        {
            public string Name { get; set; }
            public string FilePath { get; set; }
            public override string ToString() => Name;
        }

        private List<KeyConfig> KeyConfigs = new List<KeyConfig>();
        private KeyAction EditTargetAction = null;
        private string preselectMotionPath = null;

        public MotionKeyAddWindow()
        {
            InitializeComponent();
            ActionTypeComboBox.ItemsSource = new ObservableCollection<string>
            {
                LanguageSelector.Get("MotionKeyAddWindow_TypePlay"),
                LanguageSelector.Get("MotionKeyAddWindow_TypePose"),
                LanguageSelector.Get("MotionKeyAddWindow_TypeRelease"),
            };
            ActionTypeComboBox.SelectedIndex = 0;
            UpdateKeys();
        }

        public MotionKeyAddWindow(KeyAction action) : this()
        {
            EditTargetAction = action;
            ActionTypeComboBox.SelectedIndex = action.MotionPlayType;
            FrameTextBox.Text = action.MotionFrame.ToString();
            KeyUpCheckBox.IsChecked = action.IsKeyUp;
            KeyConfigs.AddRange(action.KeyConfigs);
            UpdateKeys();
        }

        //再生画面から特定モーションを選択済みの状態で開く
        public MotionKeyAddWindow(string motionFilePath) : this()
        {
            preselectMotionPath = motionFilePath;
        }

        //再生画面のプレビュー位置(フレーム)を取り込んで開く
        public MotionKeyAddWindow(string motionFilePath, int currentFrame, int frameCount) : this()
        {
            preselectMotionPath = motionFilePath;
            FrameTextBox.Text = currentFrame.ToString();
            if (frameCount > 0)
            {
                FrameTotalTextBlock.Text = $"/ {frameCount - 1}";
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Globals.Client.ReceivedEvent += Client_Received;

            //読み込み済みのモーション一覧を取得する
            await Globals.Client.SendCommandWaitAsync(new PipeCommands.Motion_GetFileList(), d =>
            {
                var ret = (PipeCommands.Motion_ReturnFileList)d;
                Dispatcher.Invoke(() =>
                {
                    MotionFileComboBox.Items.Clear();
                    if (ret.Files != null)
                    {
                        foreach (var info in ret.Files)
                        {
                            MotionFileComboBox.Items.Add(new MotionFileItem { Name = info.Name, FilePath = info.FilePath });
                        }
                    }
                    var targetPath = EditTargetAction != null ? EditTargetAction.MotionFilePath : preselectMotionPath;
                    if (string.IsNullOrEmpty(targetPath) == false)
                    {
                        var item = MotionFileComboBox.Items.Cast<MotionFileItem>().FirstOrDefault(f => f.FilePath == targetPath);
                        if (item != null) MotionFileComboBox.SelectedItem = item;
                    }
                    else if (MotionFileComboBox.Items.Count > 0)
                    {
                        MotionFileComboBox.SelectedIndex = 0;
                    }
                });
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
        }

        private bool ReceiveKey = false;

        private void KeysListBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ReceiveKey = true;
        }

        private void KeysListBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ReceiveKey = false;
        }

        private void KeyRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (KeysListBox.SelectedIndex != -1 && KeyConfigs?.Count > 0)
            {
                KeyConfigs.RemoveAt(KeysListBox.SelectedIndex);
            }
            UpdateKeys();
        }

        private void UpdateKeys()
        {
            KeysListBox.Items.Clear();
            if (KeyConfigs.Count > 0)
            {
                foreach (var key in KeyConfigs)
                {
                    KeysListBox.Items.Add(key.ToString());
                }
            }
            else
            {
                KeysListBox.Items.Add(LanguageSelector.Get("KeysWatermark"));
            }
        }

        private void ActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MotionFilePanel == null || FramePanel == null) return;
            var type = ActionTypeComboBox.SelectedIndex;
            MotionFilePanel.IsEnabled = type == 0 || type == 1; //再生かポーズ適用のときだけファイル選択
            FramePanel.IsEnabled = type == 1; //ポーズ適用のときだけフレーム指定
        }

        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.CommandType == typeof(PipeCommands.KeyDown))
                {
                    var d = (PipeCommands.KeyDown)e.Data;
                    if (ReceiveKey)
                    {
                        if (KeyConfigs.Where(k => k.IsEqual(d.Config)).Any() == false)
                        {
                            KeyConfigs.Add(d.Config);
                            UpdateKeys();
                        }
                    }
                }
            });
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (KeyConfigs.Count == 0)
            {
                MessageBox.Show(LanguageSelector.Get("KeyNotFoundError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var type = ActionTypeComboBox.SelectedIndex;
            var selectedFile = MotionFileComboBox.SelectedItem as MotionFileItem;
            if ((type == 0 || type == 1) && selectedFile == null)
            {
                MessageBox.Show(LanguageSelector.Get("MotionKeyAddWindow_MotionNotFoundError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            int frame = 0;
            if (type == 1 && (int.TryParse(FrameTextBox.Text, out frame) == false || frame < 0))
            {
                MessageBox.Show(LanguageSelector.Get("MotionKeyAddWindow_FrameError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var action = new KeyAction();
            action.KeyConfigs = KeyConfigs;
            action.OnlyPress = true;
            action.MotionAction = true;
            action.MotionPlayType = type;
            action.MotionFilePath = selectedFile?.FilePath;
            action.MotionFrame = frame;
            action.IsKeyUp = KeyUpCheckBox.IsChecked.Value;
            switch (type)
            {
                case 0:
                    action.Name = $"{LanguageSelector.Get("MotionKeyAddWindow_TypePlay")} : {selectedFile.Name}";
                    break;
                case 1:
                    action.Name = $"{LanguageSelector.Get("MotionKeyAddWindow_TypePose")} : {selectedFile.Name} [{frame}]";
                    break;
                default:
                    action.Name = LanguageSelector.Get("MotionKeyAddWindow_TypeRelease");
                    break;
            }

            if (Globals.KeyActions == null) Globals.KeyActions = new List<KeyAction>();
            Globals.KeyActions.Add(action);
            await Globals.Client.SendCommandAsync(new PipeCommands.SetKeyActions { KeyActions = Globals.KeyActions });
            this.DialogResult = true;
        }

        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
