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
using UnityNamedPipe;

namespace ControlWindowWPF
{
    /// <summary>
    /// VRMImportWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class VRMImportWindow : Window
    {
        public VRMImportWindow()
        {
            InitializeComponent();
        }

        private VRMData CurrentMeta = null;

        private async void LoadVRMButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandWaitAsync(new PipeCommands.LoadVRM(), d =>
            {
                var ret = (PipeCommands.ReturnLoadVRM)d;
                Dispatcher.Invoke(() => LoadMetaData(ret.Data));
            });
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
            Globals.CurrentVRMFilePath = CurrentMeta.FilePath;
            await Globals.Client.SendCommandAsync(new PipeCommands.ImportVRM { Path = CurrentMeta.FilePath, ImportForCalibration = false });
        }

        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMetaData(new VRMData());
            ImportButton.IsEnabled = false;
            IgnoreButton.IsEnabled = false;
        }
    }
}
