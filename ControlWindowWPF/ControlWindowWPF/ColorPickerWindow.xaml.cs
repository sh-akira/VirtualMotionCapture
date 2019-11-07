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

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// ColorPickerWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ColorPickerWindow : Window
    {
        public ColorPickerWindow()
        {
            InitializeComponent();
        }

        public EventHandler<Color> SelectedColorChangedEvent;

        public Color? SelectedColor
        {
            get => colorPicker.SelectedColor;
            set => colorPicker.SelectedColor = value;
        }

        private void ColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            SelectedColorChangedEvent?.Invoke(sender, e.NewValue.Value);
        }
    }
}
