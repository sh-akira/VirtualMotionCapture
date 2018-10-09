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

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// FunctionKeyAddWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class FunctionKeyAddWindow : Window
    {
        public ObservableCollection<string> FunctionItems = new ObservableCollection<string>
        {
            LanguageSelector.Get("Functions_ShowControlPanel"),
            LanguageSelector.Get("Functions_ColorGreen"),
            LanguageSelector.Get("Functions_ColorBlue"),
            LanguageSelector.Get("Functions_ColorWhite"),
            LanguageSelector.Get("Functions_ColorCustom"),
            LanguageSelector.Get("Functions_ColorTransparent"),
            LanguageSelector.Get("Functions_FrontCamera"),
            LanguageSelector.Get("Functions_BackCamera"),
            LanguageSelector.Get("Functions_FreeCamera"),
            LanguageSelector.Get("Functions_PositionFixedCamera"),
        };
        public FunctionKeyAddWindow()
        {
            InitializeComponent();
            FunctionComboBox.ItemsSource = FunctionItems;
        }
        public FunctionKeyAddWindow(KeyAction action) : this()
        {
            FunctionComboBox.SelectedIndex = (int)action.Function;
            KeyUpCheckBox.IsChecked = action.IsKeyUp;
            KeyConfigs.AddRange(action.KeyConfigs);
            UpdateKeys();
        }

        private List<KeyConfig> KeyConfigs = new List<KeyConfig>();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
                KeysTextBox.Text = LanguageSelector.Get("KeysWatermark");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
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
            if (FunctionComboBox.SelectedItem == null)
            {
                MessageBox.Show(LanguageSelector.Get("FunctionNotFoundError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var action = new KeyAction();
            action.KeyConfigs = KeyConfigs;
            var name = FunctionComboBox.SelectedItem.ToString();
            action.Name = name;
            action.OnlyPress = true;
            action.FunctionAction = true;
            action.Function = (Functions)FunctionComboBox.SelectedIndex;
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
