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
    /// EyeTracking_ViveProEyeSettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class EyeTracking_ViveProEyeSettingWindow : Window
    {
        private bool IsSetting = true;

        public EyeTracking_ViveProEyeSettingWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetEyeTracking_ViveProEyeOffsets(), d =>
            {
                var data = (PipeCommands.SetEyeTracking_ViveProEyeOffsets)d;
                Dispatcher.Invoke(() => SetEyeTracking_ViveProEyeOffsets(data));
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetEyeTracking_ViveProEyeUseEyelidMovements(), d =>
            {
                var data = (PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements)d;
                Dispatcher.Invoke(() =>
                {
                    IsSetting = true;
                    UseEyelidMovementsCheckBox.IsChecked = data.Use;
                    IsSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetEyeTracking_ViveProEyeEnable(), d =>
            {
                var data = (PipeCommands.SetEyeTracking_ViveProEyeEnable)d;
                Dispatcher.Invoke(() =>
                {
                    IsSetting = true;
                    UseViveProEyeCheckBox.IsChecked = data.enable;
                    IsSetting = false;
                });
            });
        }

        private void SetEyeTracking_ViveProEyeOffsets(PipeCommands.SetEyeTracking_ViveProEyeOffsets offsets)
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
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetEyeTracking_ViveProEyeOffsets
            {
                ScaleHorizontal = (float)EyeMoveScaleHorizontalSlider.Value,
                ScaleVertical = (float)EyeMoveScaleVerticalSlider.Value,
                OffsetHorizontal = (float)EyeMoveOffsetHorizontalSlider.Value,
                OffsetVertical = (float)EyeMoveOffsetVerticalSlider.Value,
            });
        }

        private void SetUITexts()
        {
            EyeMoveScaleHorizontalTextBlock.Text = "x" + EyeMoveScaleHorizontalSlider.Value.ToString("0.##");
            EyeMoveScaleVerticalTextBlock.Text = "x" + EyeMoveScaleVerticalSlider.Value.ToString("0.##");
            EyeMoveOffsetHorizontalTextBlock.Text = EyeMoveOffsetHorizontalSlider.Value.ToString("0.##");
            EyeMoveOffsetVerticalTextBlock.Text = EyeMoveOffsetVerticalSlider.Value.ToString("0.##");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void UseEyelidMovementsCheckBox_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (IsSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements
            {
                Use = UseEyelidMovementsCheckBox.IsChecked.Value
            });
        }

        private async void UseViveProEyeCheckBox_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (IsSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetEyeTracking_ViveProEyeEnable
            {
                enable = UseViveProEyeCheckBox.IsChecked.Value
            });
        }
    }
}
