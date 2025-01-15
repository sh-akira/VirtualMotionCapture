using System;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using UnityMemoryMappedFile;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<string> DefaultFaces = new ObservableCollection<string> {
            "通常(NEUTRAL)",
            "喜(JOY)",
            "怒(ANGRY)",
            "哀(SORROW)",
            "楽(FUN)",
            "上見(LOOKUP)",
            "下見(LOOKDOWN)",
            "左見(LOOKLEFT)",
            "右見(LOOKRIGHT)",
        };
        private ObservableCollection<string> DefaultFacesBase = new ObservableCollection<string> {
            "NEUTRAL",
            "JOY",
            "ANGRY",
            "SORROW",
            "FUN",
            "LOOKUP",
            "LOOKDOWN",
            "LOOKLEFT",
            "LOOKRIGHT",
        };

        private ObservableCollection<string> LipSyncDevices = new ObservableCollection<string>();

        private int CurrentWindowNum = 0;
        CalibrationResultWindow calibrationResultWindow;

        public MainWindow()
        {
            if (App.CommandLineArgs != null && App.CommandLineArgs.Length == 1 && App.CommandLineArgs.First() == "/firewall")
            {
                var firewallManagerWindow = new FirewallManagerWindow();
                firewallManagerWindow.Show();
                this.Close();
                return;
            }
            if (App.CommandLineArgs == null || App.CommandLineArgs.Length < 2 || App.CommandLineArgs.First().StartsWith("/pipeName") == false)
            {
                this.Close();
                return;
            }
            InitializeComponent();
            Globals.Connect(App.CommandLineArgs[1]);
            Globals.Client.ReceivedEvent += Client_Received;
            DefaultFaceComboBox.ItemsSource = DefaultFacesBase;
            LipSyncDeviceComboBox.ItemsSource = LipSyncDevices;
            UpdateWindowTitle();
            Globals.LoadCommonSettings();
        }

        private void UpdateWindowTitle()
        {
            Title = $"{LanguageSelector.Get("MainWindowTitle")} ({(CurrentWindowNum == 0 ? LanguageSelector.Get("MainWindowTitleLoading") : CurrentWindowNum.ToString())})";
        }

        private void CheckVMCCameraVersion()
        {
            var directory = @"C:\VMC_Camera\";
            var path32 = directory + "VMC_CameraFilter32bit.dll";
            var path64 = directory + "VMC_CameraFilter64bit.dll";
            var path32_old = directory + "VMC_CameraFilter32bit_old.dll";
            var path64_old = directory + "VMC_CameraFilter64bit_old.dll";

            if (Directory.Exists(directory) == false)
            {
                return;
            }

            bool needUpdate = false;
            if (File.Exists(path32))
            {
                var fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(path32);
                if (string.IsNullOrWhiteSpace(fileVersionInfo.ProductVersion))
                {
                    needUpdate = true;
                }
            }

            if (File.Exists(path64))
            {
                var fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(path64);
                if (string.IsNullOrWhiteSpace(fileVersionInfo.ProductVersion))
                {
                    needUpdate = true;
                }
            }

            if (needUpdate)
            {
                try
                {
                    if (File.Exists(path32)) File.Move(path32, path32_old);
                    if (File.Exists(path64)) File.Move(path64, path64_old);
                }
                catch (Exception)
                {
                    return;
                }
                try
                {
                    File.Copy(Globals.GetCurrentAppDir() + @"VMC_Camera\VMC_CameraFilter32bit.dll", path32, true);
                    File.Copy(Globals.GetCurrentAppDir() + @"VMC_Camera\VMC_CameraFilter64bit.dll", path64, true);
                }
                catch (Exception)
                {
                    MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedFileCopy"), "VMC_Camera update failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show(LanguageSelector.Get("SettingWindow_SuccessDriverInstall"), "VMC_Camera update");
            }

            if (File.Exists(path32_old))
            {
                try
                {
                    File.Delete(path32_old);
                }
                catch (Exception)
                {
                }
            }
            if (File.Exists(path64_old))
            {
                try
                {
                    File.Delete(path64_old);
                }
                catch (Exception)
                {
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Globals.CurrentCommonSettingsWPF.FirewallChecked == false && App.CommandLineArgs[1] != "VMCTest")
            {
                var (successExecute, exitCode) = AdminExecute.RestartAsAdmin(new[] { "/firewall" });
                if (successExecute)
                {
                    if (exitCode == 0)
                    {
                        Globals.CurrentCommonSettingsWPF.FirewallChecked = true;
                        Globals.SaveCommonSettings();
                    }
                }
            }

            calibrationResultWindow = new CalibrationResultWindow { Owner = this, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen };

            CheckVMCCameraVersion();

            while (Globals.Client.IsConnected != true)
            {
                await Task.Delay(100);
            }
#if BETA && FANBOX
            await Globals.Client.SendCommandAsync(new PipeCommands.SetIsBeta { IsBeta = true , IsPreRelease = true});
#elif FANBOX
            await Globals.Client.SendCommandAsync(new PipeCommands.SetIsBeta { IsBeta = false , IsPreRelease = true});

            //thanks 1000 supporters
            var imgpath = System.IO.Path.Combine(Globals.GetCurrentAppDir(), "startup.png");
            if (System.IO.File.Exists(imgpath))
            {
                var win = new Window();
                win.SizeToContent = SizeToContent.WidthAndHeight;
                win.ResizeMode = ResizeMode.NoResize;
                var image = new Image();
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.CreateOptions = BitmapCreateOptions.None;
                bitmapImage.UriSource = new Uri(imgpath);
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                image.Source = bitmapImage;
                image.Width = bitmapImage.PixelWidth;
                image.Height = bitmapImage.PixelHeight;
                var grid = new Grid();
                grid.Children.Add(image);
                var button = new Button();
                button.Content = "閉じる/Close";
                button.FontSize = 24;
                button.Height = double.NaN;
                button.Padding = new Thickness(10);
                button.Margin = new Thickness(10);
                button.Click += (csender, ce) => win.Close();
                button.HorizontalAlignment = HorizontalAlignment.Right;
                button.VerticalAlignment = VerticalAlignment.Bottom;
                grid.Children.Add(button);
                win.Content = grid;
                win.Closed += (wsender, we) => System.IO.File.Delete(imgpath);
                win.Show();
            }
#endif
            if (VRoidHubWindow.IncludeVRoidHubWindow)
            {
                VRoidHubWindow.IncludeVRoidHubWindow = false;
                await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetModIsLoaded(), d =>
                {
                    var data = (PipeCommands.ReturnModIsLoaded)d;
                    if (data.IsLoaded)
                    {
                        VRoidHubWindow.IncludeVRoidHubWindow = false;
                    }
                    else
                    {
                        VRoidHubWindow.IncludeVRoidHubWindow = true;
                    }
                });
            }
            await GetLipSyncDevice();
            await Globals.Client.SendCommandAsync(new PipeCommands.LoadCurrentSettings());
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    await Globals.Client?.SendCommandAsync(new PipeCommands.ExitControlPanel { });
                }
                catch { }
            });

            Application.Current?.Windows?.Cast<Window>()?.ToList()?.ForEach(d => { if (d != this) d?.Close(); });
            Globals.Client.Dispose();

            if (lastLog != null)
            {
                //エラーカウント超過による自動クローズの場合にメッセージを出す
                if (lastLog.errorCount >= PipeCommands.ErrorCountMax)
                {
                    MessageBox.Show(lastLog.condition, LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SilentChangeChecked(CheckBox checkBox, bool enable, RoutedEventHandler checkedHandler, RoutedEventHandler uncheckedHandler)
        {
            checkBox.Checked -= checkedHandler;
            checkBox.Unchecked -= uncheckedHandler;
            checkBox.IsChecked = enable;
            checkBox.Unchecked += uncheckedHandler;
            checkBox.Checked += checkedHandler;
        }

        private bool IsSliderSetting = false;

        private void LoadSlider(float setvalue, float multiply, Slider slider, RoutedPropertyChangedEventHandler<double> valueChanged)
        {
            IsSliderSetting = true;
            var min = (int)slider.Minimum;
            var max = (int)slider.Maximum;
            int value = (int)Math.Round(setvalue * multiply);
            if (value < min) value = min;
            if (value > max) value = max;
            slider.Value = value;
            IsSliderSetting = false;
        }

        private float oldSliderValue = -1;

        private async Task SliderValueChanged(object slider, TextBlock textBlock, float multiple, PipeCommands.SetFloatValueBase command, bool isSliderSetting)
        {
            if (textBlock == null) return;
            float value = (float)(slider as Slider).Value / multiple;
            if (oldSliderValue == value) return;
            oldSliderValue = value;
            textBlock.Text = multiple == 1.0f ? value.ToString() : value.ToString("#." + multiple.ToString().Substring(1));
            command.value = value;
            if (isSliderSetting == false && Globals.Client != null) await Globals.Client.SendCommandAsync(command);
        }

        private PipeCommands.LogNotify lastLog = null;

        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(async () =>
            {
                //"設定"
                if (e.CommandType == typeof(PipeCommands.LoadVRMPath))
                {
                    var d = (PipeCommands.LoadVRMPath)e.Data;
                    Globals.CurrentVRMFilePath = d.Path;
                } 
                else if (e.CommandType == typeof(PipeCommands.LoadControllerTouchPadPoints))
                {
                    var d = (PipeCommands.LoadControllerTouchPadPoints)e.Data;
                    Globals.LeftControllerPoints = d.LeftPoints;
                    Globals.LeftControllerCenterEnable = d.LeftCenterEnable;
                    Globals.RightControllerPoints = d.RightPoints;
                    Globals.RightControllerCenterEnable = d.RightCenterEnable;
                }
                else if (e.CommandType == typeof(PipeCommands.LoadControllerStickPoints))
                {
                    var d = (PipeCommands.LoadControllerStickPoints)e.Data;
                    Globals.LeftControllerStickPoints = d.LeftPoints;
                    Globals.RightControllerStickPoints = d.RightPoints;
                }
                else if (e.CommandType == typeof(PipeCommands.LoadSkeletalInputEnable))
                {
                    var d = (PipeCommands.LoadSkeletalInputEnable)e.Data;
                    Globals.EnableSkeletal = d.enable;
                }
                else if (e.CommandType == typeof(PipeCommands.LoadKeyActions))
                {
                    var d = (PipeCommands.LoadKeyActions)e.Data;
                    Globals.KeyActions = d.KeyActions;
                }
                else if (e.CommandType == typeof(PipeCommands.SetHandFreeOffset))
                {
                    var d = (PipeCommands.SetHandFreeOffset)e.Data;
                    Globals.FreeOffset.SetFromPipeCommands(d);
                }
                //"背景色"
                else if (e.CommandType == typeof(PipeCommands.LoadCustomBackgroundColor))
                {
                    var d = (PipeCommands.LoadCustomBackgroundColor)e.Data;
                    customColor = Color.FromArgb(255, (byte)(d.r * 255f), (byte)(d.g * 255f), (byte)(d.b * 255f));
                    ColorCustomButton.Background = new SolidColorBrush(customColor);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadIsTopMost))
                {
                    var d = (PipeCommands.LoadIsTopMost)e.Data;
                    SilentChangeChecked(TopMostCheckBox, d.enable, TopMostCheckBox_Checked, TopMostCheckBox_Unchecked);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadHideBorder))
                {
                    var d = (PipeCommands.LoadHideBorder)e.Data;
                    SilentChangeChecked(WindowBorderCheckBox, d.enable, WindowBorderCheckBox_Checked, WindowBorderCheckBox_Unchecked);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadSetWindowClickThrough))
                {
                    var d = (PipeCommands.LoadSetWindowClickThrough)e.Data;
                    SilentChangeChecked(WindowClickThroughCheckBox, d.enable, WindowClickThroughCheckBox_Checked, WindowClickThroughCheckBox_Unchecked);
                }
                //"カメラ"
                else if (e.CommandType == typeof(PipeCommands.LoadShowCameraGrid))
                {
                    var d = (PipeCommands.LoadShowCameraGrid)e.Data;
                    SilentChangeChecked(CameraGridCheckBox, d.enable, CameraGridCheckBox_Checked, CameraGridCheckBox_Unchecked);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadCameraMirror))
                {
                    var d = (PipeCommands.LoadCameraMirror)e.Data;
                    SilentChangeChecked(CameraMirrorCheckBox, d.enable, CameraMirrorCheckBox_Checked, CameraMirrorCheckBox_Unchecked);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadCameraFOV))
                {
                    var d = (PipeCommands.LoadCameraFOV)e.Data;
                    LoadSlider(d.fov, 1.0f, FOVSlider, FOVSlider_ValueChanged);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadCameraSmooth))
                {
                    var d = (PipeCommands.LoadCameraSmooth)e.Data;
                    LoadSlider(d.speed, 1.0f, CameraSmoothSlider, CameraSmoothSlider_ValueChanged);
                }
                //"リップシンク"
                else if (e.CommandType == typeof(PipeCommands.LoadLipSyncEnable))
                {
                    var d = (PipeCommands.LoadLipSyncEnable)e.Data;
                    SilentChangeChecked(LipSyncCheckBox, d.enable, LipSyncCheckBox_Checked, LipSyncCheckBox_Unchecked);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadLipSyncMaxWeightEnable))
                {
                    var d = (PipeCommands.LoadLipSyncMaxWeightEnable)e.Data;
                    SilentChangeChecked(MaxWeightCheckBox, d.enable, MaxWeightCheckBox_Checked, MaxWeightCheckBox_Unchecked);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadLipSyncMaxWeightEmphasis))
                {
                    var d = (PipeCommands.LoadLipSyncMaxWeightEmphasis)e.Data;
                    SilentChangeChecked(MaxWeightEmphasisCheckBox, d.enable, MaxWeightEmphasisCheckBox_Checked, MaxWeightEmphasisCheckBox_Unchecked);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadLipSyncDevice))
                {
                    var d = (PipeCommands.LoadLipSyncDevice)e.Data;
                    LoadLipSyncDevice(d.device);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadLipSyncGain))
                {
                    var d = (PipeCommands.LoadLipSyncGain)e.Data;
                    LoadSlider(d.gain, 10.0f, GainSlider, GainSlider_ValueChanged);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadLipSyncWeightThreashold))
                {
                    var d = (PipeCommands.LoadLipSyncWeightThreashold)e.Data;
                    LoadSlider(d.threashold, 1000.0f, WeightThreasholdSlider, WeightThreasholdSlider_ValueChanged);
                }
                //"表情制御"
                else if (e.CommandType == typeof(PipeCommands.LoadAutoBlinkEnable))
                {
                    var d = (PipeCommands.LoadAutoBlinkEnable)e.Data;
                    SilentChangeChecked(AutoBlinkCheckBox, d.enable, AutoBlinkCheckBox_Checked, AutoBlinkCheckBox_Unchecked);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadDefaultFace))
                {
                    var d = (PipeCommands.LoadDefaultFace)e.Data;
                    if (string.IsNullOrEmpty(d.face)) return;
                    if (DefaultFaces.Contains(d.face) == false)
                    {
                        DefaultFaces.Insert(0, d.face);
                        DefaultFacesBase.Insert(0, d.face);
                    }
                    DefaultFaceComboBox.SelectionChanged -= DefaultFaceComboBox_SelectionChanged;
                    DefaultFaceComboBox.SelectedIndex = DefaultFaces.IndexOf(d.face);
                    DefaultFaceComboBox.SelectionChanged += DefaultFaceComboBox_SelectionChanged;
                }
                else if (e.CommandType == typeof(PipeCommands.LoadBlinkTimeMin))
                {
                    var d = (PipeCommands.LoadBlinkTimeMin)e.Data;
                    LoadSlider(d.time, 10.0f, BlinkTimeMinSlider, BlinkTimeMinSlider_ValueChanged);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadBlinkTimeMax))
                {
                    var d = (PipeCommands.LoadBlinkTimeMax)e.Data;
                    LoadSlider(d.time, 10.0f, BlinkTimeMaxSlider, BlinkTimeMaxSlider_ValueChanged);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadCloseAnimationTime))
                {
                    var d = (PipeCommands.LoadCloseAnimationTime)e.Data;
                    LoadSlider(d.time, 100.0f, CloseAnimationTimeSlider, CloseAnimationTimeSlider_ValueChanged);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadOpenAnimationTime))
                {
                    var d = (PipeCommands.LoadOpenAnimationTime)e.Data;
                    LoadSlider(d.time, 100.0f, OpenAnimationTimeSlider, OpenAnimationTimeSlider_ValueChanged);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadClosingTime))
                {
                    var d = (PipeCommands.LoadClosingTime)e.Data;
                    LoadSlider(d.time, 100.0f, ClosingTimeSlider, ClosingTimeSlider_ValueChanged);
                }
                else if (e.CommandType == typeof(PipeCommands.SetAutoEyeMovementConfig))
                {
                    var d = (PipeCommands.SetAutoEyeMovementConfig)e.Data;
                    SilentChangeChecked(EyeFluctuationCheckBox, d.FluctuationEnable, EyeFluctuationCheckBox_Checked, EyeFluctuationCheckBox_Checked);
                    SilentChangeChecked(AutoLookCameraCheckBox, d.AutoLookCameraEnable, EyeFluctuationCheckBox_Checked, EyeFluctuationCheckBox_Checked);
                }
                else if (e.CommandType == typeof(PipeCommands.SetLightAngle))
                {
                    var d = (PipeCommands.SetLightAngle)e.Data;
                    LoadSlider(d.X, 1.0f, LightXSlider, LightSlider_ValueChanged);
                    LoadSlider(d.Y, 1.0f, LightYSlider, LightSlider_ValueChanged);
                }
                else if (e.CommandType == typeof(PipeCommands.ChangeLightColor))
                {
                    var d = (PipeCommands.ChangeLightColor)e.Data;
                    LightColorButton.Background = new SolidColorBrush(Color.FromArgb((byte)(d.a * 255f), (byte)(d.r * 255f), (byte)(d.g * 255f), (byte)(d.b * 255f)));
                }
                else if (e.CommandType == typeof(PipeCommands.SetWindowNum))
                {
                    var d = (PipeCommands.SetWindowNum)e.Data;
                    CurrentWindowNum = d.Num;
                    UpdateWindowTitle();
                }
                // "VMC本体終了"
                else if (e.CommandType == typeof(PipeCommands.QuitApplication))
                {
                    Application.Current.Shutdown();
                }
                //for Debug
                else if (e.CommandType == typeof(PipeCommands.KeyDown))
                {
                    var d = (PipeCommands.KeyDown)e.Data;
                    logKeyConfig(d.Config, true);
                }
                else if (e.CommandType == typeof(PipeCommands.KeyUp))
                {
                    var d = (PipeCommands.KeyUp)e.Data;
                    logKeyConfig(d.Config, false);
                }
                else if (e.CommandType == typeof(PipeCommands.LogNotify))
                {
                    var d = (PipeCommands.LogNotify)e.Data;

                    switch (d.type) {
                        case NotifyLogTypes.Error:
                        case NotifyLogTypes.Assert:
                        case NotifyLogTypes.Exception:
                            UnityLogStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 102, 102));
                            UnityLogStatusTextBlock.Text = "[" + d.type.ToString() + ":" + d.errorCount + "] " + d.condition;
                            break;
                        case NotifyLogTypes.Warning:
                            UnityLogStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0));
                            UnityLogStatusTextBlock.Text = "[" + d.type.ToString() + "] " + d.condition;
                            return;
                        default:
                            UnityLogStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238));
                            UnityLogStatusTextBlock.Text = "[" + d.type.ToString() + "] " + d.condition;
                            return;
                    }

                    lastLog = d;
                }
                else if (e.CommandType == typeof(PipeCommands.ShowCalibrationWindow))
                {
                    CalibrationButton_Click(null, null);
                }
                else if (e.CommandType == typeof(PipeCommands.ShowPhotoWindow))
                {
                    PhotoButton_Click(null, null);
                }
                else if (e.CommandType == typeof(PipeCommands.VRMLoadStatus))
                {
                    var d = (PipeCommands.VRMLoadStatus)e.Data;
                    CalibrationButton.IsEnabled = d.Valid;
                }
                else if (e.CommandType == typeof(PipeCommands.ChangeCamera))
                {
                    var d = (PipeCommands.ChangeCamera)e.Data;

                    FrontCameraButton.IsEnabled = true;
                    BackCameraButton.IsEnabled = true;
                    FreeCameraButton.IsEnabled = true;
                    PositionFixedCameraButton.IsEnabled = true;

                    switch (d.type) {
                        case CameraTypes.Front: FrontCameraButton.IsEnabled = false; break;
                        case CameraTypes.Back: BackCameraButton.IsEnabled = false; break;
                        case CameraTypes.Free: FreeCameraButton.IsEnabled = false; break;
                        case CameraTypes.PositionFixed: PositionFixedCameraButton.IsEnabled = false; break;
                        default: break;
                    }

                }
                else if (e.CommandType == typeof(PipeCommands.CalibrationResult))
                {
                    var d = (PipeCommands.CalibrationResult)e.Data;
                    calibrationResultWindow.Update(d);
                }
                else if (e.CommandType == typeof(PipeCommands.OpenVRStatus))
                {
                    var d = (PipeCommands.OpenVRStatus)e.Data;
                    OpenVRAlertStatusTextBlock.Visibility = d.DashboardOpened? Visibility.Visible : Visibility.Collapsed;
                }
            }));
        }

        private void logKeyConfig(KeyConfig key, bool isDown)
        {
            var updown = isDown ? "Down" : "Up  ";
            var leftright = key.isLeft ? "Left " : "Right";
            var facehand = key.actionType == KeyActionTypes.Face ? "Face" : "Hand";
            var type = key.type == KeyTypes.Controller ? "Controller" : key.type == KeyTypes.Keyboard ? "Keyboard  " : key.type == KeyTypes.Midi ? "Midi " : key.type == KeyTypes.MidiCC ? "MIDI CC " : "Mouse     ";
            System.Diagnostics.Debug.WriteLine($"Key{updown} {facehand} {leftright} {type} {key.keyCode} {key.keyIndex}");
        }

