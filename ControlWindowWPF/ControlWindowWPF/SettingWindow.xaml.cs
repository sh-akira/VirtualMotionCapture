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
#if FREE
            (FanboxExternalMotionSenderGroupBox.Parent as StackPanel).Children.Remove(FanboxExternalMotionSenderGroupBox);
#elif FANBOX
            (FreeExternalMotionSenderGroupBox.Parent as StackPanel).Children.Remove(FreeExternalMotionSenderGroupBox);
#endif
        }

        private Brush WhiteBrush = new SolidColorBrush(Colors.White);
        private Brush ActiveBrush = new SolidColorBrush(Colors.Green);

        private ConcurrentDictionary<string, DateTime> endTime = new ConcurrentDictionary<string, DateTime>();

        private bool VMCisAlive = true;

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
            else if (e.CommandType == typeof(PipeCommands.Alive))
            {
                VMCisAlive = true;
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
            Globals.LoadCommonSettings();

            var tracker = ControllerComboBox.SelectedItem as TrackerConfigWindow.TrackerInfo;
            var ofd = new OpenFileDialog();
            ofd.Filter = "externalcamera.cfg|externalcamera.cfg";
            ofd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnExternalCameraFileDialog);
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

                if (Globals.CurrentCommonSettingsWPF.CurrentPathOnExternalCameraFileDialog != System.IO.Path.GetDirectoryName(ofd.FileName))
                {
                    Globals.CurrentCommonSettingsWPF.CurrentPathOnExternalCameraFileDialog = System.IO.Path.GetDirectoryName(ofd.FileName);
                    Globals.SaveCommonSettings();
                }
            }
        }

        private async void ExternalCameraConigExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (ControllerComboBox.SelectedItem == null)
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_SelectedItemError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Globals.LoadCommonSettings();

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
                    sfd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnExternalCameraFileDialog);

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

                        if (Globals.CurrentCommonSettingsWPF.CurrentPathOnExternalCameraFileDialog != System.IO.Path.GetDirectoryName(sfd.FileName))
                        {
                            Globals.CurrentCommonSettingsWPF.CurrentPathOnExternalCameraFileDialog = System.IO.Path.GetDirectoryName(sfd.FileName);
                            Globals.SaveCommonSettings();
                        }
                    }
                });
            });
        }

        private void TrackerConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new TrackerConfigWindow();
            win.Owner = this;
            win.ShowDialog();
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
                    ExternalMotionSenderResponderEnableCheckBox.IsChecked = data.ResponderEnable;
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
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetExternalMotionReceiverPort { }, d =>
            {
                var data = (PipeCommands.ChangeExternalMotionReceiverPort)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    ExternalMotionReceiverPortTextBox.Text = data.port.ToString();
                    ExternalMotionReceiverRequesterEnableCheckBox.IsChecked = data.RequesterEnable;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetExternalReceiveBones { }, d =>
            {
                var data = (PipeCommands.ExternalReceiveBones)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    ReceiveBonesEnableCheckBox.IsChecked = data.ReceiveBonesEnable;
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
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetVirtualMotionTracker { }, d =>
            {
                var data = (PipeCommands.SetVirtualMotionTracker)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    VirtualMotionTrackerEnableCheckBox.IsChecked = data.enable;
                    VirtualMotionTrackerNumber.Text = data.no.ToString();
                    isSetting = false;
                });
            });

            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetPauseTracking { }, d =>
            {
                var data = (PipeCommands.PauseTracking)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    PauseTrackingCheckBox.IsChecked = data.enable;
                    isSetting = false;
                });
            });

            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetMidiEnable { }, d =>
            {
                var data = (PipeCommands.MidiEnable)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    MidiEnableCheckBox.IsChecked = data.enable;
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
                WebCamEnableCheckBox.IsChecked = true; //インストールと同時にチェックを入れる
                UpdateWebCamConfig();
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
                WebCamEnableCheckBox.IsChecked = false; //アンストールと同時にチェックを外す
                UpdateWebCamConfig();
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
            win.Owner = this;
            win.ShowDialog();
        }

        private void EyeTracking_ViveProEyeSettingButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new EyeTracking_ViveProEyeSettingWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private async void CameraPlus_ImportButton_Click(object sender, RoutedEventArgs e)
        {
            Globals.LoadCommonSettings();

            var ofd = new OpenFileDialog();

            ofd.Filter = "cameraplus.cfg|cameraplus.cfg";
            ofd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnCameraPlusFileDialog);
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
                if (Globals.CurrentCommonSettingsWPF.CurrentPathOnCameraPlusFileDialog != System.IO.Path.GetDirectoryName(ofd.FileName))
                {
                    Globals.CurrentCommonSettingsWPF.CurrentPathOnCameraPlusFileDialog = System.IO.Path.GetDirectoryName(ofd.FileName);
                    Globals.SaveCommonSettings();
                }
            }
        }

        private async void CameraPlus_ExportButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.ExportCameraPlus { }, r =>
            {
                var d = (PipeCommands.ReturnExportCameraPlus)r;
                Dispatcher.Invoke(() =>
                {
                    Globals.LoadCommonSettings();
                    var ofd = new OpenFileDialog();
                    ofd.Filter = "cameraplus.cfg|cameraplus.cfg";
                    ofd.Title = "Select cameraplus.cfg";
                    ofd.FileName = "cameraplus.cfg";
                    ofd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnCameraPlusFileDialog);

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

                        if (Globals.CurrentCommonSettingsWPF.CurrentPathOnCameraPlusFileDialog != System.IO.Path.GetDirectoryName(ofd.FileName))
                        {
                            Globals.CurrentCommonSettingsWPF.CurrentPathOnCameraPlusFileDialog = System.IO.Path.GetDirectoryName(ofd.FileName);
                            Globals.SaveCommonSettings();
                        }
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
                    ResponderEnable = ExternalMotionSenderResponderEnableCheckBox.IsChecked.Value
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
                    port = port.Value,
                    RequesterEnable = ExternalMotionReceiverRequesterEnableCheckBox.IsChecked.Value
                });
            }
        }
        private async void ReceiveBonesEnableCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.ExternalReceiveBones
            {
                ReceiveBonesEnable = ReceiveBonesEnableCheckBox.IsChecked.Value
            });
        }

        private void MidiCCBlendShapeSettingButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new MidiCCBlendShapeSettingWIndow();
            win.Owner = this;
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

        private void CheckIPAddressButton_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("IP Address:");
            try
            {
                var hostname = System.Net.Dns.GetHostName();
                var addresses = System.Net.Dns.GetHostAddresses(hostname);
                foreach (var address in addresses)
                {
                    //IPv4
                    if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        sb.AppendLine(address.ToString());
                    }
                }
            }
            catch (Exception) { }

            MessageBox.Show(sb.ToString(), "IP Address", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

        private async void VirtualMotionTrackerEnableCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;

            int result = 0;
            if (VirtualMotionTrackerNumber?.Text != null)
            {
                if (!int.TryParse(VirtualMotionTrackerNumber?.Text, out result))
                {
                    result = 0;
                }
            }
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetVirtualMotionTracker
            {
                enable = VirtualMotionTrackerEnableCheckBox.IsChecked.Value,
                no = result
            });
        }
        private async void VirtualMotionTrackerSetButton_Click(object sender, RoutedEventArgs e)
        {
            int result = 0;
            if (VirtualMotionTrackerNumber?.Text != null)
            {
                if (!int.TryParse(VirtualMotionTrackerNumber?.Text, out result))
                {
                    result = 0;
                }
            }
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetVirtualMotionTracker
            {
                enable = VirtualMotionTrackerEnableCheckBox.IsChecked.Value,
                no = result
            });
        }
        private void LipTracking_ViveSettingButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new LipTracking_ViveSettingWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private async void VMTInstallButton_Click(object sender, RoutedEventArgs e)
        {
            VirtualMotionTrackerEnableCheckBox.IsChecked = true; //インストールと同時にチェックを入れる
            await SetupVirtualMotionTracker(true);
        }

        private async void VMTUninstallButton_Click(object sender, RoutedEventArgs e)
        {
            VirtualMotionTrackerEnableCheckBox.IsChecked = false; //アンイストールと同時にチェックを外す
            await SetupVirtualMotionTracker(false);
        }

        private async Task SetupVirtualMotionTracker(bool install)
        {
            var messageBoxResult = MessageBox.Show(LanguageSelector.Get("SettingWindow_VMTContinue"), LanguageSelector.Get("Confirm"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (messageBoxResult != MessageBoxResult.OK) {
                return;
            }

            if (install)
            {
                try
                {
                    var targetDirectory = @"C:\VirtualMotionTracker";
                    if (Directory.Exists(targetDirectory) == false)
                    {
                        Directory.CreateDirectory(targetDirectory);
                        DirectoryCopy(Globals.GetCurrentAppDir() + "vmt", targetDirectory, true);
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show(LanguageSelector.Get("SettingWindow_VMTFailedFolderCreate"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.SetupVirtualMotionTracker { install = install }, d =>
            {
                var data = (PipeCommands.ResultSetupVirtualMotionTracker)d;
                Dispatcher.Invoke(() =>
                {
                    if (string.IsNullOrEmpty(data.result))
                    {
                        MessageBox.Show(LanguageSelector.Get("SettingWindow_VirtualMotionTrackerInstallSuccess"), "VMT Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                        RestartSteamVRandVirtualMotionCapture();
                    }
                    else
                    {
                        MessageBox.Show(data.result, LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            });
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destDirName);

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = System.IO.Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = System.IO.Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        private async void RestartSteamVRandVirtualMotionCapture()
        {
            System.Diagnostics.Process.Start("vrmonitor://restartsystem");

            while (VMCisAlive)
            {
                await Globals.Client?.SendCommandAsync(new PipeCommands.Alive { });
                VMCisAlive = false;
                await Task.Delay(1000);
            }

            await Task.Delay(10000);

            var vmcPath_rel = Globals.GetCurrentAppDir() + @"..\VirtualMotionCapture.exe";
            var vmcPath = System.IO.Path.GetFullPath(vmcPath_rel);

            System.Diagnostics.Process.Start(vmcPath);

            await Task.Delay(1000);

            Application.Current.Shutdown();
        }

        private async void LIVExternalCameraConigExportButton_Click(object sender, RoutedEventArgs e)
        {
            Globals.LoadCommonSettings();

            var tracker = TrackersList.First(); //なんでもいい。fovだけ返してもらう
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetExternalCameraConfig { ControllerName = tracker.SerialNumber }, r =>
            {
                var d = (PipeCommands.SetExternalCameraConfig)r;
                Dispatcher.Invoke(() =>
                {
                    var sfd = new SaveFileDialog();
                    sfd.Filter = "externalcamera.cfg|externalcamera.cfg";
                    sfd.Title = "Export externalcamera.cfg";
                    sfd.FileName = "externalcamera.cfg";
                    sfd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnExternalCameraFileDialog);

                    if (sfd.ShowDialog() == true)
                    {
                        var culture = System.Globalization.CultureInfo.InvariantCulture;
                        var format = culture.NumberFormat;
                        var lines = new List<string>();
                        lines.Add($"x=" + 0f.ToString("G", format));
                        lines.Add($"y=" + 0f.ToString("G", format));
                        lines.Add($"z=" + 0f.ToString("G", format));
                        lines.Add($"rx=" + 0f.ToString("G", format));
                        lines.Add($"ry=" + 0f.ToString("G", format));
                        lines.Add($"rz=" + 0f.ToString("G", format));
                        lines.Add($"fov=" + d.fov.ToString("G", format));
                        lines.Add($"near=0.01");
                        lines.Add($"far=1000");
                        lines.Add($"disableStandardAssets=False");
                        lines.Add($"frameSkip=0");
                        File.WriteAllLines(sfd.FileName, lines);

                        if (Globals.CurrentCommonSettingsWPF.CurrentPathOnExternalCameraFileDialog != System.IO.Path.GetDirectoryName(sfd.FileName))
                        {
                            Globals.CurrentCommonSettingsWPF.CurrentPathOnExternalCameraFileDialog = System.IO.Path.GetDirectoryName(sfd.FileName);
                            Globals.SaveCommonSettings();
                        }
                    }
                });
            });
        }

        private void HandFreeOffsetButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new HandFreeOffsetWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private async void PauseTrackingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.PauseTracking { enable = true });
        }

        private async void PauseTrackingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.PauseTracking { enable = false });
        }

        private async void MidiEnableCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.MidiEnable
            {
                enable = MidiEnableCheckBox.IsChecked.Value
            });
        }

        private async void MidiEnableCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.MidiEnable
            {
                enable = MidiEnableCheckBox.IsChecked.Value
            });
        }

        private void ModSetting_Click(object sender, RoutedEventArgs e)
        {
            var win = new ModSetting();
            win.Show();
        }

        private void DebugLogButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new DebugLogWindow();
            win.Show();
        }
    }
}
