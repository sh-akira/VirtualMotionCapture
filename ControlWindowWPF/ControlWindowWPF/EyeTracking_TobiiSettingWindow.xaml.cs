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
    /// EyeTracking_TobiiSettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class EyeTracking_TobiiSettingWindow : Window
    {
        private bool IsSetting = true;

        public EyeTracking_TobiiSettingWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetEyeTracking_TobiiOffsets(), d =>
            {
                var data = (PipeCommands.SetEyeTracking_TobiiOffsets )d;
                Dispatcher.Invoke(() => SetEyeTracking_TobiiOffsets(data));
            });
        }

        private void SetEyeTracking_TobiiOffsets(PipeCommands.SetEyeTracking_TobiiOffsets offsets)
        {
            IsSetting = true;
            EyeMoveScaleHorizontalSlider.Value = offsets.ScaleHorizontal;
            EyeMoveScaleVerticalSlider.Value = offsets.ScaleVertical;
            EyeMoveOffsetHorizontalSlider.Value = offsets.OffsetHorizontal;
            EyeMoveOffsetVerticalSlider.Value = offsets.OffsetVertical;
            SetUITexts();
            IsSetting = false;
        }

        private async void EyeMoveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsSetting) return;
            SetUITexts();
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetEyeTracking_TobiiOffsets 
            {
                ScaleHorizontal  = (float)EyeMoveScaleHorizontalSlider.Value,
                ScaleVertical  = (float)EyeMoveScaleVerticalSlider.Value ,
                OffsetHorizontal = (float)EyeMoveOffsetHorizontalSlider.Value,
                OffsetVertical  = (float)EyeMoveOffsetVerticalSlider.Value,
            });
        }

        private void SetUITexts()
        {
            EyeMoveScaleHorizontalTextBlock.Text = "x" + EyeMoveScaleHorizontalSlider.Value.ToString("0.##");
            EyeMoveScaleVerticalTextBlock.Text = "x" + EyeMoveScaleVerticalSlider.Value.ToString("0.##");
            EyeMoveOffsetHorizontalTextBlock.Text = EyeMoveOffsetHorizontalSlider.Value.ToString("0.##");
            EyeMoveOffsetVerticalTextBlock.Text = EyeMoveOffsetVerticalSlider.Value.ToString("0.##");
        }

        private async void CalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandAsync(new PipeCommands.EyeTracking_TobiiCalibration());   
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