#region "設定"

        private async void LoadSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Globals.LoadCommonSettings();

            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Setting File(*.json)|*.json";
            ofd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnSettingFileDialog);
            if (ofd.ShowDialog() == true)
            {
                await Globals.Client.SendCommandAsync(new PipeCommands.LoadSettings { Path = ofd.FileName });

                if (Globals.CurrentCommonSettingsWPF.CurrentPathOnSettingFileDialog != System.IO.Path.GetDirectoryName(ofd.FileName))
                {
                    Globals.CurrentCommonSettingsWPF.CurrentPathOnSettingFileDialog = System.IO.Path.GetDirectoryName(ofd.FileName);
                    Globals.SaveCommonSettings();
                }
            }
        }

        private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Globals.LoadCommonSettings();

            var sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.Filter = "Setting File(*.json)|*.json";
            sfd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnSettingFileDialog);
            if (sfd.ShowDialog() == true)
            {
                await Globals.Client.SendCommandAsync(new PipeCommands.SaveSettings { Path = sfd.FileName });

                if (Globals.CurrentCommonSettingsWPF.CurrentPathOnSettingFileDialog != System.IO.Path.GetDirectoryName(sfd.FileName))
                {
                    Globals.CurrentCommonSettingsWPF.CurrentPathOnSettingFileDialog = System.IO.Path.GetDirectoryName(sfd.FileName);
                    Globals.SaveCommonSettings();
                }
            }
        }

        private void ImportVRMButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new VRMImportWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private bool existCalibrationWindow = false;
        private void CalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            if (existCalibrationWindow) {
                return;
            }

            existCalibrationWindow = true;
            if (string.IsNullOrWhiteSpace(Globals.CurrentVRMFilePath))
            {
                MessageBox.Show(LanguageSelector.Get("MainWindow_ErrorCalibration"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                existCalibrationWindow = false;
                return;
            }
            var win = new CalibrationWindow();
            win.Owner = this;
            win.ShowDialog();
            existCalibrationWindow = false;
        }

        private void ShortcutKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new ShortcutKeyWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingWindow();
            win.Owner = this;
            win.ShowDialog();
            UpdateWindowTitle();
        }

