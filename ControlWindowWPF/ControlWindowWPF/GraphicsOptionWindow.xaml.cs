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
    /// GraphicsOptionWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class GraphicsOptionWindow : Window
    {
        bool Initializing = true;
        public GraphicsOptionWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Globals.Client.ReceivedEvent += Client_Received;

            await Globals.Client.SendCommandAsync(new PipeCommands.GetPostProcessing { });
            Initializing = false;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
        }

        private async void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.SetPostProcessing))
            {
                var d = (PipeCommands.SetPostProcessing)e.Data;
                ValueApply(d);
            }
        }

        private async void ValueChanged()
        {
            if (Initializing) { return; }
            //セットされたpostProcessingValueを送信する
            await Globals.Client.SendCommandAsync(new PipeCommands.SetPostProcessing { 
                PPS_Enable = true, //暫定
                
                Bloom_Enable = Bloom_Enable_CheckBox?.IsChecked ?? false,
                Bloom_Intensity = (float)Bloom_Intensity_Slider?.Value,
                Bloom_Threshold = (float)Bloom_Threshold_Slider?.Value,

                DoF_Enable = DoF_Enable_CheckBox?.IsChecked ?? false,
                DoF_FocusDistance = (float)(DoF_FocusDistance_Slider?.Value ?? 0f),
                DoF_Aperture = (float)(DoF_Aperture_Slider?.Value ?? 0f),
                DoF_FocusLength = (float)(DoF_FocusLength_Slider?.Value ?? 0f),
                DoF_MaxBlurSize = (int)(DoF_MaxBlurSize_Slider?.Value ?? 0),

                CG_Enable = CG_Enable_CheckBox?.IsChecked ?? false,
                CG_Temperature = (float)(CG_Temperature_Slider?.Value ?? 0f),
                CG_Saturation = (float)(CG_Saturation_Slider?.Value ?? 0f),
                CG_Contrast = (float)(CG_Contrast_Slider?.Value ?? 0f),

                Vignette_Enable = Vignette_Enable_CheckBox?.IsChecked ?? false,
                Vignette_Intensity = (float)(Vignette_Intensity_Slider?.Value ?? 0f),
                Vignette_Smoothness = (float)(Vignette_Smoothness_Slider?.Value ?? 0f),
                Vignette_Rounded = (float)(Vignette_Rounded_Slider?.Value ?? 0f),

                CA_Enable = CA_Enable_CheckBox?.IsChecked ?? false,
                CA_Intensity = (float)(CA_Intensity_Slider?.Value ?? 0f)
            });
        }
        private async void ValueApply(PipeCommands.SetPostProcessing d)
        {
            //postProcessingValueを画面に反映する
            Dispatcher.Invoke(() =>
            {
                if (Bloom_Enable_CheckBox != null)
                {
                    Bloom_Enable_CheckBox.IsChecked = d.Bloom_Enable;
                }
                if (Bloom_Intensity_Slider != null)
                {
                    Bloom_Intensity_Slider.Value = d.Bloom_Intensity;
                }
                if (Bloom_Threshold_Slider != null)
                {
                    Bloom_Threshold_Slider.Value = d.Bloom_Threshold;
                }

                if (DoF_Enable_CheckBox != null)
                {
                    DoF_Enable_CheckBox.IsChecked = d.DoF_Enable;
                }
                if (DoF_FocusDistance_Slider != null)
                {
                    DoF_FocusDistance_Slider.Value = d.DoF_FocusDistance;
                }
                if (DoF_Aperture_Slider != null)
                {
                    DoF_Aperture_Slider.Value = d.DoF_Aperture;
                }
                if (DoF_FocusLength_Slider != null)
                {
                    DoF_FocusLength_Slider.Value = d.DoF_FocusLength;
                }
                if (DoF_MaxBlurSize_Slider != null)
                {
                    DoF_MaxBlurSize_Slider.Value = d.DoF_MaxBlurSize;
                }

                if (CG_Enable_CheckBox != null)
                {
                    CG_Enable_CheckBox.IsChecked = d.CG_Enable;
                }
                if (CG_Temperature_Slider != null)
                {
                    CG_Temperature_Slider.Value = d.CG_Temperature;
                }
                if (CG_Saturation_Slider != null)
                {
                    CG_Saturation_Slider.Value = d.CG_Saturation;
                }
                if (CG_Contrast_Slider != null)
                {
                    CG_Contrast_Slider.Value = d.CG_Contrast;
                }

                if (Vignette_Enable_CheckBox != null)
                {
                    Vignette_Enable_CheckBox.IsChecked = d.Vignette_Enable;
                }
                if (Vignette_Intensity_Slider != null)
                {
                    Vignette_Intensity_Slider.Value = d.Vignette_Intensity;
                }
                if (Vignette_Smoothness_Slider != null)
                {
                    Vignette_Smoothness_Slider.Value = d.Vignette_Smoothness;
                }
                if (Vignette_Rounded_Slider != null)
                {
                    Vignette_Rounded_Slider.Value = d.Vignette_Rounded;
                }

                if (CA_Enable_CheckBox != null)
                {
                    CA_Enable_CheckBox.IsChecked = d.CA_Enable;
                }
                if (CA_Intensity_Slider != null)
                {
                    CA_Intensity_Slider.Value = d.CA_Intensity;
                }
            });

        }

        private void SliverValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ValueChanged();
        }

        private void Checked(object sender, RoutedEventArgs e)
        {
            ValueChanged();
        }
    }
}
