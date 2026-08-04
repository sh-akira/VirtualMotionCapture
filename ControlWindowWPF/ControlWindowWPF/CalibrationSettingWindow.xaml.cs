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
    public partial class CalibrationSettingWindow : Window
    {

        private CalibrationSettingItem CalibrationSetting = new CalibrationSettingItem();
        private WristRotationFixSettingItem WristRotationFixSetting = new WristRotationFixSettingItem();

        public CalibrationSettingWindow()
        {
            InitializeComponent();
            
            OverrideBodyHeightGroupBox.DataContext = CalibrationSetting;
            PelvisOffsetGroupBox.DataContext = CalibrationSetting;
            WristRotationFixSettingGroupBox.DataContext = WristRotationFixSetting;
        }

        bool disablePropertyChanged = false;
        private async void CalibrationSetting_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (disablePropertyChanged) return;
            await Globals.Client?.SendCommandAsync(CalibrationSetting.ConvertToPipeCommands());
        }

        private async void WristRotationFixSetting_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (disablePropertyChanged) return;
            await Globals.Client?.SendCommandAsync(WristRotationFixSetting.ConvertToPipeCommands());
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // CalibrationSetting読み込み
            await Globals.Client.SendCommandWaitAsync(new PipeCommands.GetCalibrationSetting(), d =>
            {
                var ret = (PipeCommands.SetCalibrationSetting)d;
                Dispatcher.Invoke(() => {
                    disablePropertyChanged = true;
                    CalibrationSetting.SetFromPipeCommands(ret);
                    disablePropertyChanged = false;
                });
            });

            // WristRotationFixSetting読み込み
            await Globals.Client.SendCommandWaitAsync(new PipeCommands.GetWristRotationFixSetting(), d =>
            {
                var ret = (PipeCommands.SetWristRotationFixSetting)d;
                Dispatcher.Invoke(() => {
                    disablePropertyChanged = true;
                    WristRotationFixSetting.SetFromPipeCommands(ret);
                    disablePropertyChanged = false;
                });
            });

            CalibrationSetting.PropertyChanged += CalibrationSetting_PropertyChanged;
            WristRotationFixSetting.PropertyChanged += WristRotationFixSetting_PropertyChanged;
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            CalibrationSetting.PropertyChanged -= CalibrationSetting_PropertyChanged;
            WristRotationFixSetting.PropertyChanged -= WristRotationFixSetting_PropertyChanged;
        }
    }

    public class CalibrationSettingItem : ViewModelBase
    {
        public bool EnableOverrideBodyHeight { get => Getter<bool>(); set => Setter(value); }
        public int OverrideBodyHeight { get => Getter<int>(); set => Setter(value); }
        public int PelvisOffsetAdjustY { get => Getter<int>(); set => Setter(value); }
        public int PelvisOffsetAdjustZ { get => Getter<int>(); set => Setter(value); }

        public float OverrideBodyHeightcm { get => Getter<float>(); set { OverrideBodyHeight = Convert.ToInt32(value * 10); Setter(value); } }
        public float PelvisOffsetAdjustYcm { get => Getter<float>(); set { PelvisOffsetAdjustY = Convert.ToInt32(value * 10); Setter(value); } }
        public float PelvisOffsetAdjustZcm { get => Getter<float>(); set { PelvisOffsetAdjustZ = Convert.ToInt32(value * 10); Setter(value); } }

        public PipeCommands.SetCalibrationSetting ConvertToPipeCommands()
        {
            return new PipeCommands.SetCalibrationSetting
            {
                EnableOverrideBodyHeight = EnableOverrideBodyHeight,
                OverrideBodyHeight = OverrideBodyHeight,
                PelvisOffsetAdjustY = PelvisOffsetAdjustY,
                PelvisOffsetAdjustZ = PelvisOffsetAdjustZ,
            };
        }

        public void SetFromPipeCommands(PipeCommands.SetCalibrationSetting CalibrationSetting)
        {
            EnableOverrideBodyHeight = CalibrationSetting.EnableOverrideBodyHeight;
            OverrideBodyHeightcm = CalibrationSetting.OverrideBodyHeight / 10f;
            OverrideBodyHeight = CalibrationSetting.OverrideBodyHeight;
            PelvisOffsetAdjustYcm = CalibrationSetting.PelvisOffsetAdjustY / 10f;
            PelvisOffsetAdjustZcm = CalibrationSetting.PelvisOffsetAdjustZ / 10f;
            PelvisOffsetAdjustY = CalibrationSetting.PelvisOffsetAdjustY;
            PelvisOffsetAdjustZ = CalibrationSetting.PelvisOffsetAdjustZ;
        }
    }

    public class WristRotationFixSettingItem : ViewModelBase
    {
        public int UpperArmWeight { get => Getter<int>(); set => Setter(value); }
        public int ForearmWeight { get => Getter<int>(); set => Setter(value); }
        public int MaxAccumulatedTwist { get => Getter<int>(); set => Setter(value); }

        public float UpperArmWeightPercent 
        { 
            get => Getter<float>(); 
            set { UpperArmWeight = Convert.ToInt32(value * 10); Setter(value); } 
        }
        public float ForearmWeightPercent 
        { 
            get => Getter<float>(); 
            set { ForearmWeight = Convert.ToInt32(value * 10); Setter(value); } 
        }

        public PipeCommands.SetWristRotationFixSetting ConvertToPipeCommands()
        {
            return new PipeCommands.SetWristRotationFixSetting
            {
                UpperArmWeight = UpperArmWeight,
                ForearmWeight = ForearmWeight,
                MaxAccumulatedTwist = MaxAccumulatedTwist,
            };
        }

        public void SetFromPipeCommands(PipeCommands.SetWristRotationFixSetting WristRotationFixSetting)
        {
            UpperArmWeightPercent = WristRotationFixSetting.UpperArmWeight / 10f;
            ForearmWeightPercent = WristRotationFixSetting.ForearmWeight / 10f;
            MaxAccumulatedTwist = WristRotationFixSetting.MaxAccumulatedTwist;
            UpperArmWeight = WristRotationFixSetting.UpperArmWeight;
            ForearmWeight = WristRotationFixSetting.ForearmWeight;
        }
    }
}