#endregion

#region "背景色"

        private async void ColorGreenButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.ChangeBackgroundColor { r = 0.0f, g = 1.0f, b = 0.0f, isCustom = false });
        }

        private async void ColorBlueButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.ChangeBackgroundColor { r = 0.0f, g = 0.0f, b = 1.0f, isCustom = false });
        }

        private async void ColorWhiteButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.ChangeBackgroundColor { r = 0.9375f, g = 0.9375f, b = 0.9375f, isCustom = false });
        }

        private Color customColor = Color.FromArgb(255, 174, 212, 255);

        private async void ColorCustomButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.ChangeBackgroundColor { r = customColor.R / 255f, g = customColor.G / 255f, b = customColor.B / 255f, isCustom = true });
        }

        private void ColorCustomButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dialog = new System.Windows.Forms.ColorDialog();
            dialog.AllowFullOpen = true;
            dialog.AnyColor = true;
            dialog.Color = System.Drawing.Color.FromArgb(customColor.A, customColor.R, customColor.G, customColor.B);
            dialog.FullOpen = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                customColor = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                ColorCustomButton.Background = new SolidColorBrush(customColor);
            }
        }

        private async void ColorTransparentButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowBorderCheckBox.IsChecked == false) WindowBorderCheckBox.IsChecked = true;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetBackgroundTransparent());
        }

        private async void TopMostCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetWindowTopMost { enable = true });
        }

        private async void TopMostCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetWindowTopMost { enable = false });
        }

        private async void WindowBorderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetWindowBorder { enable = true });
        }

        private async void WindowBorderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetWindowBorder { enable = false });
        }

        private async void WindowClickThroughCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetWindowClickThrough { enable = true });
        }

        private async void WindowClickThroughCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetWindowClickThrough { enable = false });
        }

