using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
    /// SettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingWindow : Window
    {
        public class ResolutionItem
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int RefreshRate { get; set; }
        }
        public ObservableCollection<ResolutionItem> ResolutionItems;

        public ObservableCollection<TrackerConfigWindow.TrackerInfo> TrackersList { get; set; } = new ObservableCollection<TrackerConfigWindow.TrackerInfo>();

        private ObservableCollection<float> RotationItems = new ObservableCollection<float> { -180.0f, -135.0f, -90.0f, -45.0f, 0.0f, 45.0f, 90.0f, 135.0f, 180.0f };
        public SettingWindow()
        {
            var language = Globals.CurrentLanguage;
            InitializeComponent();
            this.DataContext = this;
            var languageitem = LanguageComboBox.Items.Cast<string>().First(d => d.Contains(language));
            LanguageComboBox.SelectedItem = languageitem;
            LeftHandRotateComboBox.ItemsSource = RotationItems;
            RightHandRotateComboBox.ItemsSource = RotationItems;
            if (RotationItems.Contains(Globals.LeftHandRotation)) LeftHandRotateComboBox.SelectedItem = Globals.LeftHandRotation;
            if (RotationItems.Contains(Globals.RightHandRotation)) RightHandRotateComboBox.SelectedItem = Globals.RightHandRotation;
        }

        private Brush WhiteBrush = new SolidColorBrush(Colors.White);
        private Brush ActiveBrush = new SolidColorBrush(Colors.Green);

        private ConcurrentDictionary<string, DateTime> endTime = new ConcurrentDictionary<string, DateTime>();
        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.TrackerMoved))
            {
                var d = (PipeCommands.TrackerMoved)e.Data;
                var time = DateTime.Now.AddSeconds(3);
                var item = TrackersList.Where(t => t.SerialNumber == d.SerialNumber).FirstOrDefault();
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
            else if (e.CommandType == typeof(PipeCommands.StatusStringChanged))
            {
                var d = (PipeCommands.StatusStringChanged)e.Data;
                Dispatcher.Invoke(() => StatusStringTextbox.Text = d.StatusString);
            }
        }

        private void SetTrackersList(List<Tuple<string, string>> list, PipeCommands.SetTrackerSerialNumbers setting)
        {
            TrackersList.Clear();
            foreach (var d in list.OrderBy(d => d.Item1).ThenBy(d => d.Item2))
            {
                var trackerinfo = new TrackerConfigWindow.TrackerInfo { TypeName = d.Item1, SerialNumber = d.Item2, Background = WhiteBrush };
                TrackersList.Add(trackerinfo);
            }
        }

        private async void LeftHandRotateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LeftHandRotateComboBox.SelectedItem == null) return;
            Globals.LeftHandRotation = (float)LeftHandRotateComboBox.SelectedItem;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetHandRotations { LeftHandRotation = Globals.LeftHandRotation, RightHandRotation = Globals.RightHandRotation });
        }

        private async void RightHandRotateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RightHandRotateComboBox.SelectedItem == null) return;
            Globals.RightHandRotation = (float)RightHandRotateComboBox.SelectedItem;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetHandRotations { LeftHandRotation = Globals.LeftHandRotation, RightHandRotation = Globals.RightHandRotation });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private async void ExternalCameraConigButton_Click(object sender, RoutedEventArgs e)
        {
            if (ControllerComboBox.SelectedItem == null)
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_SelectedItemError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var tracker = ControllerComboBox.SelectedItem as TrackerConfigWindow.TrackerInfo;
            var ofd = new OpenFileDialog();
            ofd.Filter = "externalcamera.cfg|externalcamera.cfg";
            if (ofd.ShowDialog() == true)
            {
                var configs = new Dictionary<string, string>();
                var lines = File.ReadAllLines(ofd.FileName);
                foreach (var line in lines)
                {
                    if (line.Contains("="))
                    {
                        var items = line.Split(new string[] { "=" }, 2, StringSplitOptions.None);
                        configs.Add(items[0], items[1]);
                    }
                }
                Func<string, float> GetFloat = (string key) =>
                {
                    if (configs.ContainsKey(key) == false) { return 0.0f; }
                    if (float.TryParse(configs[key], out var ret)) { return ret; }
                    return 0.0f;
                };
                var x = GetFloat("x");
                var y = GetFloat("y");
                var z = GetFloat("z");
                var rx = GetFloat("rx");
                var ry = GetFloat("ry");
                var rz = GetFloat("rz");
                var fov = GetFloat("fov");

                await Globals.Client?.SendCommandAsync(new PipeCommands.SetExternalCameraConfig { x = x, y = y, z = z, rx = rx, ry = ry, rz = rz, fov = fov, ControllerName = tracker.SerialNumber });
            }
        }

        private async void ExternalCameraConigExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (ControllerComboBox.SelectedItem == null)
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_SelectedItemError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var tracker = ControllerComboBox.SelectedItem as TrackerConfigWindow.TrackerInfo;
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetExternalCameraConfig { ControllerName = tracker.SerialNumber }, r =>
            {
                var d = (PipeCommands.SetExternalCameraConfig)r;
                Dispatcher.Invoke(() =>
                {
                    var sfd = new SaveFileDialog();
                    sfd.Filter = "externalcamera.cfg|externalcamera.cfg";
                    sfd.Title = "Export externalcamera.cfg";
                    sfd.FileName = "externalcamera.cfg";
                    if (sfd.ShowDialog() == true)
                    {
                        var culture = System.Globalization.CultureInfo.InvariantCulture;
                        var format = culture.NumberFormat;
                        var lines = new List<string>();
                        lines.Add($"x=" + d.x.ToString("G", format));
                        lines.Add($"y=" + d.y.ToString("G", format));
                        lines.Add($"z=" + d.z.ToString("G", format));
                        lines.Add($"rx=" + d.rx.ToString("G", format));
                        lines.Add($"ry=" + d.ry.ToString("G", format));
                        lines.Add($"rz=" + d.rz.ToString("G", format));
                        lines.Add($"fov=" + d.fov.ToString("G", format));
                        lines.Add($"near=0.01");
                        lines.Add($"far=1000");
                        lines.Add($"disableStandardAssets=False");
                        lines.Add($"frameSkip=0");
                        File.WriteAllLines(sfd.FileName, lines);
                    }
                });
            });
        }

        private void TrackerConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new TrackerConfigWindow();
            win.Show();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var language = LanguageComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(language)) language = "Japanese";
            if (language.Contains(" ("))
            {
                language = language.Split(' ').First();
            }
            LanguageSelector.ChangeLanguage(language);
        }

        private bool isSetting = false;

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Globals.Client.ReceivedEvent += Client_Received;
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetTrackerSerialNumbers(), d =>
            {
                var data = (PipeCommands.ReturnTrackerSerialNumbers)d;
                Dispatcher.Invoke(() => SetTrackersList(data.List, data.CurrentSetting));
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetResolutions { }, d =>
            {
                var config = (PipeCommands.ReturnResolutions)d;
                Dispatcher.Invoke(() =>
                {
                    ResolutionItems = new ObservableCollection<ResolutionItem>(config.List.Select(r => new ResolutionItem { Width = r.Item1, Height = r.Item2, RefreshRate = r.Item3 }));
                    ResolutionComboBox.ItemsSource = ResolutionItems;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetVirtualWebCamConfig { }, d =>
            {
                var config = (PipeCommands.SetVirtualWebCamConfig)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    WebCamEnableCheckBox.IsChecked = config.Enabled;
                    WebCamResizeCheckBox.IsChecked = config.Resize;
                    WebCamMirrorCheckBox.IsChecked = config.Mirroring;
                    WebCamBufferingComboBox.SelectedIndex = config.Buffering;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetEnableExternalMotionSender { }, d =>
            {
                var data = (PipeCommands.EnableExternalMotionSender)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    ExternalMotionSenderEnableCheckBox.IsChecked = data.enable;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetExternalMotionSenderAddress { }, d =>
            {
                var data = (PipeCommands.ChangeExternalMotionSenderAddress)d;
                Dispatcher.Invoke(() =>
                {
                    ExternalMotionSenderAddressTextBox.Text = data.address;
                    ExternalMotionSenderPortTextBox.Text = data.port.ToString();
                    PeriodStatusTextBox.Text = data.PeriodStatus.ToString();
                    PeriodRootTextBox.Text = data.PeriodRoot.ToString();
                    PeriodBoneTextBox.Text = data.PeriodBone.ToString();
                    PeriodBlendShapeTextBox.Text = data.PeriodBlendShape.ToString();
                    PeriodCameraTextBox.Text = data.PeriodCamera.ToString();
                    PeriodDevicesTextBox.Text = data.PeriodDevices.ToString();
                    OptionStringTextbox.Text = data.OptionString;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetEnableExternalMotionReceiver { }, d =>
            {
                var data = (PipeCommands.EnableExternalMotionReceiver)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    ExternalMotionReceiverEnableCheckBox.IsChecked = data.enable;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetEnableTrackingFilter { }, d =>
            {
                var data = (PipeCommands.EnableTrackingFilter)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    TrackingFilterEnable.IsChecked = data.globalEnable;
                    TrackingFilterHmdEnable.IsChecked = data.hmdEnable;
                    TrackingFilterControllerEnable.IsChecked = data.controllerEnable;
                    TrackingFilterTrackerEnable.IsChecked = data.trackerEnable;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetEnableModelModifier { }, d =>
            {
                var data = (PipeCommands.EnableModelModifier)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    FixKneeRotationCheckBox.IsChecked = data.fixKneeRotation;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetHandleControllerAsTracker { }, d =>
            {
                var data = (PipeCommands.EnableHandleControllerAsTracker)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    HandleControllerAsTrackerCheckBox.IsChecked = data.HandleControllerAsTracker;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetStatusString { }, d =>
            {
                var data = (PipeCommands.SetStatusString)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    StatusStringTextbox.Text = data.StatusString;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetQualitySettings { }, d =>
            {
                var data = (PipeCommands.SetQualitySettings)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    var antialiasingList = new List<int> { 0, 2, 4, 8 };
                    AntiAliasingComboBox.ItemsSource = antialiasingList;
                    AntiAliasingComboBox.SelectedItem = data.antiAliasing;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandAsync(new PipeCommands.TrackerMovedRequest { doSend = true });
            await Globals.Client?.SendCommandAsync(new PipeCommands.StatusStringChangedRequest { doSend = true });
        }

        private void VirtualWebCamInstallButton_Click(object sender, RoutedEventArgs e)
        {
            var directory = @"C:\VMC_Camera\";
            if (Directory.Exists(directory) == false)
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception)
                {
                    MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedFolderCreate"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            try
            {
                File.Copy(Globals.GetCurrentAppDir() + @"VMC_Camera\VMC_CameraFilter32bit.dll", directory + "VMC_CameraFilter32bit.dll", true);
                File.Copy(Globals.GetCurrentAppDir() + @"VMC_Camera\VMC_CameraFilter64bit.dll", directory + "VMC_CameraFilter64bit.dll", true);
            }
            catch (Exception)
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedFileCopy"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var process32 = System.Diagnostics.Process.Start(Globals.GetCurrentAppDir() + "DLLInstaller32.exe", "/i /s " + directory + "VMC_CameraFilter32bit.dll");
            var process64 = System.Diagnostics.Process.Start(Globals.GetCurrentAppDir() + "DLLInstaller64.exe", "/i /s " + directory + "VMC_CameraFilter64bit.dll");
            process32.WaitForExit();
            process64.WaitForExit();
            if (process32.ExitCode == 0 && process64.ExitCode == 0)
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_SuccessDriverInstall"));
            }
            else
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedDriverInstall"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void VirtualWebCamUninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var directory = @"C:\VMC_Camera\";
            var process32 = System.Diagnostics.Process.Start(Globals.GetCurrentAppDir() + "DLLInstaller32.exe", "/u /s " + directory + "VMC_CameraFilter32bit.dll");
            var process64 = System.Diagnostics.Process.Start(Globals.GetCurrentAppDir() + "DLLInstaller64.exe", "/u /s " + directory + "VMC_CameraFilter64bit.dll");
            process32.WaitForExit();
            process64.WaitForExit();
            if (process32.ExitCode == 0 && process64.ExitCode == 0)
            {
                try
                {
                    File.Delete(directory + "VMC_CameraFilter32bit.dll");
                    File.Delete(directory + "VMC_CameraFilter64bit.dll");
                }
                catch (Exception)
                {
                    MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedFileDelete"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                try
                {
                    Directory.Delete(directory);
                }
                catch (Exception)
                {
                    MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedFolderDelete"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                MessageBox.Show(LanguageSelector.Get("SettingWindow_SuccessDriverUninstall"));
            }
            else
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedDriverUninstall"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WebCamCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateWebCamConfig();
        }

        private void WebCamBufferingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateWebCamConfig();
        }

        private async void UpdateWebCamConfig()
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetVirtualWebCamConfig
            {
                Enabled = WebCamEnableCheckBox.IsChecked == true,
                Resize = WebCamResizeCheckBox.IsChecked == true,
                Mirroring = WebCamMirrorCheckBox.IsChecked == true,
                Buffering = WebCamBufferingComboBox.SelectedIndex,
            });
        }

        private async void ResolutionApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResolutionComboBox.SelectedItem == null) return;
            var item = ResolutionComboBox.SelectedItem as ResolutionItem;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetResolution
            {
                Width = item.Width,
                Height = item.Height,
                RefreshRate = item.RefreshRate,
            });
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            await Globals.Client?.SendCommandAsync(new PipeCommands.TrackerMovedRequest { doSend = false });
            await Globals.Client?.SendCommandAsync(new PipeCommands.StatusStringChangedRequest { doSend = false });
        }

        private void EyeTracking_TobiiSettingButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new EyeTracking_TobiiSettingWindow();
            win.ShowDialog();
        }

        private void EyeTracking_ViveProEyeSettingButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new EyeTracking_ViveProEyeSettingWindow();
            win.ShowDialog();
        }

        private async void CameraPlus_ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();

            ofd.Filter = "cameraplus.cfg|cameraplus.cfg";
            if (ofd.ShowDialog() == true)
            {
                var configs = new Dictionary<string, string>();
                var lines = File.ReadAllLines(ofd.FileName);
                foreach (var line in lines)
                {
                    if (line.Contains("="))
                    {
                        var items = line.Split(new string[] { "=" }, 2, StringSplitOptions.None);
                        configs.Add(items[0], items[1]);
                    }
                }
                Func<string, float> GetFloat = (string key) =>
                {
                    if (configs.ContainsKey(key) == false) { return 0.0f; }
                    if (float.TryParse(configs[key], out var ret)) { return ret; }
                    return 0.0f;
                };
                var x = GetFloat("posx");
                var y = GetFloat("posy");
                var z = GetFloat("posz");
                var rx = GetFloat("angx");
                var ry = GetFloat("angy");
                var rz = GetFloat("angz");
                var fov = GetFloat("fov");

                await Globals.Client?.SendCommandAsync(new PipeCommands.ImportCameraPlus { x = x, y = y, z = z, rx = rx, ry = ry, rz = rz, fov = fov });
            }
        }

        private async void CameraPlus_ExportButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.ExportCameraPlus { }, r =>
            {
                var d = (PipeCommands.ReturnExportCameraPlus)r;
                Dispatcher.Invoke(() =>
                {
                    var ofd = new OpenFileDialog();
                    ofd.Filter = "cameraplus.cfg|cameraplus.cfg";
                    ofd.Title = "Select cameraplus.cfg";
                    ofd.FileName = "cameraplus.cfg";
                    if (ofd.ShowDialog() == true)
                    {
                        var culture = System.Globalization.CultureInfo.InvariantCulture;
                        var format = culture.NumberFormat;
                        var lines = File.ReadAllLines(ofd.FileName);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].StartsWith("posx")) lines[i] = $"posx=" + d.x.ToString("G", format);
                            if (lines[i].StartsWith("posy")) lines[i] = $"posy=" + d.y.ToString("G", format);
                            if (lines[i].StartsWith("posz")) lines[i] = $"posz=" + d.z.ToString("G", format);
                            if (lines[i].StartsWith("angx")) lines[i] = $"angx=" + d.rx.ToString("G", format);
                            if (lines[i].StartsWith("angy")) lines[i] = $"angy=" + d.ry.ToString("G", format);
                            if (lines[i].StartsWith("angz")) lines[i] = $"angz=" + d.rz.ToString("G", format);
                            if (lines[i].StartsWith("fov")) lines[i] = $"fov=" + d.fov.ToString("G", format);
                        }
                        File.WriteAllLines(ofd.FileName, lines);
                    }
                });
            });
        }

        private async void ExternalMotionSenderCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.EnableExternalMotionSender
            {
                enable = ExternalMotionSenderEnableCheckBox.IsChecked.Value
            });
        }

        private async void OSCApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var port = TextBoxTryParse(ExternalMotionSenderPortTextBox);
            var PeriodStatus = TextBoxTryParse(PeriodStatusTextBox);
            var PeriodRoot = TextBoxTryParse(PeriodRootTextBox);
            var PeriodBone = TextBoxTryParse(PeriodBoneTextBox);
            var PeriodBlendShape = TextBoxTryParse(PeriodBlendShapeTextBox);
            var PeriodCamera = TextBoxTryParse(PeriodCameraTextBox);
            var PeriodDevices = TextBoxTryParse(PeriodDevicesTextBox);

            if (port.HasValue && PeriodStatus.HasValue && PeriodRoot.HasValue && PeriodBone.HasValue && PeriodBlendShape.HasValue && PeriodCamera.HasValue && PeriodDevices.HasValue)
            {
                await Globals.Client?.SendCommandAsync(new PipeCommands.ChangeExternalMotionSenderAddress
                {
                    address = ExternalMotionSenderAddressTextBox.Text,
                    port = port.Value,
                    PeriodStatus = PeriodStatus.Value,
                    PeriodRoot = PeriodRoot.Value,
                    PeriodBone = PeriodBone.Value,
                    PeriodBlendShape = PeriodBlendShape.Value,
                    PeriodCamera = PeriodCamera.Value,
                    PeriodDevices = PeriodDevices.Value,
                    OptionString = OptionStringTextbox.Text,
                });
            }
        }

        private async void ExternalMotionReceiverCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.EnableExternalMotionReceiver
            {
                enable = ExternalMotionReceiverEnableCheckBox.IsChecked.Value
            });
        }

        private async void OSCReceiverApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var port = TextBoxTryParse(ExternalMotionReceiverPortTextBox);
            if (port.HasValue)
            {
                await Globals.Client?.SendCommandAsync(new PipeCommands.ChangeExternalMotionReceiverPort
                {
                    port = port.Value
                });
            }
        }

        private void MidiCCBlendShapeSettingButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new MidiCCBlendShapeSettingWIndow();
            win.ShowDialog();
        }

        private int? TextBoxTryParse(TextBox textBox)
        {
            textBox.Background = new SolidColorBrush(Colors.White);
            if (int.TryParse(textBox.Text, out int value))
            {
                return value;
            }
            else
            {
                textBox.Background = new SolidColorBrush(Colors.Pink);
                return null;
            }
        }

        private async void TrackingFilterEnable_Changed(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.EnableTrackingFilter
            {
                globalEnable = TrackingFilterEnable.IsChecked.Value,
                hmdEnable = TrackingFilterHmdEnable.IsChecked.Value,
                controllerEnable = TrackingFilterControllerEnable.IsChecked.Value,
                trackerEnable = TrackingFilterTrackerEnable.IsChecked.Value,
            });
        }

        private async void FixKneeRotationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.EnableModelModifier
            {
                fixKneeRotation = FixKneeRotationCheckBox.IsChecked.Value,
            });
        }

        private async void HandleControllerAsTrackerCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.EnableHandleControllerAsTracker
            {
                HandleControllerAsTracker = HandleControllerAsTrackerCheckBox.IsChecked.Value,
            });
        }

        private void AntiAliasingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AntiAliasingComboBox.SelectedItem == null) return;
            SetQualitySettings();
        }

        private async void SetQualitySettings()
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetQualitySettings
            {
                antiAliasing = (int)AntiAliasingComboBox.SelectedItem,
            });
        }
    }
}
