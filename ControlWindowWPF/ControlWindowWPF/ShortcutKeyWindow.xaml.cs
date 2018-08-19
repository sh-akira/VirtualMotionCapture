using sh_akira;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// ShortcutKeyWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ShortcutKeyWindow : Window
    {

        public class KeysListItem
        {
            public string TypeStr { get; set; }
            public string NameStr { get; set; }
            public string KeysStr { get; set; }
            public KeyAction KeyAction { get; set; }
        }

        public ObservableCollection<KeysListItem> KeysListItems = new ObservableCollection<KeysListItem>();

        public ShortcutKeyWindow()
        {
            InitializeComponent();
            CenterColor = FromHsv(0, S, V);
            LeftImage.Source = LeftWB;
            RightImage.Source = RightWB;
            KeysDataGrid.ItemsSource = KeysListItems;
            UpdateKeyList();
            UpdateTouchPadPoints();
            if (Directory.Exists(Globals.GetCurrentAppDir() + PresetDirectory) == false)
            {
                Directory.CreateDirectory(Globals.GetCurrentAppDir() + PresetDirectory);
            }
            PresetComboBox.ItemsSource = Directory.EnumerateFiles(Globals.GetCurrentAppDir() + PresetDirectory, "*.json").Select(d => System.IO.Path.GetFileNameWithoutExtension(d));
        }

        private void UpdateTouchPadPoints()
        {
            LeftCenterKeyCheckBox.IsChecked = Globals.LeftControllerCenterEnable;
            if (Globals.LeftControllerPoints != null)
            {
                if (Globals.LeftControllerPoints.Count > 0)
                {
                    LeftTouchPadUpDown.Value = Globals.LeftControllerPoints.Count;
                    ClearPoints(true);
                    Globals.LeftControllerPoints.ForEach(d => AddPoint(d.x, -d.y, true));
                    UpdateTouchPadView(LeftWB);
                }
            }
            RightCenterKeyCheckBox.IsChecked = Globals.RightControllerCenterEnable;
            if (Globals.RightControllerPoints != null)
            {
                if (Globals.RightControllerPoints.Count > 0)
                {
                    RightTouchPadUpDown.Value = Globals.RightControllerPoints.Count;
                    ClearPoints(false);
                    Globals.RightControllerPoints.ForEach(d => AddPoint(d.x, -d.y, false));
                    UpdateTouchPadView(RightWB);
                }
            }
            TouchPadApplyButton.IsEnabled = false;
        }

        private float _nextH = 0;
        private float S = 0.7f;
        private float V = 0.95f;
        private float nextH { get { _nextH += 67; if (_nextH > 360) _nextH -= 360; return _nextH; } }
        private WriteableBitmap LeftWB = new WriteableBitmap(300, 300, 96.0, 96.0, PixelFormats.Bgr32, null);
        private WriteableBitmap RightWB = new WriteableBitmap(300, 300, 96.0, 96.0, PixelFormats.Bgr32, null);

        public class ControllerPoint
        {
            public Point Point;
            public Color Color;
            public Brush Brush;
            public Pen Pen;
            public Thumb Thumb;
            public WriteableBitmap Wb;
        }

        public List<ControllerPoint> LeftPoints = new List<ControllerPoint>();
        public List<ControllerPoint> RightPoints = new List<ControllerPoint>();

        private Color CenterColor;

        private Image TouchPadImage(bool isLeft) => isLeft ? LeftImage : RightImage;
        private Canvas TouchPadCanvas(bool isLeft) => isLeft ? LeftCanvas : RightCanvas;


        private void AddPoint(float x, float y, bool isLeft)
        {
            var point = new ControllerPoint();
            point.Point = new Point(x, y);
            point.Color = FromHsv(nextH, S, V);
            point.Brush = new SolidColorBrush(point.Color);
            point.Brush.Freeze();
            point.Pen = new Pen(point.Brush, 1.0f);
            point.Pen.Freeze();
            point.Thumb = new Thumb { Width = 8, Height = 8, Background = new SolidColorBrush(Colors.Black) };
            TouchPadCanvas(isLeft).Children.Add(point.Thumb);
            point.Thumb.PreviewMouseLeftButtonDown += Thumb_PreviewMouseLeftButtonDown;
            point.Thumb.PreviewMouseMove += Thumb_PreviewMouseMove;
            point.Thumb.PreviewMouseLeftButtonUp += Thumb_PreviewMouseLeftButtonUp;
            Canvas.SetLeft(point.Thumb, (int)((point.Point.X + 1.0f) * TouchPadImage(isLeft).Width) / 2);
            Canvas.SetTop(point.Thumb, (int)((point.Point.Y + 1.0f) * TouchPadImage(isLeft).Height) / 2);
            point.Thumb.Tag = point;
            point.Wb = isLeft ? LeftWB : RightWB;
            (isLeft ? LeftPoints : RightPoints).Add(point);
        }

        //h:0～360 s:0～1.0 v:0～1.0
        public static Color FromHsv(float h, float s, float v)
        {

            float r, g, b;
            if (s == 0)
            {
                r = v;
                g = v;
                b = v;
            }
            else
            {
                h = h / 60f;
                int i = (int)Math.Floor(h);
                float f = h - i;
                float p = v * (1f - s);
                float q;
                if (i % 2 == 0)
                {
                    //t
                    q = v * (1f - (1f - f) * s);
                }
                else
                {
                    q = v * (1f - f * s);
                }

                switch (i)
                {
                    case 0: r = v; g = q; b = p; break;
                    case 1: r = q; g = v; b = p; break;
                    case 2: r = p; g = v; b = q; break;
                    case 3: r = p; g = q; b = v; break;
                    case 4: r = q; g = p; b = v; break;
                    case 5: r = v; g = p; b = q; break;
                    default: r = 0; g = 0; b = 0; break;
                }
            }

            return Color.FromArgb(255, (byte)Math.Round(r * 255f), (byte)Math.Round(g * 255f), (byte)Math.Round(b * 255f));
        }

        private void ClearPoints(bool isLeft)
        {
            _nextH = 0;
            var points = (isLeft ? LeftPoints : RightPoints);
            foreach (var p in points)
            {
                p.Thumb.PreviewMouseLeftButtonDown -= Thumb_PreviewMouseLeftButtonDown;
                p.Thumb.PreviewMouseMove -= Thumb_PreviewMouseMove;
                p.Thumb.PreviewMouseLeftButtonUp -= Thumb_PreviewMouseLeftButtonUp;
                TouchPadCanvas(isLeft).Children.Remove(p.Thumb);
            }
            points.Clear();
            //(isLeft ? Globals.LeftControllerPoints : Globals.RightControllerPoints).Clear();
        }

        private DrawingGroup backingStore = new DrawingGroup();

        private void UpdateTouchPadView(WriteableBitmap wb)
        {
            wb.Lock();
            var isLeft = wb == LeftWB;
            var width = (float)wb.PixelWidth;
            var height = (float)wb.PixelHeight;
            //領域塗り
            var points = isLeft ? LeftPoints : RightPoints;
            if (points.Count > 0)
            {
                for (int y = 0; y < wb.PixelHeight; y++)
                {
                    for (int x = 0; x < wb.PixelWidth; x++)
                    {
                        var nearest = NearestPointIndex(points, (x * 2 - width) / width, (y * 2 - height) / height);
                        DrawPoint(wb, x, y, points[nearest].Color);
                    }
                }
            }
            if ((isLeft ? LeftCenterKeyCheckBox : RightCenterKeyCheckBox).IsChecked.Value)
            {
                var cwidth = width * 2 / 5;
                var cheight = height * 2 / 5;
                FillEllipse(wb, (int)(width / 2.0), (int)(height / 2.0), (int)(cwidth / 2), (int)(cheight / 2), CenterColor);
            }
            //円形に切り取り
            FillEllipse(wb, (int)(width / 2), (int)(height / 2), (int)(width / 2), (int)(height / 2), Colors.White, true);
            wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
            wb.Unlock();
            if (TouchPadApplyButton != null) TouchPadApplyButton.IsEnabled = true;
        }

        private bool IsDrag;
        private double DragX;
        private double DragY;
        private void Thumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var thumb = sender as Thumb;
            IsDrag = true;
            var pos = e.GetPosition(thumb.Parent as Canvas);
            DragX = (thumb.Parent as Canvas).ActualWidth / 2;// pos.X;
            DragY = (thumb.Parent as Canvas).ActualHeight / 2;// pos.Y;
            thumb.CaptureMouse();
        }

        private void Thumb_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Thumb_MouseMove");
            if (IsDrag)
            {
                var thumb = sender as Thumb;
                var point = thumb.Tag as ControllerPoint;
                var canvas = thumb.Parent as Canvas;
                var r = canvas.ActualWidth / 2.0;
                var pos = e.GetPosition(canvas);
                var newx = Canvas.GetLeft(point.Thumb) + pos.X - DragX - r;
                var newy = Canvas.GetTop(point.Thumb) + pos.Y - DragY - r;
                var rad = Math.Atan2(newx, newy);
                //var rad = angle * (Math.PI / 180);
                newx = Math.Sin(rad) * 0.8 * r + r;
                newy = Math.Cos(rad) * 0.8f * r + r;
                System.Diagnostics.Debug.WriteLine($"X:{newx} Y:{newy}");
                Canvas.SetLeft(point.Thumb, newx);
                Canvas.SetTop(point.Thumb, newy);
                var width = (float)canvas.ActualWidth;
                var height = (float)canvas.ActualHeight;
                point.Point.X = (Canvas.GetLeft(point.Thumb) * 2 - width) / width;
                point.Point.Y = (Canvas.GetTop(point.Thumb) * 2 - width) / height;
                UpdateTouchPadView((thumb.Tag as ControllerPoint).Wb);
            }
        }

        private void Thumb_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var thumb = sender as Thumb;
            IsDrag = false;
            thumb.ReleaseMouseCapture();
        }

        private int NearestPointIndex(List<ControllerPoint> points, float x, float y)
        {
            double maxlength = double.MaxValue;
            int index = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i].Point;
                double length = Math.Sqrt(Math.Pow(x - p.X, 2) + Math.Pow(y - p.Y, 2));
                if (maxlength > length)
                {
                    maxlength = length;
                    index = i;
                }
            }
            return index;
        }

        private void LeftCenterKeyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateTouchPadView(LeftWB);
        }

        private void LeftCenterKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateTouchPadView(LeftWB);
        }

        private void LeftTouchPadUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LeftTouchPadUpDownTextBlock != null) LeftTouchPadUpDownTextBlock.Text = LeftTouchPadUpDown.Value.ToString();
            ClearPoints(true);
            for (int i = 1; i <= LeftTouchPadUpDown.Value; i++)
            {
                var rad = -((360 / (double)LeftTouchPadUpDown.Value) * (Math.PI / 180) * i);
                AddPoint((float)Math.Sin(rad) * 0.8f, -(float)Math.Cos(rad) * 0.8f, true);
            }
            UpdateTouchPadView(LeftWB);
        }

        private void RightCenterKeyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateTouchPadView(RightWB);
        }

        private void RightCenterKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateTouchPadView(RightWB);
        }

        private void RightTouchPadUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RightTouchPadUpDownTextBlock != null) RightTouchPadUpDownTextBlock.Text = RightTouchPadUpDown.Value.ToString();
            ClearPoints(false);
            for (int i = 1; i <= RightTouchPadUpDown.Value; i++)
            {
                var rad = (360 / (double)RightTouchPadUpDown.Value) * (Math.PI / 180) * i;
                AddPoint((float)Math.Sin(rad) * 0.8f, -(float)Math.Cos(rad) * 0.8f, false);
            }
            UpdateTouchPadView(RightWB);
        }

        private async void HandGestureAddButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.StartKeyConfig { });
            var win = new HandGestureControlKeyAddWindow();
            if (win.ShowDialog() == true)
            {
                UpdateKeyList();
            }
            await Globals.Client.SendCommandAsync(new PipeCommands.EndKeyConfig { });
        }

        private async void FaceAddButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.StartKeyConfig { });
            var win = new FaceControlKeyAddWindow();
            if (win.ShowDialog() == true)
            {
                UpdateKeyList();
            }
            await Globals.Client.SendCommandAsync(new PipeCommands.EndKeyConfig { });
        }

        private async void FunctionAddButton_Click(object sender, RoutedEventArgs e)
        {
            await Globals.Client.SendCommandAsync(new PipeCommands.StartKeyConfig { });
            var win = new FunctionKeyAddWindow();
            if (win.ShowDialog() == true)
            {
                UpdateKeyList();
            }
            await Globals.Client.SendCommandAsync(new PipeCommands.EndKeyConfig { });
        }

        private void UpdateKeyList()
        {
            KeysListItems.Clear();
            if (Globals.KeyActions == null) return;
            foreach (var key in Globals.KeyActions)
            {
                var typestr = "";
                if (key.HandAction)
                {
                    if (key.Hand == Hands.Both) typestr += "両手";
                    else if (key.Hand == Hands.Right) typestr += "右手";
                    else typestr += "左手";
                }
                if (key.FaceAction)
                {
                    typestr += "表情";
                }
                if (key.FunctionAction)
                {
                    typestr += "機能";
                }

                var keysstr = "";
                var keystrs = new List<string>();
                foreach (var k in key.KeyConfigs)
                {
                    keystrs.Add(k.ToString());
                }
                if (keystrs.Count > 0)
                {
                    keysstr = string.Join(" + ", keystrs);
                }
                KeysListItems.Add(new KeysListItem
                {
                    TypeStr = typestr,
                    NameStr = key.Name,
                    KeysStr = keysstr,
                    KeyAction = key
                });
            }
        }

        #region WriteableBitmap

        public unsafe void DrawPoint(WriteableBitmap bmp, int x, int y, Color drawColor)
        {
            var pixels = (int*)bmp.BackBuffer;
            int w = (int)bmp.Width;
            int color = (((int)drawColor.R & 0xff) << 16) | (((int)drawColor.G & 0xff) << 8) | ((int)drawColor.B & 0xff);
            pixels[y * w + x] = color;
        }

        public unsafe void FillEllipse(WriteableBitmap bmp, int xc, int yc, int xr, int yr, Color drawColor, bool reverse = false)
        {

            var pixels = (int*)bmp.BackBuffer;
            int w = (int)bmp.Width;
            int h = (int)bmp.Height;

            // Avoid endless loop
            if (xr < 1 || yr < 1)
            {
                return;
            }

            // Skip completly outside objects
            if (xc - xr >= w || xc + xr < 0 || yc - yr >= h || yc + yr < 0)
            {
                return;
            }

            // Init vars
            int uh, lh, uy, ly, lx, rx;
            int x = xr;
            int y = 0;
            int xrSqTwo = (xr * xr) << 1;
            int yrSqTwo = (yr * yr) << 1;
            int xChg = yr * yr * (1 - (xr << 1));
            int yChg = xr * xr;
            int err = 0;
            int xStopping = yrSqTwo * xr;
            int yStopping = 0;

            int color = (((int)drawColor.R & 0xff) << 16) | (((int)drawColor.G & 0xff) << 8) | ((int)drawColor.B & 0xff);

            // Draw first set of points counter clockwise where tangent line slope > -1.
            while (xStopping >= yStopping)
            {
                // Draw 4 quadrant points at once
                // Upper half
                uy = yc + y;
                // Lower half
                ly = yc - y - 1;

                // Clip
                if (uy < 0) uy = 0;
                if (uy >= h) uy = h - 1;
                if (ly < 0) ly = 0;
                if (ly >= h) ly = h - 1;

                // Upper half
                uh = uy * w;
                // Lower half
                lh = ly * w;

                rx = xc + x;
                lx = xc - x;

                // Clip
                if (rx < 0) rx = 0;
                if (rx >= w) rx = w - 1;
                if (lx < 0) lx = 0;
                if (lx >= w) lx = w - 1;

                // Draw line
                if (reverse == false)
                {
                    for (int i = lx; i <= rx; i++)
                    {
                        pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                        pixels[i + lh] = color; // Quadrant III to IV
                    }
                }
                else
                {
                    for (int i = 0; i < lx; i++)
                    {
                        pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                        pixels[i + lh] = color; // Quadrant III to IV
                    }

                    for (int i = rx; i < w; i++)
                    {
                        pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                        pixels[i + lh] = color; // Quadrant III to IV
                    }
                }


                y++;
                yStopping += xrSqTwo;
                err += yChg;
                yChg += xrSqTwo;
                if ((xChg + (err << 1)) > 0)
                {
                    x--;
                    xStopping -= yrSqTwo;
                    err += xChg;
                    xChg += yrSqTwo;
                }
            }

            // ReInit vars
            x = 0;
            y = yr;

            // Upper half
            uy = yc + y;
            // Lower half
            ly = yc - y;

            // Clip
            if (uy < 0) uy = 0;
            if (uy >= h) uy = h - 1;
            if (ly < 0) ly = 0;
            if (ly >= h) ly = h - 1;

            // Upper half
            uh = uy * w;
            // Lower half
            lh = ly * w;

            xChg = yr * yr;
            yChg = xr * xr * (1 - (yr << 1));
            err = 0;
            xStopping = 0;
            yStopping = xrSqTwo * yr;

            // Draw second set of points clockwise where tangent line slope < -1.
            while (xStopping <= yStopping)
            {
                // Draw 4 quadrant points at once
                rx = xc + x;
                lx = xc - x;

                // Clip
                if (rx < 0) rx = 0;
                if (rx >= w) rx = w - 1;
                if (lx < 0) lx = 0;
                if (lx >= w) lx = w - 1;

                // Draw line
                if (reverse == false)
                {
                    for (int i = lx; i <= rx; i++)
                    {
                        pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                        pixels[i + lh] = color; // Quadrant III to IV
                    }
                }
                else
                {
                    for (int i = 0; i < lx; i++)
                    {
                        pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                        pixels[i + lh] = color; // Quadrant III to IV
                    }

                    for (int i = rx; i < w; i++)
                    {
                        pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                        pixels[i + lh] = color; // Quadrant III to IV
                    }
                }

                x++;
                xStopping += yrSqTwo;
                err += xChg;
                xChg += yrSqTwo;
                if ((yChg + (err << 1)) > 0)
                {
                    y--;
                    uy = yc + y; // Upper half
                    ly = yc - y; // Lower half
                    if (uy < 0) uy = 0; // Clip
                    if (uy >= h) uy = h - 1; // ...
                    if (ly < 0) ly = 0;
                    if (ly >= h) ly = h - 1;
                    uh = uy * w; // Upper half
                    lh = ly * w; // Lower half
                    yStopping -= xrSqTwo;
                    err += yChg;
                    yChg += xrSqTwo;
                }
            }
        }
        #endregion

        private async void TouchPadApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.LeftControllerPoints == null) Globals.LeftControllerPoints = new List<UPoint>();
            Globals.LeftControllerPoints.Clear();
            foreach (var p in LeftPoints)
            {
                Globals.LeftControllerPoints.Add(new UPoint { x = (float)p.Point.X, y = -(float)p.Point.Y });
            }
            Globals.LeftControllerCenterEnable = LeftCenterKeyCheckBox.IsChecked.Value;
            if (Globals.RightControllerPoints == null) Globals.RightControllerPoints = new List<UPoint>();
            Globals.RightControllerPoints.Clear();
            foreach (var p in RightPoints)
            {
                Globals.RightControllerPoints.Add(new UPoint { x = (float)p.Point.X, y = -(float)p.Point.Y });
            }
            Globals.RightControllerCenterEnable = RightCenterKeyCheckBox.IsChecked.Value;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetControllerTouchPadPoints
            {
                IsOculus = false,
                LeftPoints = Globals.LeftControllerPoints,
                LeftCenterEnable = LeftCenterKeyCheckBox.IsChecked.Value,
                RightPoints = Globals.RightControllerPoints,
                RightCenterEnable = RightCenterKeyCheckBox.IsChecked.Value
            });
            TouchPadApplyButton.IsEnabled = false;
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (KeysDataGrid.SelectedItem != null)
            {
                bool? edited = true;
                var item = (KeysListItem)KeysDataGrid.SelectedItem;
                await Globals.Client.SendCommandAsync(new PipeCommands.StartKeyConfig { });
                if (item.KeyAction.FaceAction)
                {
                    var win = new FaceControlKeyAddWindow(item.KeyAction);
                    edited = win.ShowDialog();
                }
                else if (item.KeyAction.HandAction)
                {
                    var win = new HandGestureControlKeyAddWindow(item.KeyAction);
                    edited = win.ShowDialog();
                }
                else if (item.KeyAction.FunctionAction)
                {
                    var win = new FunctionKeyAddWindow(item.KeyAction);
                    edited = win.ShowDialog();
                }
                await Globals.Client.SendCommandAsync(new PipeCommands.EndKeyConfig { });
                if (edited == true)
                {   //編集なので、新しく追加された設定を選択中の設定と入れ替える
                    var insertto = Globals.KeyActions.IndexOf(item.KeyAction);
                    var newitem = Globals.KeyActions.Last();
                    Globals.KeyActions.Remove(newitem);
                    Globals.KeyActions.Remove(item.KeyAction);
                    Globals.KeyActions.Insert(insertto, newitem);
                    await Globals.Client.SendCommandAsync(new PipeCommands.SetKeyActions { KeyActions = Globals.KeyActions });
                    UpdateKeyList();
                }
            }
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (KeysDataGrid.SelectedItem != null)
            {
                var item = (KeysListItem)KeysDataGrid.SelectedItem;
                Globals.KeyActions.Remove(item.KeyAction);
                await Globals.Client.SendCommandAsync(new PipeCommands.SetKeyActions { KeyActions = Globals.KeyActions });
                UpdateKeyList();
            }
        }
        private const string PresetDirectory = "ShortcutKeyPresets";

        private void CustomSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (Globals.CheckFileNameIsValid(CustomNameTextBox.Text) == false)
            {
                MessageBox.Show("ファイル名として使用できない文字が含まれています。変更してください", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var path = Globals.GetCurrentAppDir() + PresetDirectory + "\\" + CustomNameTextBox.Text + ".json";
            if (File.Exists(path))
            {
                if (MessageBox.Show("既に存在する名前です。上書きしますか？", "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel)
                {
                    return;
                }
            }
            File.WriteAllText(path, Json.Serializer.ToReadable(Json.Serializer.Serialize(Globals.KeyActions)));
            PresetComboBox.ItemsSource = Directory.EnumerateFiles(Globals.GetCurrentAppDir() + PresetDirectory, "*.json").Select(d => System.IO.Path.GetFileNameWithoutExtension(d));
        }

        private async void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetComboBox.SelectedItem == null) return;

            var path = Globals.GetCurrentAppDir() + PresetDirectory + "\\" + PresetComboBox.SelectedItem.ToString() + ".json";
            Globals.KeyActions = Json.Serializer.Deserialize<List<KeyAction>>(File.ReadAllText(path));
            UpdateKeyList();
            await Globals.Client.SendCommandAsync(new PipeCommands.SetKeyActions { KeyActions = Globals.KeyActions });
        }
    }
}