#endregion

#region "カメラ"

        private async void FrontCameraButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.ChangeCamera { type = CameraTypes.Front });
        }

        private async void BackCameraButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.ChangeCamera { type = CameraTypes.Back });
        }

        private async void FreeCameraButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.ChangeCamera { type = CameraTypes.Free });
        }

        private async void PositionFixedCameraButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.ChangeCamera { type = CameraTypes.PositionFixed });
        }

        private async void CameraGridCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetGridVisible { enable = true });
        }

        private async void CameraGridCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetGridVisible { enable = false });
        }

        private async void CameraMirrorCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetCameraMirror { enable = true });
        }

        private async void CameraMirrorCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetCameraMirror { enable = false });
        }

        private async void FOVSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await SliderValueChanged(FOVSlider, FOVTextBlock, 1.0f, new PipeCommands.SetCameraFOV(), IsSliderSetting);
        }

        private async void CameraSmoothSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await SliderValueChanged(CameraSmoothSlider, CameraSmoothTextBlock, 1.0f, new PipeCommands.SetCameraSmooth(), IsSliderSetting);
        }

        private bool existPhotoWindow = false;
        private void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (existPhotoWindow) {
                return;
            }

            existPhotoWindow = true;
            var win = new PhotoWindow();
            win.Owner = this;
            if (win.ShowDialog() == true)
            {

            }
            existPhotoWindow = false;
        }

