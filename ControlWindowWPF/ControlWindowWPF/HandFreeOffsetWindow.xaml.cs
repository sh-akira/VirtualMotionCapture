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
        public int SwivelOffset { get => Getter<int>(); set => Setter(value); }

        public float LeftHandPositionXcm { get => Getter<float>(); set { LeftHandPositionX = Convert.ToInt32(value * 10); Setter(value); } }
        public float LeftHandPositionYcm { get => Getter<float>(); set { LeftHandPositionY = Convert.ToInt32(value * 10); Setter(value); } }
        public float LeftHandPositionZcm { get => Getter<float>(); set { LeftHandPositionZ = Convert.ToInt32(value * 10); Setter(value); } }
        public float RightHandPositionXcm { get => Getter<float>(); set { RightHandPositionX = Convert.ToInt32(value * 10); Setter(value); } }
        public float RightHandPositionYcm { get => Getter<float>(); set { RightHandPositionY = Convert.ToInt32(value * 10); Setter(value); } }
        public float RightHandPositionZcm { get => Getter<float>(); set { RightHandPositionZ = Convert.ToInt32(value * 10); Setter(value); } }

        public void SyncToLeft()
        {
            RightHandPositionXcm = LeftHandPositionXcm;
            RightHandPositionYcm = LeftHandPositionYcm;
            RightHandPositionZcm = LeftHandPositionZcm;
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
                RightHandRotationZ = RightHandRotationZ,
                SwivelOffset = SwivelOffset
            };
        }

        public void SetFromPipeCommands(PipeCommands.SetHandFreeOffset FreeOffset)
        {
            LeftHandPositionXcm = FreeOffset.LeftHandPositionX / 10f;
            LeftHandPositionYcm = FreeOffset.LeftHandPositionY / 10f;
            LeftHandPositionZcm = FreeOffset.LeftHandPositionZ / 10f;
            LeftHandRotationX = FreeOffset.LeftHandRotationX;
            LeftHandRotationY = FreeOffset.LeftHandRotationY;
            LeftHandRotationZ = FreeOffset.LeftHandRotationZ;
            RightHandPositionXcm = FreeOffset.RightHandPositionX / 10f;
            RightHandPositionYcm = FreeOffset.RightHandPositionY / 10f;
            RightHandPositionZcm = FreeOffset.RightHandPositionZ / 10f;
            RightHandRotationX = FreeOffset.RightHandRotationX;
            RightHandRotationY = FreeOffset.RightHandRotationY;
            RightHandRotationZ = FreeOffset.RightHandRotationZ;
            SwivelOffset = FreeOffset.SwivelOffset;
        }
    }
}
