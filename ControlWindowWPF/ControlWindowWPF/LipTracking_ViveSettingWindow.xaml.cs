using System;
using System.Collections.Concurrent;
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
    /// LipTracking_ViveSettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class LipTracking_ViveSettingWindow : Window
    {
        private bool IsSetting = true;

        public LipTracking_ViveSettingWindow()
        {
            InitializeComponent();
        }
        public class BlendShapeItem : ViewModelBase
        {
            public string LipShape { get { return Getter<string>(); } set { Setter(value); } }
            public string BlendShape { get { return Getter<string>(); } set { Setter(value); } }
            public Brush Background { get { return Getter<Brush>(); } set { Setter(value); } }
            public List<string> BlendShapeKeys { get { return Getter<List<string>>(); } set { Setter(value); } }
        }

        private List<string> LipShapes = null;
        public List<string> BlendShapeKeys = null;

        public ObservableCollection<BlendShapeItem> BlendShapeItems = new ObservableCollection<BlendShapeItem>();


        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var blendshapes = BlendShapeItems.Select(d => d.BlendShape).ToList();
            var lipShapesToBlendShapeMap = new Dictionary<string, string>();
            foreach (var item in BlendShapeItems)
            {
                if (item.BlendShape != null)
                {
                    lipShapesToBlendShapeMap.Add(item.LipShape, item.BlendShape);
                }
            }
            await Globals.Client.SendCommandAsync(new PipeCommands.SetViveLipTrackingBlendShape { LipShapes = LipShapes, LipShapesToBlendShapeMap = lipShapesToBlendShapeMap });
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
            await Globals.Client.SendCommandWaitAsync(new PipeCommands.GetViveLipTrackingBlendShape(), d =>
            {
                var ret = (PipeCommands.SetViveLipTrackingBlendShape)d;
                Dispatcher.Invoke(() =>
                {
                    LipShapes = ret.LipShapes;
                    foreach (var key in LipShapes)
                    {
                        BlendShapeItems.Add(new BlendShapeItem { LipShape = key, BlendShape = ret.LipShapesToBlendShapeMap.ContainsKey(key) ? ret.LipShapesToBlendShapeMap[key] : null, BlendShapeKeys = BlendShapeKeys });
                    }
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetViveLipTrackingEnable(), d =>
            {
                var data = (PipeCommands.SetViveLipTrackingEnable)d;
                Dispatcher.Invoke(() =>
                {
                    IsSetting = true;
                    UseViveLipTrackerCheckBox.IsChecked = data.enable;
                    IsSetting = false;
                });
            });
            KeysDataGrid.DataContext = this;
            KeysDataGrid.ItemsSource = BlendShapeItems;
        }

        private async void UseViveLipTrackerCheckBox_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (IsSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetViveLipTrackingEnable
            {
                enable = UseViveLipTrackerCheckBox.IsChecked.Value
            });
        }
    }
}