#endregion

#region "リップシンク"

        private async void LipSyncCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetLipSyncEnable { enable = true });
        }

        private async void LipSyncCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetLipSyncEnable { enable = false });
        }

        private async void MaxWeightCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetLipSyncMaxWeightEnable { enable = true });
        }

        private async void MaxWeightCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetLipSyncMaxWeightEnable { enable = false });
        }

        private async void MaxWeightEmphasisCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetLipSyncMaxWeightEmphasis { enable = true });
        }

        private async void MaxWeightEmphasisCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetLipSyncMaxWeightEmphasis { enable = false });
        }

        private async void LipSyncDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LipsyncTabTextBlock.Background = new SolidColorBrush(Colors.PaleVioletRed);
            if (LipSyncDeviceComboBox.SelectedItem == null) return;
            if (LipSyncDeviceComboBox.SelectedItem.ToString().StartsWith(LanguageSelector.Get("Error") + ":")) return;
            LipsyncTabTextBlock.Background = new SolidColorBrush(Colors.Transparent);
            await Globals.Client.SendCommandAsync(new PipeCommands.SetLipSyncDevice { device = LipSyncDeviceComboBox.SelectedItem.ToString() });
        }

        private async void LipSyncDeviceRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await GetLipSyncDevice();
        }

        private async Task GetLipSyncDevice()
        {
            LipSyncDeviceComboBox.SelectionChanged -= LipSyncDeviceComboBox_SelectionChanged;
            await Globals.Client.SendCommandWaitAsync(new PipeCommands.GetLipSyncDevices(), d =>
            {
                Dispatcher.Invoke(() =>
                {
                    LipsyncTabTextBlock.Background = new SolidColorBrush(Colors.PaleVioletRed);
                    var ret = (PipeCommands.ReturnGetLipSyncDevices)d;
                    var selectedItem = LipSyncDeviceComboBox.SelectedItem;
                    LipSyncDevices.Clear();
                    if (ret.Devices != null)
                    {
                        ret.Devices.ToList().ForEach(LipSyncDevices.Add);
                        if (selectedItem != null) LoadLipSyncDevice(selectedItem.ToString());
                    }
                    LipSyncDeviceComboBox.SelectionChanged += LipSyncDeviceComboBox_SelectionChanged;
                });
            });
        }

        void LoadLipSyncDevice(string device)
        {
            if (string.IsNullOrEmpty(device)) return;
            if (LipSyncDevices.Contains(device))
            {
                LipSyncDeviceComboBox.SelectedItem = device;
                LipsyncTabTextBlock.Background = new SolidColorBrush(Colors.Transparent);
            }
            else
            {
                LipSyncDevices.Insert(0, device.StartsWith(LanguageSelector.Get("Error") + ":") ? device : LanguageSelector.Get("Error") + ":" + device);
            }
        }

        private async void GainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await SliderValueChanged(GainSlider, GainTextBlock, 10.0f, new PipeCommands.SetLipSyncGain(), IsSliderSetting);
        }

        private async void WeightThreasholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await SliderValueChanged(WeightThreasholdSlider, WeightThreasholdTextBlock, 1000.0f, new PipeCommands.SetLipSyncWeightThreashold(), IsSliderSetting);
        }

