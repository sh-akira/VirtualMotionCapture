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
    }
}
