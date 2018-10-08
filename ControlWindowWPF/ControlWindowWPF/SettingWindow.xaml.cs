using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using UnityNamedPipe;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// SettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingWindow : Window
    {
        private ObservableCollection<float> RotationItems = new ObservableCollection<float> { -180.0f, -135.0f, -90.0f, -45.0f, 0.0f, 45.0f, 90.0f, 135.0f, 180.0f };
        public SettingWindow()
        {
            InitializeComponent();
            LeftHandRotateComboBox.ItemsSource = RotationItems;
            RightHandRotateComboBox.ItemsSource = RotationItems;
            if (RotationItems.Contains(Globals.LeftHandRotation)) LeftHandRotateComboBox.SelectedItem = Globals.LeftHandRotation;
            if (RotationItems.Contains(Globals.RightHandRotation)) RightHandRotateComboBox.SelectedItem = Globals.RightHandRotation;
        }

        private async void LeftHandRotateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LeftHandRotateComboBox.SelectedItem == null) return;
            Globals.LeftHandRotation = (float)LeftHandRotateComboBox.SelectedItem;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetHandRotations { LeftHandRotation = Globals.LeftHandRotation, RightHandRotation = Globals.RightHandRotation });
        }

        private async void RightHandRotateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RightHandRotateComboBox.SelectedItem == null) return;
            Globals.RightHandRotation = (float)RightHandRotateComboBox.SelectedItem;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetHandRotations { LeftHandRotation = Globals.LeftHandRotation, RightHandRotation = Globals.RightHandRotation });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private async void ExternalCameraConigButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "externalcamera.cfg|externalcamera.cfg";
            if (ofd.ShowDialog() == true)
            {
                var configs = new Dictionary<string, string>();
                var lines = File.ReadAllLines(ofd.FileName);
                foreach (var line in lines)
                {
                    if (line.Contains("="))
                    {
                        var items = line.Split(new string[] { "=" }, 2, StringSplitOptions.None);
                        configs.Add(items[0], items[1]);
                    }
                }
                Func<string, float> GetFloat = (string key) =>
                {
                    if (configs.ContainsKey(key) == false) { return 0.0f; }
                    if (float.TryParse(configs[key], out var ret)) { return ret; }
                    return 0.0f;
                };
                var x = GetFloat("x");
                var y = GetFloat("y");
                var z = GetFloat("z");
                var rx = GetFloat("rx");
                var ry = GetFloat("ry");
                var rz = GetFloat("rz");
                var fov = GetFloat("fov");

                await Globals.Client?.SendCommandAsync(new PipeCommands.SetExternalCameraConfig { x = x, y = y, z = z, rx = rx, ry = ry, rz = rz, fov = fov, ControllerIndex = ControllerComboBox.SelectedIndex });
            }
        }

        private void TrackerConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new TrackerConfigWindow();
            win.Show();
        }
    }
}
