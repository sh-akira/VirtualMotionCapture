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
    /// MidiCCBlendShapeSettingWIndow.xaml の相互作用ロジック
    /// </summary>
    public partial class MidiCCBlendShapeSettingWIndow : Window
    {
        public class BlendShapeItem : ViewModelBase
        {
            public int Index { get { return Getter<int>(); } set { Setter(value); } }
            public string BlendShape { get { return Getter<string>(); } set { Setter(value); } }
            public Brush Background { get { return Getter<Brush>(); } set { Setter(value); } }
            public List<string> BlendShapeKeys { get { return Getter<List<string>>(); } set { Setter(value); } }
        }

        public List<string> BlendShapeKeys = null;

        public ObservableCollection<BlendShapeItem> BlendShapeItems = new ObservableCollection<BlendShapeItem>();

        private Brush WhiteBrush = new SolidColorBrush(Colors.White);
        private Brush ActiveBrush = new SolidColorBrush(Colors.Green);

        private ConcurrentDictionary<int, DateTime> endTime = new ConcurrentDictionary<int, DateTime>();

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
                    foreach (var key in ret.BlendShapes)
                    {
                        BlendShapeItems.Add(new BlendShapeItem { Index = index++, BlendShape = key, BlendShapeKeys = BlendShapeKeys });
                    }
                });
            });
            KeysDataGrid.DataContext = this;
            KeysDataGrid.ItemsSource = BlendShapeItems;

            await Globals.Client.SendCommandAsync(new PipeCommands.StartKeySend { });
            Globals.Client.ReceivedEvent += Client_Received;
        }

        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.MidiCCKnobUpdate))
            {
                var d = (PipeCommands.MidiCCKnobUpdate)e.Data;
                var time = DateTime.Now.AddSeconds(3);
                var item = BlendShapeItems.Where(t => t.Index == d.knobNo).FirstOrDefault();
                if (item == null) return;
                Dispatcher.Invoke(() => item.Background = ActiveBrush);
                if (endTime.ContainsKey(d.knobNo))
                {
                    endTime[d.knobNo] = time;
                }
                else
                {
                    endTime.TryAdd(d.knobNo, time);
                }
                var task = Task.Run(async () =>
                {
                    while (time > DateTime.Now)
                    {
                        await Task.Delay(200);
                    }
                    DateTime tmpTime;
                    endTime.TryGetValue(d.knobNo, out tmpTime);
                    if (tmpTime == time)
                    {
                        endTime.TryRemove(d.knobNo, out tmpTime);
                        Dispatcher.Invoke(() => item.Background = WhiteBrush);
                    }
                });
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
            await Globals.Client.SendCommandAsync(new PipeCommands.EndKeySend { });
        }
    }
}
