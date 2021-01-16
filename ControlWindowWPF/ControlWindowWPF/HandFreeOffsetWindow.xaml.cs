using System;
using System.Collections.Generic;
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
    /// HandFreeOffsetWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class HandFreeOffsetWindow : Window
    {

        private FreeOffsetItem FreeOffset = Globals.FreeOffset;

        public HandFreeOffsetWindow()
        {
            InitializeComponent();
            DataContext = FreeOffset;
        }

        bool disablePropertyChanged = false;
        private async void FreeOffset_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (disablePropertyChanged) return;
            disablePropertyChanged = true;
            if (RightHandStackPanel.IsEnabled == false) FreeOffset.SyncToLeft();
            disablePropertyChanged = false;
            await Globals.Client?.SendCommandAsync(FreeOffset.ConvertToPipeCommands());
        }

        private void SyncToLeftCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            RightHandStackPanel.IsEnabled = false;
            FreeOffset.SyncToLeft();
        }

        private void SyncToLeftCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            RightHandStackPanel.IsEnabled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FreeOffset.PropertyChanged += FreeOffset_PropertyChanged;
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            FreeOffset.PropertyChanged -= FreeOffset_PropertyChanged;
        }
    }
    public class FreeOffsetItem : ViewModelBase
    {
        public int LeftHandPositionX { get => Getter<int>(); set => Setter(value); }
        public int LeftHandPositionY { get => Getter<int>(); set => Setter(value); }
        public int LeftHandPositionZ { get => Getter<int>(); set => Setter(value); }
        public int LeftHandRotationX { get => Getter<int>(); set => Setter(value); }
        public int LeftHandRotationY { get => Getter<int>(); set => Setter(value); }
        public int LeftHandRotationZ { get => Getter<int>(); set => Setter(value); }
        public int RightHandPositionX { get => Getter<int>(); set => Setter(value); }
        public int RightHandPositionY { get => Getter<int>(); set => Setter(value); }
        public int RightHandPositionZ { get => Getter<int>(); set => Setter(value); }
        public int RightHandRotationX { get => Getter<int>(); set => Setter(value); }
        public int RightHandRotationY { get => Getter<int>(); set => Setter(value); }
        public int RightHandRotationZ { get => Getter<int>(); set => Setter(value); }

        public void SyncToLeft()
        {
            RightHandPositionX = LeftHandPositionX;
            RightHandPositionY = LeftHandPositionY;
            RightHandPositionZ = LeftHandPositionZ;
            RightHandRotationX = LeftHandRotationX;
            RightHandRotationY = LeftHandRotationY;
            RightHandRotationZ = LeftHandRotationZ;
        }

        public PipeCommands.SetHandFreeOffset ConvertToPipeCommands()
        {
            return new PipeCommands.SetHandFreeOffset
            {
                LeftHandPositionX = LeftHandPositionX,
                LeftHandPositionY = LeftHandPositionY,
                LeftHandPositionZ = LeftHandPositionZ,
                LeftHandRotationX = LeftHandRotationX,
                LeftHandRotationY = LeftHandRotationY,
                LeftHandRotationZ = LeftHandRotationZ,
                RightHandPositionX = RightHandPositionX,
                RightHandPositionY = RightHandPositionY,
                RightHandPositionZ = RightHandPositionZ,
                RightHandRotationX = RightHandRotationX,
                RightHandRotationY = RightHandRotationY,
                RightHandRotationZ = RightHandRotationZ
            };
        }

        public void SetFromPipeCommands(PipeCommands.SetHandFreeOffset FreeOffset)
        {
            LeftHandPositionX = FreeOffset.LeftHandPositionX;
            LeftHandPositionY = FreeOffset.LeftHandPositionY;
            LeftHandPositionZ = FreeOffset.LeftHandPositionZ;
            LeftHandRotationX = FreeOffset.LeftHandRotationX;
            LeftHandRotationY = FreeOffset.LeftHandRotationY;
            LeftHandRotationZ = FreeOffset.LeftHandRotationZ;
            RightHandPositionX = FreeOffset.RightHandPositionX;
            RightHandPositionY = FreeOffset.RightHandPositionY;
            RightHandPositionZ = FreeOffset.RightHandPositionZ;
            RightHandRotationX = FreeOffset.RightHandRotationX;
            RightHandRotationY = FreeOffset.RightHandRotationY;
            RightHandRotationZ = FreeOffset.RightHandRotationZ;
        }
    }
}
