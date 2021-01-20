using System;
using System.Collections.Generic;
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
    /// VRMImportWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class VRMImportWindow : Window
    {
        public VRMImportWindow()
        {
            InitializeComponent();
            if (VRoidHubWindow.IncludeVRoidHubWindow == false)
            {
                ShowVRoidHubButton.Visibility = Visibility.Collapsed;
            }
            if (DMMVRConnectWindow.IncludeDMMVRConnectWindow == false)
            {
                ShowDMMVRConnectButton.Visibility = Visibility.Collapsed;
            }
        }

        private VRMData CurrentMeta = null;

        private async void LoadVRMButton_Click(object sender, RoutedEventArgs e)
        {
            Globals.LoadCommonSettings();

            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "VRM File(*.vrm)|*.vrm";
            ofd.InitialDirectory = Globals.ExistDirectoryOrNull(Globals.CurrentCommonSettingsWPF.CurrentPathOnVRMFileDialog);

            if (ofd.ShowDialog() == true)
            {
                await Globals.Client.SendCommandWaitAsync(new PipeCommands.LoadVRM { Path = ofd.FileName }, d =>
                {
                    var ret = (PipeCommands.ReturnLoadVRM)d;
                    Dispatcher.Invoke(() => LoadMetaData(ret.Data));
                });
                if (Globals.CurrentCommonSettingsWPF.CurrentPathOnVRMFileDialog != System.IO.Path.GetDirectoryName(ofd.FileName))
                {
                    Globals.CurrentCommonSettingsWPF.CurrentPathOnVRMFileDialog = System.IO.Path.GetDirectoryName(ofd.FileName);
                    Globals.SaveCommonSettings();
                }
            }
        }

        private void LoadMetaData(VRMData meta)
        {
            if (meta != null)
            {
                CurrentMeta = meta;
                this.DataContext = meta;
                ImportButton.IsEnabled = true;
                IgnoreButton.IsEnabled = true;
                if (meta.ThumbnailPNGBytes != null)
                {
                    using (var ms = new MemoryStream(meta.ThumbnailPNGBytes))
                    {
                        var imageSource = new BitmapImage();
                        imageSource.BeginInit();
                        imageSource.CacheOption = BitmapCacheOption.OnLoad;
                        imageSource.StreamSource = ms;
                        imageSource.EndInit();
                        ThumbnailImage.Source = imageSource;
                    }
                }
                else
                {
                    ThumbnailImage.Source = null;
                }
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentMeta == null) return;
            Globals.CurrentVRMFilePath = CurrentMeta.FilePath;
            await Globals.Client.SendCommandAsync(new PipeCommands.ImportVRM { Path = CurrentMeta.FilePath, ImportForCalibration = false, EnableNormalMapFix = EnableNormalMapFixCheckBox.IsChecked.Value, DeleteHairNormalMap = DeleteHairNormalMapCheckBox.IsChecked.Value });

            this.Close();
        }

        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMetaData(new VRMData());
            ImportButton.IsEnabled = false;
            IgnoreButton.IsEnabled = false;
        }

        private void ShowVRoidHubButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new VRoidHubWindow();
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                this.Close();
            }
        }

        private void ShowDMMVRConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new DMMVRConnectWindow();
            win.Owner = this;
            if (win.ShowDialog() == true)
            {
                this.Close();
            }
        }
    }
}
