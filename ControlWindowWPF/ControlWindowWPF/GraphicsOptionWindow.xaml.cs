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
        public GraphicsOptionWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Globals.Client.ReceivedEvent += Client_Received;

            await Globals.Client.SendCommandAsync(new PipeCommands.GetPostProcessing { });
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
            //セットされたpostProcessingValueを送信する
            await Globals.Client.SendCommandAsync(new PipeCommands.SetPostProcessing { 
                AntiAliasing_Enable = AntiAliasing_Enable_CheckBox.IsChecked.Value,
                
                Bloom_Enable = Bloom_Enable_CheckBox.IsChecked.Value,
                Bloom_Intensity = (float)Bloom_Intensity_Slider.Value,

                DoF_Enable = DoF_Enable_CheckBox.IsChecked.Value,
                DoF_FocusDistance = (float)DoF_FocusDistance_Slider.Value,
                DoF_Aperture = (float)DoF_Aperture_Slider.Value,
                DoF_FocusLength = (float)DoF_FocusLength_Slider.Value,
                DoF_MaxBlurSize = (float)DoF_MaxBlurSize_Slider.Value,

                CG_Enable = CG_Enable_CheckBox.IsChecked.Value,
                CG_Temperature = (float)CG_Temperature_Slider.Value,
                CG_Saturation = (float)CG_Saturation_Slider.Value,
                CG_Contrast = (float)CG_Contrast_Slider.Value,

                Vignette_Enable = Vignette_Enable_CheckBox.IsChecked.Value,
                Vignette_Intensity = (float)Vignette_Intensity_Slider.Value,
                Vignette_Smoothness = (float)Vignette_Smoothness_Slider.Value,
                Vignette_Rounded = (float)Vignette_Rounded_Slider.Value,

                CA_Enable = CA_Enable_CheckBox.IsChecked.Value,
                CA_Intensity = (float)CA_Intensity_Slider.Value
            });
        }
        private async void ValueApply(PipeCommands.SetPostProcessing d)
        {
            //postProcessingValueを画面に反映する
            AntiAliasing_Enable_CheckBox.IsChecked = d.AntiAliasing_Enable;

            Bloom_Enable_CheckBox.IsChecked = d.Bloom_Enable;
            Bloom_Intensity_Slider.Value = d.Bloom_Intensity;

            DoF_Enable_CheckBox.IsChecked = d.DoF_Enable;
            DoF_FocusDistance_Slider.Value = d.DoF_FocusDistance;
            DoF_Aperture_Slider.Value = d.DoF_Aperture;
            DoF_FocusLength_Slider.Value = d.DoF_FocusLength;
            DoF_MaxBlurSize_Slider.Value = d.DoF_MaxBlurSize;

            CG_Enable_CheckBox.IsChecked = d.CG_Enable;
            CG_Temperature_Slider.Value = d.CG_Temperature;
            CG_Saturation_Slider.Value = d.CG_Saturation;
            CG_Contrast_Slider.Value = d.CG_Contrast;

            Vignette_Enable_CheckBox.IsChecked = d.Vignette_Enable;
            Vignette_Intensity_Slider.Value = d.Vignette_Intensity;
            Vignette_Smoothness_Slider.Value = d.Vignette_Smoothness;
            Vignette_Rounded_Slider.Value = d.Vignette_Rounded;

            CA_Enable_CheckBox.IsChecked = d.CA_Enable;
            CA_Intensity_Slider.Value = d.CA_Intensity;
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