#endregion

#region "表情制御"

        private async void AutoBlinkCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetAutoBlinkEnable { enable = true });
        }

        private async void AutoBlinkCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetAutoBlinkEnable { enable = false });
        }

        private async void DefaultFaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DefaultFaceComboBox.SelectedItem == null) return;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetDefaultFace { face = DefaultFaces[DefaultFaceComboBox.SelectedIndex] });
        }

        private async void BlinkTimeMinSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await SliderValueChanged(sender, BlinkTimeMinTextBlock, 10.0f, new PipeCommands.SetBlinkTimeMin(), IsSliderSetting);
        }

        private async void BlinkTimeMaxSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await SliderValueChanged(sender, BlinkTimeMaxTextBlock, 10.0f, new PipeCommands.SetBlinkTimeMax(), IsSliderSetting);
        }

        private async void CloseAnimationTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await SliderValueChanged(sender, CloseAnimationTimeTextBlock, 100.0f, new PipeCommands.SetCloseAnimationTime(), IsSliderSetting);
        }

        private async void OpenAnimationTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await SliderValueChanged(sender, OpenAnimationTimeTextBlock, 100.0f, new PipeCommands.SetOpenAnimationTime(), IsSliderSetting);
        }

        private async void ClosingTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await SliderValueChanged(sender, ClosingTimeTextBlock, 100.0f, new PipeCommands.SetClosingTime(), IsSliderSetting);
        }


        private async void EyeFluctuationCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.SetAutoEyeMovementConfig {  FluctuationEnable = EyeFluctuationCheckBox.IsChecked.Value, AutoLookCameraEnable = AutoLookCameraCheckBox.IsChecked.Value });
        }

#endregion

        private async void LightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LightXSlider == null || LightYSlider == null || Globals.Client == null) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetLightAngle { X = (float)LightXSlider.Value, Y = (float)LightYSlider.Value });
        }

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

        private void GraphicsOptionButton_Click(object sender, RoutedEventArgs e)
        {
            //重複ウィンドウを許容しない
            if (!Application.Current.Windows.OfType<GraphicsOptionWindow>().Any()) {
                var win = new GraphicsOptionWindow();
                win.Owner = this;
                win.Show();
            }
        }

        private void StatusBar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lastLog != null)
            {
                string trace = "[" + lastLog.type.ToString() + "] " + lastLog.condition + "\n" + lastLog.stackTrace;
                try
                {
                    Clipboard.Clear();
                }
                catch (System.Runtime.InteropServices.COMException) { }
                try
                {
                    Clipboard.SetDataObject(trace, true);
                }
                catch (System.Runtime.InteropServices.COMException) { }
            MessageBox.Show("Trace log has copied.", "Trace log", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

        private async void CameraFrontResetButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandAsync(new PipeCommands.ResetCamera { });
        }
    }
}
