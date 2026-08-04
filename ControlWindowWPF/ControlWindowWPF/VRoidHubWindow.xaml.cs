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
    /// VRM1.0ライセンスの正規化値(SDKのEnumLicense文字列: ok/ng/need/noneed/profit/nonprofit/notset)を
    /// VRM0.x表示と同じ○×UI用の記号・色・ラベルへ変換する。ConverterParameterで symbol/color/label を切り替える。
    /// </summary>
    public class Vrm10LicenseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var s = value as string ?? "";
            switch (parameter as string)
            {
                case "symbol":
                    switch (s)
                    {
                        case "ok": case "profit": case "noneed": return "○";
                        case "nonprofit": return "△";
                        case "ng": return "×";
                        case "need": return "！";
                        default: return "?";
                    }
                case "color":
                    switch (s)
                    {
                        case "ok": case "profit": case "noneed": return Brushes.Green;
                        case "nonprofit": return Brushes.Orange;
                        case "ng": case "need": return Brushes.Red;
                        default: return Brushes.Black;
                    }
                case "label":
                    string key;
                    switch (s)
                    {
                        case "ok": key = "Allow"; break;
                        case "ng": key = "Disallow"; break;
                        case "need": key = "VRoidHubWindow_Necessary"; break;
                        case "noneed": key = "VRoidHubWindow_Unnecessary"; break;
                        case "profit": key = "Allow"; break;
                        case "nonprofit": key = "VRoidHubWindow_NonProfit"; break;
                        default: key = "Unknown"; break;
                    }
                    return LanguageSelector.Get(key) ?? "";
                default:
                    return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// VRoidHubWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class VRoidHubWindow : Window
    {
        public static bool IncludeVRoidHubWindow = true;

        public ObservableCollection<ModelItem> ModelItems { get; set; }

        public class ModelItem : ViewModelBase
        {
            public string id { get { return Getter<string>(); } set { Setter(value); } }

            public string portrait_image_sq150 { get { return Getter<string>(); } set { Setter(value); } }
            public string character_name { get { return Getter<string>(); } set { Setter(value); } }
            public string name { get { return Getter<string>(); } set { Setter(value); } }
            public string user_icon_sq50 { get { return Getter<string>(); } set { Setter(value); } }
            public string user_name { get { return Getter<string>(); } set { Setter(value); } }

            public string full_body_image_original { get { return Getter<string>(); } set { Setter(value); } }
            public string characterization_allowed_user { get { return Getter<string>(); } set { Setter(value); } }
            public string corporate_commercial_use { get { return Getter<string>(); } set { Setter(value); } }
            public string credit { get { return Getter<string>(); } set { Setter(value); } }
            public string modification { get { return Getter<string>(); } set { Setter(value); } }
            public string personal_commercial_use { get { return Getter<string>(); } set { Setter(value); } }
            public string redistribution { get { return Getter<string>(); } set { Setter(value); } }
            public string sexual_expression { get { return Getter<string>(); } set { Setter(value); } }
            public string violent_expression { get { return Getter<string>(); } set { Setter(value); } }
            public string vroid_hub_url { get { return Getter<string>(); } set { Setter(value); } }
            public string type_symbol { get { return Getter<string>(); } set { Setter(value); } }
            public Brush type_brush { get { return Getter<Brush>(); } set { Setter(value); } }
            public Visibility licence_visibility { get { return Getter<Visibility>(); } set { Setter(value); } }
            public Thickness full_body_image_margin { get { return Getter<Thickness>(); } set { Setter(value); } }
            public int order { get { return Getter<int>(); } set { Setter(value); } }
            public PipeCommands.ModelFilters modelFilter { get{ return Getter<PipeCommands.ModelFilters>(); } set { Setter(value); } }

            //VRM1.0対応(VRM0.xと同じ○×UIで表示するため各項目を個別プロパティで保持する)
            public string spec_version { get { return Getter<string>(); } set { Setter(value); } }
            public Visibility licence_vrm10_visibility { get { return Getter<Visibility>(); } set { Setter(value); } }
            public string vrm10_avatar_user { get { return Getter<string>(); } set { Setter(value); } }
            public string vrm10_violence { get { return Getter<string>(); } set { Setter(value); } }
            public string vrm10_sexuality { get { return Getter<string>(); } set { Setter(value); } }
            public string vrm10_political_religious { get { return Getter<string>(); } set { Setter(value); } }
            public string vrm10_antisocial_hate { get { return Getter<string>(); } set { Setter(value); } }
            public string vrm10_personal_commercial { get { return Getter<string>(); } set { Setter(value); } }
            public string vrm10_corporate_commercial { get { return Getter<string>(); } set { Setter(value); } }
            public string vrm10_redistribution { get { return Getter<string>(); } set { Setter(value); } }
            public string vrm10_modification { get { return Getter<string>(); } set { Setter(value); } }
            public string vrm10_credit { get { return Getter<string>(); } set { Setter(value); } }
        }

        public VRoidHubWindow()
        {
            InitializeComponent();
            ModelItems = new ObservableCollection<ModelItem>();
            BindingOperations.EnableCollectionSynchronization(ModelItems, new object());
            //ItemsSourceにObservableCollectionを直接バインドし、追記は正しい位置へInsertする。
            //(CollectionViewのソートは追加のたびに再評価されスクロール位置がリセットされるため使わない)
            ModelListBox.ItemsSource = ModelItems;
        }

        //orderの昇順を保ったまま、同order末尾へ挿入する(仮想化リストのスクロール位置を維持)
        private void InsertModelItemSorted(ModelItem item)
        {
            int insertIndex = ModelItems.Count;
            for (int i = 0; i < ModelItems.Count; i++)
            {
                if (ModelItems[i].order > item.order)
                {
                    insertIndex = i;
                    break;
                }
            }
            ModelItems.Insert(insertIndex, item);
        }

        //フィルタごとに次ページがあるか
        private Dictionary<PipeCommands.ModelFilters, bool> _hasNext = new Dictionary<PipeCommands.ModelFilters, bool>();
        //続きを取得中か(スクロール連打防止)
        private bool _isLoadingMore = false;

        private enum Panels
        {
            None, Login, Error, Code, Progress, ModelList,
        }

        private void ChangePanel(Panels panels)
        {
            Grid[] grids = new Grid[] { LoginGrid, ErrorGrid, CodeGrid, ProgressGrid, ModelListGrid };
            Grid targetGrid = null;
            switch (panels)
            {
                case Panels.Login: targetGrid = LoginGrid; break;
                case Panels.Error: targetGrid = ErrorGrid; break;
                case Panels.Code: targetGrid = CodeGrid; break;
                case Panels.Progress: targetGrid = ProgressGrid; LoadProgressBar.IsIndeterminate = true; break;
                case Panels.ModelList: targetGrid = ModelListGrid; break;
            }
            foreach (var grid in grids)
            {
                if (grid == targetGrid) grid.Visibility = Visibility.Visible;
                else grid.Visibility = Visibility.Collapsed;
            }
        }

        private async void DoLoginButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_DoLogin { });
            ChangePanel(Panels.Code);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Globals.Client.ReceivedEvent += Client_Received;
            await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_StartAuthenticate { });
        }

        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.CommandType == typeof(PipeCommands.VRoidSDK_NeedLogin))
                {
                    ChangePanel(Panels.Login);
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_EndAuthenticate))
                {
                    var d = (PipeCommands.VRoidSDK_EndAuthenticate)e.Data;

                    if (d.IsSuccess)
                    {
                        //ログインに成功
                        LoadModels();
                    }
                    else
                    {
                        ChangePanel(Panels.Code);
                    }
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_Error))
                {
                    var d = (PipeCommands.VRoidSDK_Error)e.Data;
                    ErrorTextBlock.Text = d.Message;
                    ChangePanel(Panels.Error);
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_ReturnModels))
                {
                    var d = (PipeCommands.VRoidSDK_ReturnModels)e.Data;
                    ShowModels(d.Models, d.ModelFilter, d.Append, d.HasNext);
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_ModelDownloadProgress))
                {
                    var d = (PipeCommands.VRoidSDK_ModelDownloadProgress)e.Data;
                    LoadProgressBar.Value = d.progress;
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_ModelLoadComplete))
                {
                    LoadProgressBar.Value = LoadProgressBar.Maximum;
                    this.DialogResult = true;
                }
            });
        }

        private void ShowModels(List<PipeCommands.CharacterModel> characterModels, PipeCommands.ModelFilters modelFilter, bool append, bool hasNext)
        {
            string type_symbol;
            Brush type_brush;
            bool showLicense; //このフィルタでライセンスを表示するか(自作モデルは非表示)
            Thickness full_body_image_margin;
            int order;
            if (modelFilter == PipeCommands.ModelFilters.Heart)
            {
                type_symbol = "♥";
                type_brush = new SolidColorBrush(Colors.Red);
                showLicense = true;
                full_body_image_margin = new Thickness(260, 0, 0, 0);
                order = 1;
            }
            else if (modelFilter == PipeCommands.ModelFilters.Recommend)
            {
                type_symbol = "★";
                type_brush = new SolidColorBrush(Colors.Orange);
                showLicense = true;
                full_body_image_margin = new Thickness(260, 0, 0, 0);
                order = 2;
            }
            else
            {
                type_symbol = "";
                type_brush = new SolidColorBrush(Colors.Blue);
                showLicense = false;
                full_body_image_margin = new Thickness(500, 0, 0, 0);
                order = 0;
            }

            //ページネーション状態を更新
            _hasNext[modelFilter] = hasNext;
            if (append) _isLoadingMore = false;

            //初回(追記でない)取得時は、同フィルタの既存項目を入れ替える
            if (append == false && ModelItems.Count > 0)
            {
                foreach (var oldmodel in ModelItems.Where(d => d.order == order).ToList())
                {
                    ModelItems.Remove(oldmodel);
                }
            }
            ChangePanel(Panels.ModelList);
            foreach (var characterModel in characterModels)
            {
                //追記時に重複を避ける
                if (append && ModelItems.Any(d => d.id == characterModel.id && d.order == order)) continue;

                var isVRM10 = characterModel.spec_version == "1.0";
                InsertModelItemSorted(new ModelItem
                {
                    id = characterModel.id,
                    character_name = characterModel.character.name,
                    name = characterModel.name,
                    portrait_image_sq150 = characterModel.portrait_image.sq150.url,
                    user_icon_sq50 = characterModel.character.user.icon.sq50.url,
                    user_name = characterModel.character.user.name,
                    full_body_image_original = characterModel.full_body_image.original.url,
                    characterization_allowed_user = characterModel.license.characterization_allowed_user,
                    corporate_commercial_use = characterModel.license.corporate_commercial_use,
                    credit = characterModel.license.credit,
                    modification = characterModel.license.modification,
                    personal_commercial_use = characterModel.license.personal_commercial_use,
                    redistribution = characterModel.license.redistribution,
                    sexual_expression = characterModel.license.sexual_expression,
                    violent_expression = characterModel.license.violent_expression,
                    vroid_hub_url = $"https://hub.vroid.com/characters/{characterModel.character.id}/models/{characterModel.id}",
                    type_symbol = type_symbol,
                    type_brush = type_brush,
                    //VRM0.x・VRM1.0とも同じ○×シンボル行列で表示する(モデルのspec_versionでどちらを表示するか切り替える)
                    licence_visibility = (showLicense && !isVRM10) ? Visibility.Visible : Visibility.Collapsed,
                    licence_vrm10_visibility = (showLicense && isVRM10) ? Visibility.Visible : Visibility.Collapsed,
                    spec_version = characterModel.spec_version,
                    vrm10_avatar_user = characterModel.license_vrm10.avatar_user,
                    vrm10_violence = characterModel.license_vrm10.violence,
                    vrm10_sexuality = characterModel.license_vrm10.sexuality,
                    vrm10_political_religious = characterModel.license_vrm10.political_religious,
                    vrm10_antisocial_hate = characterModel.license_vrm10.antisocial_hate,
                    vrm10_personal_commercial = characterModel.license_vrm10.personal_commercial,
                    vrm10_corporate_commercial = characterModel.license_vrm10.corporate_commercial,
                    vrm10_redistribution = characterModel.license_vrm10.redistribution,
                    vrm10_modification = characterModel.license_vrm10.modification,
                    vrm10_credit = characterModel.license_vrm10.credit,
                    full_body_image_margin = full_body_image_margin,
                    order = order,
                    modelFilter = modelFilter,
                });
            }
        }

        //リストを下までスクロールしたら次ページを取得する(順次読み込み)。
        //一番下のグループ(最大order)のみを対象にし、末尾へ追記することでスクロール位置を保つ。
        private async void ModelListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isLoadingMore) return;
            if (canLoadModels == false) return;
            //一番下に表示されているグループ(=最後の項目のフィルタ)を続き取得の対象にする
            var bottomItem = ModelItems.LastOrDefault();
            if (bottomItem == null) return;
            var targetFilter = bottomItem.modelFilter;
            if (_hasNext.TryGetValue(targetFilter, out var hasNext) == false || hasNext == false) return;
            //残りスクロールが1画面分を切ったら先読みする
            if (e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight * 2)
            {
                _isLoadingMore = true;
                await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_RequestMoreModels { ModelFilter = targetFilter });
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Globals.Client.ReceivedEvent -= Client_Received;
        }

        private bool canLoadModels = false;

        private async void LoadModels()
        {
            await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_RequestAccountCharacterModels { });
            await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_RequestHearts { });
            await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_RequestRecommend { });
            canLoadModels = true;
        }

        private async void RegisterCodeButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_RegisterCode { Code = RegisterCodeTextBox.Text });
        }

        private void VRoidHubUrlButton_Click(object sender, RoutedEventArgs e)
        {
            var url = VRoidHubUrlButton.Tag as string;
            if (string.IsNullOrWhiteSpace(url) == false)
            {
                System.Diagnostics.Process.Start(url);
            }
        }

        private void ModelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelListBox.SelectedItem == null)
            {
                this.DataContext = new ModelItem();
            }
            else
            {
                this.DataContext = ModelListBox.SelectedItem as ModelItem;
            }
        }

        private void VRoidHubFindModelButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(@"https://hub.vroid.com/apps/74e43cd52cc429201044b16afd113319ad9d6cf2ef4ab6610359f8401b0d334c");
            //https://hub.vroid.com/models?is_downloadable=1&characterization_allowed_user=everyone
        }

        private async void Window_Activated(object sender, EventArgs e)
        {
            if (canLoadModels)
            {
                await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_RequestAccountCharacterModels { });
                await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_RequestHearts { });
            }
        }

        private async void LoadModelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                canLoadModels = false;
                if (ModelListBox.SelectedItem == null)
                {
                    MessageBox.Show(LanguageSelector.Get("VRoidHubWindow_SelectModelMessage"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    var model = ModelListBox.SelectedItem as ModelItem;
                    if (model.credit == "necessary" && model.modelFilter != PipeCommands.ModelFilters.Account)
                    {
                        if (MessageBox.Show(LanguageSelector.Get("VRoidHubWindow_CreditCautionConfirm"), LanguageSelector.Get("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.No)
                        {
                            return;
                        }
                    }
                    ChangePanel(Panels.Progress);
                    LoadProgressBar.IsIndeterminate = false;
                    Globals.CurrentVRMFilePath = $"vroidhub://{model.id}";
                    await Globals.Client?.SendCommandAsync(new PipeCommands.VRoidSDK_LoadModel { id = model.id });
                    await Globals.Client?.SendCommandAsync(new PipeCommands.LoadRemoteVRM { Path = Globals.CurrentVRMFilePath });
                }

            }
            finally
            {
                canLoadModels = true;
            }
        }

        private void PasteCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                RegisterCodeTextBox.Text = Clipboard.GetText();
            }
        }
    }
}
