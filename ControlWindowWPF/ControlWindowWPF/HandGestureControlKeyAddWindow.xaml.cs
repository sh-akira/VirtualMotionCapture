using sh_akira;
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
using System.Windows.Shapes;
using UnityMemoryMappedFile;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// HandGestureControlKeyAddWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class HandGestureControlKeyAddWindow : Window
    {
        private List<int> handAngles = new List<int>();

        private const string PresetDirectory = "HandPresets";

        public HandGestureControlKeyAddWindow()
        {
            InitializeComponent();

            for (int i = 1; i <= 20; i++)
            {
                var slider = this.FindName("ValueSlider" + i.ToString("00")) as Slider;
                var textblock = this.FindName("ValueTextBlock" + i.ToString("00")) as TextBlock;
                slider.ValueChanged += Slider_ValueChanged;
                slider.Tag = i;
                Sliders.Add(slider);
                TextBlocks.Add(textblock);
                handAngles.Add((int)slider.Value);
            }
            AngleLimitCheckBox_Checked(null, null);
            if (Directory.Exists(Globals.GetCurrentAppDir() + PresetDirectory) == false)
            {
                Directory.CreateDirectory(Globals.GetCurrentAppDir() + PresetDirectory);
            }
            PresetComboBox.ItemsSource = Directory.EnumerateFiles(Globals.GetCurrentAppDir() + PresetDirectory, "*.json").Select(d => System.IO.Path.GetFileNameWithoutExtension(d));
        }
        public HandGestureControlKeyAddWindow(KeyAction action) : this()
        {
            CustomNameTextBox.Text = action.Name;
            if (action.Hand == Hands.Both) BothHandRadioButton.IsChecked = true;
            else if (action.Hand == Hands.Right) RightHandRadioButton.IsChecked = true;
            else LeftHandRadioButton.IsChecked = true;
            KeyUpCheckBox.IsChecked = action.IsKeyUp;

            isLoading = true;
            for (int i = 1; i <= 20; i++)
            {
                var slider = this.FindName("ValueSlider" + i.ToString("00")) as Slider;
                var textblock = this.FindName("ValueTextBlock" + i.ToString("00")) as TextBlock;
                slider.Value = action.HandAngles[i - 1];
                textblock.Text = slider.Value.ToString();
                handAngles[(int)slider.Tag - 1] = (int)slider.Value;
            }
            AnimationTimeSlider.Value = action.HandChangeTime;
            AnimationTimeTextBlock.Text = action.HandChangeTime.ToString("0.00");
            isLoading = false;

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

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
            await Globals.Client.SendCommandAsync(new PipeCommands.EndHandCamera());
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

        private bool isLoading = false;
        private async void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isLoading) return;
            var slider = sender as Slider;
            var textblock = TextBlocks[Sliders.IndexOf(slider)];
            textblock.Text = slider.Value.ToString();
            handAngles[(int)slider.Tag - 1] = (int)slider.Value;
            var command = new PipeCommands.SetHandAngle { HandAngles = handAngles };
            if (LeftHandRadioButton.IsChecked == true) command.LeftEnable = true;
            if (RightHandRadioButton.IsChecked == true) command.RightEnable = true;
            if (BothHandRadioButton.IsChecked == true) command.LeftEnable = command.RightEnable = true;
            await Globals.Client.SendCommandAsync(command);
            CustomNameTextBox.Text = LanguageSelector.Get("Custom");
        }

        private List<Slider> Sliders = new List<Slider>();
        private List<TextBlock> TextBlocks = new List<TextBlock>();

        private async void HandRadioButton_Checked_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.StartHandCamera { IsLeft = (LeftHandRadioButton != null && LeftHandRadioButton.IsChecked == true) });
        }

        private void AngleLimitCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var slider in Sliders)
            {
                if (slider.Value < -90) slider.Value = -90;
                else if (slider.Value > 10) slider.Value = 10;
                slider.Minimum = -90;
                slider.Maximum = 10;
            }
        }

        private void AngleLimitCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var slider in Sliders)
            {
                slider.Minimum = -180;
                slider.Maximum = 180;
            }
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (KeyConfigs.Count == 0)
            {
                MessageBox.Show(LanguageSelector.Get("KeyNotFoundError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var action = new KeyAction();
            action.KeyConfigs = KeyConfigs;
            var name = CustomNameTextBox.Text;
            if (string.IsNullOrEmpty(name) && PresetComboBox.SelectedItem != null)
            {
                name = PresetComboBox.SelectedItem as string;
            }
            else
            {
                name = LanguageSelector.Get("Custom");
            }
            action.Name = name;
            action.OnlyPress = false;
            action.HandAction = true;
            action.HandAngles = handAngles;
            action.Hand = BothHandRadioButton.IsChecked == true ? Hands.Both : (RightHandRadioButton.IsChecked == true ? Hands.Right : Hands.Left);
            action.IsKeyUp = KeyUpCheckBox.IsChecked.Value;
            action.HandChangeTime = (float)AnimationTimeSlider.Value;

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

        private void CustomSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.CheckFileNameIsValid(CustomNameTextBox.Text) == false)
            {
                MessageBox.Show(LanguageSelector.Get("FileNameIsInvalidError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var path = Globals.GetCurrentAppDir() + PresetDirectory + "\\" + CustomNameTextBox.Text + ".json";
            if (File.Exists(path))
            {
                if (MessageBox.Show(LanguageSelector.Get("Overwritten"), LanguageSelector.Get("Confirm"), MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel)
                {
                    return;
                }
            }
            File.WriteAllText(path, Json.Serializer.Serialize(handAngles));
            PresetComboBox.ItemsSource = Directory.EnumerateFiles(Globals.GetCurrentAppDir() + PresetDirectory, "*.json").Select(d => System.IO.Path.GetFileNameWithoutExtension(d));
        }

        private async void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetComboBox.SelectedItem == null) return;
            var path = Globals.GetCurrentAppDir() + PresetDirectory + "\\" + PresetComboBox.SelectedItem.ToString() + ".json";
            handAngles = Json.Serializer.Deserialize<List<int>>(File.ReadAllText(path));
            isLoading = true;
            if (handAngles.Where(d => d < -90 || d > 10).Any()) AngleLimitCheckBox.IsChecked = false;
            for (int i = 0; i < handAngles.Count; i++)
            {
                Sliders[i].Value = handAngles[i];
                TextBlocks[i].Text = handAngles[i].ToString();
            }
            isLoading = false;
            var command = new PipeCommands.SetHandAngle { HandAngles = handAngles };
            if (LeftHandRadioButton.IsChecked == true) command.LeftEnable = true;
            if (RightHandRadioButton.IsChecked == true) command.RightEnable = true;
            if (BothHandRadioButton.IsChecked == true) command.LeftEnable = command.RightEnable = true;
            await Globals.Client.SendCommandAsync(command);
            CustomNameTextBox.Text = "";
        }

        private void AnimationTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isLoading || AnimationTimeTextBlock == null) return;
            AnimationTimeTextBlock.Text = AnimationTimeSlider.Value.ToString("0.00");
        }
    }
}
