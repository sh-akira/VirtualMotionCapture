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
    /// ModSetting.xaml の相互作用ロジック
    /// </summary>
    public partial class ModSetting : Window
    {
        private ObservableCollection<ModViewModel> ModList = new ObservableCollection<ModViewModel>();

        public ModSetting()
        {
            InitializeComponent();

            ModsDataGrid.ItemsSource = ModList;
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetModList(), d =>
            {
                var data = (PipeCommands.ReturnModList)d;
                Dispatcher.Invoke(() => CreateModList(data.ModList));
            });
        }

        private void CreateModList(List<ModItem> modList)
        {
            foreach(var modItem in modList)
            {
                ModList.Add(new ModViewModel(modItem));
            }
        }

        private void ModsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModsDataGrid.SelectedItem == null)
            {
                ModInstructionPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ModInstructionPanel.Visibility = Visibility.Collapsed;
                ModDescriptionPanel.DataContext = ModsDataGrid.SelectedItem as ModViewModel;
            }
        }
    }

    public class ModViewModel : ViewModelBase
    {
        public SettingCommand SettingCommand { get; set; }
        public OpenModFolderCommand OpenModFolderCommand { get; set; }

        public ModViewModel()
        {
            SettingCommand = new SettingCommand(this);
            OpenModFolderCommand = new OpenModFolderCommand(this);
        }

        public ModViewModel(ModItem modItem) : this()
        {
            Name = modItem.Name;
            Version = modItem.Version;
            Author = modItem.Author;
            AuthorURL = modItem.AuthorURL;
            Description = modItem.Description;
            PluginURL = modItem.PluginURL;
            InstanceId = modItem.InstanceId;
            AssemblyPath = modItem.AssemblyPath;

            if (PluginURL != null)
            {
                PluginURLVisibility = Visibility.Visible;
                ModNameOnlyVisibility = Visibility.Collapsed;
            }
            else
            {
                PluginURLVisibility = Visibility.Collapsed;
                ModNameOnlyVisibility = Visibility.Visible;
            }
            if (AuthorURL != null)
            {
                AuthorURLVisibility = Visibility.Visible;
                AuthorOnlyVisibility = Visibility.Collapsed;
            }
            else
            {
                AuthorURLVisibility = Visibility.Collapsed;
                AuthorOnlyVisibility = Visibility.Visible;
            }
        }

        public string Name { get => Getter<string>(); set => Setter(value); }
        public string Version { get => Getter<string>(); set => Setter(value); }
        public string Author { get => Getter<string>(); set => Setter(value); }
        public string AuthorURL { get => Getter<string>(); set => Setter(value); }
        public string Description { get => Getter<string>(); set => Setter(value); }
        public string PluginURL { get => Getter<string>(); set => Setter(value); }
        public string InstanceId { get => Getter<string>(); set => Setter(value); }
        public string AssemblyPath { get => Getter<string>(); set => Setter(value); }
        public Visibility PluginURLVisibility { get => Getter<Visibility>(); set => Setter(value); }
        public Visibility ModNameOnlyVisibility { get => Getter<Visibility>(); set => Setter(value); }
        public Visibility AuthorURLVisibility { get => Getter<Visibility>(); set => Setter(value); }
        public Visibility AuthorOnlyVisibility { get => Getter<Visibility>(); set => Setter(value); }
    }

    public class SettingCommand : ICommand
    {
        private ModViewModel modViewModel;

        public SettingCommand(ModViewModel modViewModel) => this.modViewModel = modViewModel;

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => modViewModel != null && modViewModel.InstanceId != null;

        public async void Execute(object parameter) => await Globals.Client?.SendCommandAsync(new PipeCommands.ModSettingEvent { InstanceId = modViewModel.InstanceId });
    }

    public class OpenModFolderCommand : ICommand
    {
        private ModViewModel modViewModel;

        public OpenModFolderCommand(ModViewModel modViewModel) => this.modViewModel = modViewModel;

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => modViewModel != null && modViewModel.AssemblyPath != null;

        public void Execute(object parameter) => System.Diagnostics.Process.Start(System.IO.Path.GetDirectoryName(modViewModel.AssemblyPath));
    }
}
