using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using UnityNamedPipe;

namespace ControlWindowWPF
{
    /// <summary>
    /// FaceControlKeyAddWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class FaceControlKeyAddWindow : Window
    {
        public FaceControlKeyAddWindow()
        {
            InitializeComponent();
            KeysDataGrid.ItemsSource = faceItems;
        }
        public FaceControlKeyAddWindow(KeyAction action) : this()
        {
            for (int i = 0; i < action.FaceNames.Count; i++)
            {
                faceItems.Add(new FaceItem { Key = action.FaceNames[i], Value = action.FaceStrength[i] });
            }
            AutoBlinkCheckBox.IsChecked = action.StopBlink;
            CustomNameTextBox.Text = action.Name;
            KeyUpCheckBox.IsChecked = action.IsKeyUp;
            KeyConfigs.AddRange(action.KeyConfigs);
            UpdateKeys();
        }

        private ObservableCollection<FaceItem> faceItems = new ObservableCollection<FaceItem>();

        private List<KeyConfig> KeyConfigs = new List<KeyConfig>();

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandWaitAsync(new PipeCommands.GetFaceKeys(), d =>
            {
                var ret = (PipeCommands.ReturnFaceKeys)d;
                Dispatcher.Invoke(() => ShapeKeysComboBox.ItemsSource = ret.Keys);
            });
            Globals.Client.ReceivedEvent += Client_Received;
        }

        private bool ReceiveKey = false;

        private void KeysTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ReceiveKey = true;
        }

        private void KeysTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ReceiveKey = false;
        }

        private void KeyClearButton_Click(object sender, RoutedEventArgs e)
        {
            KeyConfigs.Clear();
            UpdateKeys();
        }

        private void UpdateKeys()
        {
            var texts = new List<string>();
            foreach (var key in KeyConfigs)
            {
                texts.Add(key.ToString());
            }
            if (texts.Count > 0)
            {
                KeysTextBox.Text = string.Join(" + ", texts);
            }
            else
            {
                KeysTextBox.Text = "ここをクリックして、操作するキーを押してください";
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetDefaultFace { }); //表情デフォルトを送って瞬き無効を解除
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

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (ShapeKeysComboBox.SelectedItem != null)
            {
                faceItems.Add(new FaceItem { Key = ShapeKeysComboBox.SelectedItem.ToString(), Value = 1.0f });
            }
        }

        private async void KeysDataGrid_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Property == Slider.ValueProperty)
            {
                //値が更新されたら表情に反映する
                await Globals.Client.SendCommandAsync(new PipeCommands.SetFace { Keys = faceItems.Select(d => d.Key).ToList(), Strength = faceItems.Select(d => d.Value).ToList() });
            }
        }

        private class FaceItem
        {
            public string Key { get; set; }
            public float Value { get; set; }
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (KeyConfigs.Count == 0)
            {
                MessageBox.Show("キーが設定されていません。割り当てるキーを設定してください", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var action = new KeyAction();
            action.KeyConfigs = KeyConfigs;
            var name = CustomNameTextBox.Text;
            action.Name = name;
            action.OnlyPress = false;
            action.FaceAction = true;
            action.FaceNames = faceItems.Select(d => d.Key).ToList();
            action.FaceStrength = faceItems.Select(d => d.Value).ToList();
            action.StopBlink = AutoBlinkCheckBox.IsChecked.Value;
            action.IsKeyUp = KeyUpCheckBox.IsChecked.Value;

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
