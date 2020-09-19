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

            await Globals.Client.SendCommandAsync(new PipeCommands.GetAdvancedGraphicsOption { });
            Initializing = false;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
        }

        private async void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.SetAdvancedGraphicsOption))
            {
                var d = (PipeCommands.SetAdvancedGraphicsOption)e.Data;
                ValueApply(d);
            }
        }

        private async void ValueChanged()
        {
            if (Initializing) { return; }
            //セットされたpostProcessingValueを送信する
            await Globals.Client.SendCommandAsync(new PipeCommands.SetAdvancedGraphicsOption
            { 
                PPS_Enable = Global_Enable_CheckBox?.IsChecked ?? false,
                
                Bloom_Enable = Bloom_Enable_CheckBox?.IsChecked ?? false,
                Bloom_Intensity = (float)Bloom_Intensity_Slider?.Value,
                Bloom_Threshold = (float)Bloom_Threshold_Slider?.Value,
                Bloom_Color_r = (float)((Bloom_Color_Button.Background as SolidColorBrush).Color.R / 255.0),
                Bloom_Color_g = (float)((Bloom_Color_Button.Background as SolidColorBrush).Color.G / 255.0),
                Bloom_Color_b = (float)((Bloom_Color_Button.Background as SolidColorBrush).Color.B / 255.0),
                Bloom_Color_a = (float)((Bloom_Color_Button.Background as SolidColorBrush).Color.A / 255.0),

                DoF_Enable = DoF_Enable_CheckBox?.IsChecked ?? false,
                DoF_FocusDistance = (float)(DoF_FocusDistance_Slider?.Value ?? 0f),
                DoF_Aperture = (float)(DoF_Aperture_Slider?.Value ?? 0f),
                DoF_FocusLength = (float)(DoF_FocusLength_Slider?.Value ?? 0f),
                DoF_MaxBlurSize = (int)(DoF_MaxBlurSize_Slider?.Value ?? 0),

                CG_Enable = CG_Enable_CheckBox?.IsChecked ?? false,
                CG_Temperature = (float)(CG_Temperature_Slider?.Value ?? 0f),
                CG_Saturation = (float)(CG_Saturation_Slider?.Value ?? 0f),
                CG_Contrast = (float)(CG_Contrast_Slider?.Value ?? 0f),
                CG_Gamma = (float)(CG_Gamma_Slider?.Value ?? 0f),
                CG_ColorFilter_r = (float)((CG_ColorFilter_Button.Background as SolidColorBrush).Color.R / 255.0),
                CG_ColorFilter_g = (float)((CG_ColorFilter_Button.Background as SolidColorBrush).Color.G / 255.0),
                CG_ColorFilter_b = (float)((CG_ColorFilter_Button.Background as SolidColorBrush).Color.B / 255.0),
                CG_ColorFilter_a = (float)((CG_ColorFilter_Button.Background as SolidColorBrush).Color.A / 255.0),

                Vignette_Enable = Vignette_Enable_CheckBox?.IsChecked ?? false,
                Vignette_Intensity = (float)(Vignette_Intensity_Slider?.Value ?? 0f),
                Vignette_Smoothness = (float)(Vignette_Smoothness_Slider?.Value ?? 0f),
                Vignette_Roundness = (float)(Vignette_Roundness_Slider?.Value ?? 0f),
                Vignette_Color_r = (float)((Vignette_Color_Button.Background as SolidColorBrush).Color.R / 255.0),
                Vignette_Color_g = (float)((Vignette_Color_Button.Background as SolidColorBrush).Color.G / 255.0),
                Vignette_Color_b = (float)((Vignette_Color_Button.Background as SolidColorBrush).Color.B / 255.0),
                Vignette_Color_a = (float)((Vignette_Color_Button.Background as SolidColorBrush).Color.A / 255.0),

                CA_Enable = CA_Enable_CheckBox?.IsChecked ?? false,
                CA_Intensity = (float)(CA_Intensity_Slider?.Value ?? 0f),
                CA_FastMode = CA_FastMode_CheckBox?.IsChecked ?? false
            });
        }
        private void ValueApply(PipeCommands.SetAdvancedGraphicsOption d)
        {
            //postProcessingValueを画面に反映する
            Dispatcher.Invoke(() =>
            {
                if (Global_Enable_CheckBox != null)
                {
                    Global_Enable_CheckBox.IsChecked = d.PPS_Enable;
                }
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
                if (Bloom_Color_Button != null) {
                    Color c = Color.FromArgb((byte)(d.Bloom_Color_a * 255), (byte)(d.Bloom_Color_r * 255), (byte)(d.Bloom_Color_g * 255), (byte)(d.Bloom_Color_b * 255));
                    Bloom_Color_Button.Background = new SolidColorBrush(c);
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
                if (CG_Gamma_Slider != null)
                {
                    CG_Gamma_Slider.Value = d.CG_Gamma;
                }
                if (CG_ColorFilter_Button != null)
                {
                    Color c = Color.FromArgb((byte)(d.CG_ColorFilter_a * 255), (byte)(d.CG_ColorFilter_r * 255), (byte)(d.CG_ColorFilter_g * 255), (byte)(d.CG_ColorFilter_b * 255));
                    CG_ColorFilter_Button.Background = new SolidColorBrush(c);
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
                if (Vignette_Roundness_Slider != null)
                {
                    Vignette_Roundness_Slider.Value = d.Vignette_Roundness;
                }
                if (Vignette_Color_Button != null)
                {
                    Color c = Color.FromArgb((byte)(d.Vignette_Color_a * 255), (byte)(d.Vignette_Color_r * 255), (byte)(d.Vignette_Color_g * 255), (byte)(d.Vignette_Color_b * 255));
                    Vignette_Color_Button.Background = new SolidColorBrush(c);
                }


                if (CA_Enable_CheckBox != null)
                {
                    CA_Enable_CheckBox.IsChecked = d.CA_Enable;
                }
                if (CA_Intensity_Slider != null)
                {
                    CA_Intensity_Slider.Value = d.CA_Intensity;
                }
                if (CA_FastMode_CheckBox != null)
                {
                    CA_FastMode_CheckBox.IsChecked = d.CA_FastMode;
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


        private void CG_ColorFilter_Button_Clicked(object sender, RoutedEventArgs e)
        {
            var win = new ColorPickerWindow();
            win.SelectedColor = (CG_ColorFilter_Button.Background as SolidColorBrush).Color;
            win.SelectedColorChangedEvent += ColorPickerWindow_SelectedColorChanged_CG_ColorFilter;
            win.Owner = this;
            win.ShowDialog();
            win.SelectedColorChangedEvent -= ColorPickerWindow_SelectedColorChanged_CG_ColorFilter;
        }

        private void ColorPickerWindow_SelectedColorChanged_CG_ColorFilter(object sender, Color e)
        {
            CG_ColorFilter_Button.Background = new SolidColorBrush(e);
            ValueChanged();
        }

        private void Bloom_Color_Button_Clicked(object sender, RoutedEventArgs e)
        {
            var win = new ColorPickerWindow();
            win.SelectedColor = (Bloom_Color_Button.Background as SolidColorBrush).Color;
            win.SelectedColorChangedEvent += ColorPickerWindow_SelectedColorChanged_Bloom_Color;
            win.Owner = this;
            win.ShowDialog();
            win.SelectedColorChangedEvent -= ColorPickerWindow_SelectedColorChanged_Bloom_Color;
        }
        private void ColorPickerWindow_SelectedColorChanged_Bloom_Color(object sender, Color e)
        {
            Bloom_Color_Button.Background = new SolidColorBrush(e);
            ValueChanged();
        }


        private void Vignette_Color_Button_Clicked(object sender, RoutedEventArgs e)
        {
            var win = new ColorPickerWindow();
            win.SelectedColor = (Vignette_Color_Button.Background as SolidColorBrush).Color;
            win.SelectedColorChangedEvent += ColorPickerWindow_SelectedColorChanged_Vignette_Color_Button;
            win.Owner = this;
            win.ShowDialog();
            win.SelectedColorChangedEvent -= ColorPickerWindow_SelectedColorChanged_Vignette_Color_Button;
        }
        private void ColorPickerWindow_SelectedColorChanged_Vignette_Color_Button(object sender, Color e)
        {
            Vignette_Color_Button.Background = new SolidColorBrush(e);
            ValueChanged();
        }

    }
}
/*
         private void LightColorButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new ColorPickerWindow();
            win.SelectedColor = (LightColorButton.Background as SolidColorBrush).Color;
            win.SelectedColorChangedEvent += ColorPickerWindow_SelectedColorChanged;
            win.Owner = this;
            win.ShowDialog();
            win.SelectedColorChangedEvent -= ColorPickerWindow_SelectedColorChanged;
        }

        private async void ColorPickerWindow_SelectedColorChanged(object sender, Color e)
        {
            LightColorButton.Background = new SolidColorBrush(e);
            await Globals.Client?.SendCommandAsync(new PipeCommands.ChangeLightColor { a = e.A / 255f, r = e.R / 255f, g = e.G / 255f, b = e.B / 255f });
        }
*/