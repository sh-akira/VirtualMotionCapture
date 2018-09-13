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
using UnityNamedPipe;

namespace ControlWindowWPF
{
    /// <summary>
    /// TrackerConfigWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class TrackerConfigWindow : Window
    {
        private bool IsSetting = true;

        public TrackerConfigWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        public class TrackerInfo : ViewModelBase
        {
            private static string auto = "自動検出";
            private static string notfound = "[未接続]";
            public string TypeName { get { return Getter<string>(); } set { Setter(value); RaisePropertyChanged(nameof(Text)); } }
            public string SerialNumber { get { return Getter<string>(); } set { Setter(value); RaisePropertyChanged(nameof(Text)); } }
            public bool NotFound { get { return Getter<bool>(); } set { Setter(value); RaisePropertyChanged(nameof(Text)); } }
            public string Text { get { return $"{TypeName} ({SerialNumber ?? auto}){(NotFound ? notfound : string.Empty)}"; } }
            public Brush Background { get { return Getter<Brush>(); } set { Setter(value); } }

            public Tuple<string, string> ToTuple()
            {
                return Tuple.Create(TypeName, SerialNumber);
            }
        }

        public ObservableCollection<TrackerInfo> TrackersList { get; set; } = new ObservableCollection<TrackerInfo>();
        public ObservableCollection<TrackerInfo> TrackersViewList { get; set; } = new ObservableCollection<TrackerInfo>();

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetTrackerSerialNumbers(), d =>
            {
                var data = (PipeCommands.ReturnTrackerSerialNumbers)d;
                Dispatcher.Invoke(() => SetTrackersList(data.List, data.CurrentSetting));
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetTrackerOffsets(), d =>
            {
                var data = (PipeCommands.SetTrackerOffsets)d;
                Dispatcher.Invoke(() => SetTrackerOffsets(data));
            });
            Globals.Client.ReceivedEvent += Client_Received;
        }

        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.TrackerMoved))
            {
                var d = (PipeCommands.TrackerMoved)e.Data;
                var time = DateTime.Now.AddSeconds(3);
                var item = TrackersViewList.Where(t => t.SerialNumber == d.SerialNumber).FirstOrDefault();
                if (item == null) return;
                Dispatcher.Invoke(() => item.Background = ActiveBrush);
                if (endTime.ContainsKey(d.SerialNumber))
                {
                    endTime[d.SerialNumber] = time;
                }
                else
                {
                    endTime.TryAdd(d.SerialNumber, time);
                }
                var task = Task.Run(async () =>
                {
                    while (time > DateTime.Now)
                    {
                        await Task.Delay(200);
                    }
                    DateTime tmpTime;
                    endTime.TryGetValue(d.SerialNumber, out tmpTime);
                    if (tmpTime == time)
                    {
                        endTime.TryRemove(d.SerialNumber, out tmpTime);
                        Dispatcher.Invoke(() => item.Background = WhiteBrush);
                    }
                });
            }
        }
        private ConcurrentDictionary<string, DateTime> endTime = new ConcurrentDictionary<string, DateTime>();

        private Brush WhiteBrush = new SolidColorBrush(Colors.White);
        private Brush ActiveBrush = new SolidColorBrush(Colors.Green);

        private void SetTrackersList(List<Tuple<string, string>> list, PipeCommands.SetTrackerSerialNumbers setting)
        {
            TrackersList.Clear();
            list.Add(Tuple.Create("HMD", default(string)));
            list.Add(Tuple.Create("コントローラー", default(string)));
            list.Add(Tuple.Create("トラッカー", default(string)));
            list.Add(Tuple.Create("割り当てしない", default(string)));
            foreach (var d in list.OrderBy(d => d.Item1).ThenBy(d => d.Item2))
            {
                TrackersList.Add(new TrackerInfo { TypeName = d.Item1, SerialNumber = d.Item2, Background = WhiteBrush });
                if (d.Item2 != null) TrackersViewList.Add(new TrackerInfo { TypeName = d.Item1, SerialNumber = d.Item2, Background = WhiteBrush });
            }
            Func<Tuple<string, string>, TrackerInfo> getItem = (set) =>
            {
                var item = TrackersList.Where(d => d.TypeName == set.Item1 && d.SerialNumber == set.Item2).FirstOrDefault();
                if (item == null) { var newitem = new TrackerInfo { TypeName = set.Item1, SerialNumber = set.Item2, Background = WhiteBrush }; TrackersList.Add(newitem); item = newitem; }
                return item;
            };
            IsSetting = true;
            HeadTrackerComboBox.SelectedItem = getItem(setting.Head);
            LeftHandTrackerComboBox.SelectedItem = getItem(setting.LeftHand);
            RightHandTrackerComboBox.SelectedItem = getItem(setting.RightHand);
            PelvisTrackerComboBox.SelectedItem = getItem(setting.Pelvis);
            LeftFootTrackerComboBox.SelectedItem = getItem(setting.LeftFoot);
            RightFootTrackerComboBox.SelectedItem = getItem(setting.RightFoot);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsSetting) return;
            var comboBox = (ComboBox)sender;
            if (comboBox.SelectedItem == null) return;
            var command = new PipeCommands.SetTrackerSerialNumbers();
            command.Head = (HeadTrackerComboBox.SelectedItem as TrackerInfo ?? new TrackerInfo { TypeName = "HMD" }).ToTuple();
            command.LeftHand = (LeftHandTrackerComboBox.SelectedItem as TrackerInfo ?? new TrackerInfo { TypeName = "コントローラー" }).ToTuple();
            command.RightHand = (RightHandTrackerComboBox.SelectedItem as TrackerInfo ?? new TrackerInfo { TypeName = "コントローラー" }).ToTuple();
            command.Pelvis = (PelvisTrackerComboBox.SelectedItem as TrackerInfo ?? new TrackerInfo { TypeName = "トラッカー" }).ToTuple();
            command.LeftFoot = (LeftFootTrackerComboBox.SelectedItem as TrackerInfo ?? new TrackerInfo { TypeName = "トラッカー" }).ToTuple();
            command.RightFoot = (RightFootTrackerComboBox.SelectedItem as TrackerInfo ?? new TrackerInfo { TypeName = "トラッカー" }).ToTuple();
            await Globals.Client.SendCommandAsync(command);
        }

        private void SetTrackerOffsets(PipeCommands.SetTrackerOffsets offsets)
        {
            LeftHandTrackerOffsetToBodySideSlider.Value = Math.Round(offsets.LeftHandTrackerOffsetToBodySide * 1000.0);
            LeftHandTrackerOffsetToBottomSlider.Value = Math.Round(offsets.LeftHandTrackerOffsetToBottom * 1000.0);
            RightHandTrackerOffsetToBodySideSlider.Value = Math.Round(offsets.RightHandTrackerOffsetToBodySide * 1000.0);
            RightHandTrackerOffsetToBottomSlider.Value = Math.Round(offsets.RightHandTrackerOffsetToBottom * 1000.0);

            LeftHandTrackerOffsetToBodySideTextBlock.Text = LeftHandTrackerOffsetToBodySideSlider.Value.ToString() + " mm";
            LeftHandTrackerOffsetToBottomTextBlock.Text = LeftHandTrackerOffsetToBottomSlider.Value.ToString() + " mm";
            RightHandTrackerOffsetToBodySideTextBlock.Text = RightHandTrackerOffsetToBodySideSlider.Value.ToString() + " mm";
            RightHandTrackerOffsetToBottomTextBlock.Text = RightHandTrackerOffsetToBottomSlider.Value.ToString() + " mm";
            IsSetting = false;
        }

        private async void TrackerOffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsSetting) return;
            LeftHandTrackerOffsetToBodySideTextBlock.Text = LeftHandTrackerOffsetToBodySideSlider.Value.ToString() + " mm";
            LeftHandTrackerOffsetToBottomTextBlock.Text = LeftHandTrackerOffsetToBottomSlider.Value.ToString() + " mm";
            RightHandTrackerOffsetToBodySideTextBlock.Text = RightHandTrackerOffsetToBodySideSlider.Value.ToString() + " mm";
            RightHandTrackerOffsetToBottomTextBlock.Text = RightHandTrackerOffsetToBottomSlider.Value.ToString() + " mm";
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetTrackerOffsets
            {
                LeftHandTrackerOffsetToBodySide = (float)LeftHandTrackerOffsetToBodySideSlider.Value / 1000.0f,
                LeftHandTrackerOffsetToBottom = (float)LeftHandTrackerOffsetToBottomSlider.Value / 1000.0f,
                RightHandTrackerOffsetToBodySide = (float)RightHandTrackerOffsetToBodySideSlider.Value / 1000.0f,
                RightHandTrackerOffsetToBottom = (float)RightHandTrackerOffsetToBottomSlider.Value / 1000.0f,
            });
        }
    }
}
