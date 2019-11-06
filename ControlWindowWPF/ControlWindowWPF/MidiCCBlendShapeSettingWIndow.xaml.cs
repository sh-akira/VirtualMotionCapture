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
using UnityMemoryMappedFile;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// MidiCCBlendShapeSettingWIndow.xaml の相互作用ロジック
    /// </summary>
    public partial class MidiCCBlendShapeSettingWIndow : Window
    {
        public class BlendShapeItem
        {
            public int Index { get; set; }
            public string BlendShape { get; set; }
        }

        public List<string> BlendShapeKeys = null;

        public ObservableCollection<BlendShapeItem> BlendShapeItems = new ObservableCollection<BlendShapeItem>();

        public MidiCCBlendShapeSettingWIndow()
        {
            InitializeComponent();
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var blendshapes = BlendShapeItems.Select(d => d.BlendShape).ToList();
            await Globals.Client.SendCommandAsync(new PipeCommands.SetMidiCCBlendShape { BlendShapes = blendshapes });
            this.Close();
        }

        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BlendShapeItems.Clear();
            await Globals.Client.SendCommandWaitAsync(new PipeCommands.GetFaceKeys(), d =>
            {
                var ret = (PipeCommands.ReturnFaceKeys)d;
                Dispatcher.Invoke(() => BlendShapeKeys = ret.Keys);
            });
            BlendShapeKeys?.Insert(0, null);
            await Globals.Client.SendCommandWaitAsync(new PipeCommands.GetMidiCCBlendShape(), d =>
            {
                var ret = (PipeCommands.SetMidiCCBlendShape)d;
                Dispatcher.Invoke(() =>
                {
                    int index = 0;
                    foreach(var key in ret.BlendShapes)
                    {
                        BlendShapeItems.Add(new BlendShapeItem { Index = index++, BlendShape = key });
                    }
                });
            });
            KeysDataGrid.DataContext = this;
        }
    }
}
