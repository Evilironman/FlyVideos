using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace FlyVideos;

public partial class MainWindow : Window
{
    private enum RepeatMode { All, One, Off }

    private static readonly double[] SpeedPresets = { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 };

    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".m4v", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".asf", ".mpg", ".mpeg",
        ".m2ts", ".mts", ".ts", ".vob", ".3gp", ".3g2", ".ogv", ".ogm", ".divx", ".f4v",
        ".mxf", ".y4m", ".ivf", ".mj2", ".rm", ".rmvb", ".amv", ".wtv", ".dav"
    };

    private const double ThumbWidth = 96;
    private const double ThumbHeight = 54;

    private static readonly Brush ThumbIdle = Freeze(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush ThumbHover = Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush ThumbSelected = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x42, 0x85, 0xF4)));

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private readonly List<string> _files = new();
    private readonly List<ThumbItem> _thumbs = new();
    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _osdTimer;
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _speedTimer;
    private int _thumbGeneration;
    private int _index = -1;
    private RepeatMode _repeat = RepeatMode.All;
    private bool _speedMode;
    private double _volume = 1.0;
    private bool _playing;
    private bool _fullscreen;
    private bool _scrubbing;
    private bool _toggleOnUp;
    private bool _dragMoved;
    private Point _downPoint;
    private Rect _restoreBounds;

    public MainWindow()
    {
        InitializeComponent();

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _progressTimer.Tick += (_, _) => UpdateProgressBar();
        _osdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
        _osdTimer.Tick += (_, _) => HideOsd();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            StatusText.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)));
        };
        _speedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _speedTimer.Tick += (_, _) => HideSpeedPanel();

        Player.MediaOpened += OnMediaOpened;
        Player.MediaEnded += OnMediaEnded;
        Player.MediaFailed += OnMediaFailed;

        SpeedDownButton.MouseLeftButtonDown += (_, e) => { StepSpeed(-1); e.Handled = true; };
        SpeedUpButton.MouseLeftButtonDown += (_, e) => { StepSpeed(1); e.Handled = true; };
        SpeedDownButton.MouseLeftButtonUp += (_, e) => e.Handled = true;
        SpeedUpButton.MouseLeftButtonUp += (_, e) => e.Handled = true;
        SpeedDownButton.MouseEnter += (_, _) => { SpeedDownButton.Background = (Brush)FindResource("ButtonHover"); RestartSpeedTimer(); };
        SpeedUpButton.MouseEnter += (_, _) => { SpeedUpButton.Background = (Brush)FindResource("ButtonHover"); RestartSpeedTimer(); };
        SpeedDownButton.MouseLeave += (_, _) => SpeedDownButton.Background = (Brush)FindResource("ButtonNormal");
        SpeedUpButton.MouseLeave += (_, _) => SpeedUpButton.Background = (Brush)FindResource("ButtonNormal");

        PreviewKeyDown += OnKeyDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonDown += OnMouseLeftDown;
        MouseLeftButtonUp += OnMouseLeftUp;
        MouseWheel += OnMouseWheel;
        DragOver += OnDragOver;
        Drop += OnDrop;
        BarHost.MouseLeftButtonDown += OnBarMouseDown;
    }

    public void Initialize(string[] args)
    {
        HintText.Text = "A/D browse \u00b7 Space play \u00b7 R repeat \u00b7 S speed \u00b7 F fullscreen \u00b7 Esc close";
        var path = args.FirstOrDefault(a => File.Exists(a) || Directory.Exists(a));
        if (path != null)
        {
            LoadFrom(path);
        }
        else
        {
            TitleText.Text = "FlyVideos";
            SubText.Text = "Drop a video here, or press O to open";
        }
    }

    private void LoadFrom(string path)
    {
        string full = Path.GetFullPath(path);
        string? dir = File.Exists(full) ? Path.GetDirectoryName(full) : full;
        if (dir == null || !Directory.Exists(dir)) return;

        var list = Directory.EnumerateFiles(dir)
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => Path.GetFileName(f), NaturalComparer.Instance)
            .ToList();

        int target = list.FindIndex(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase));
        if (target < 0 && File.Exists(full))
        {
            list.Insert(0, full);
            target = 0;
        }

        _files.Clear();
        _files.AddRange(list);

        if (_files.Count == 0)
        {
            TitleText.Text = Path.GetFileName(full);
            SubText.Text = "No videos in this folder";
            return;
        }

        BuildThumbs();
        Open(target < 0 ? 0 : target);
    }

    private void Open(int index)
    {
        if (_files.Count == 0) return;
        _index = ((index % _files.Count) + _files.Count) % _files.Count;
        string file = _files[_index];
        string name = Path.GetFileName(file);

        Title = name;
        TitleText.Text = name;
        RefreshSub();
        ErrorText.Visibility = Visibility.Collapsed;
        HideSpeedPanel();

        Player.Stop();
        Player.Source = new Uri(file);
        Player.Play();
        _playing = true;

        ShowOsd();
        _progressTimer.Start();
        SelectThumb(_index);
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        UpdateProgressBar();
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        switch (_repeat)
        {
            case RepeatMode.One:
                Player.Stop();
                Player.Play();
                _playing = true;
                break;
            case RepeatMode.Off:
                if (_index < _files.Count - 1)
                {
                    Open(_index + 1);
                }
                else
                {
                    _playing = false;
                    ShowStatus("End \u2014 repeat is off");
                }
                break;
            default:
                if (_files.Count > 1) Open(_index + 1);
                break;
        }
    }

    private void OnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        _playing = false;
        string name = _index >= 0 && _index < _files.Count ? Path.GetFileName(_files[_index]) : "";
        ErrorText.Text = $"Can't play {name}\nThis codec isn't supported by the Windows media engine.";
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.D:
            case Key.PageDown:
                if (_files.Count > 1) Open(_index + 1);
                break;
            case Key.A:
            case Key.PageUp:
                if (_files.Count > 1) Open(_index - 1);
                break;
            case Key.Space:
            case Key.K:
                TogglePlay();
                break;
            case Key.Up:
                AdjustVolume(0.05);
                break;
            case Key.Down:
                AdjustVolume(-0.05);
                break;
            case Key.M:
                Player.IsMuted = !Player.IsMuted;
                ShowStatus(Player.IsMuted ? "Muted" : $"Volume {(int)Math.Round(Player.Volume * 100)}%");
                break;
            case Key.J:
                SeekBy(-10);
                break;
            case Key.L:
                SeekBy(10);
                break;
            case Key.Home:
                SeekTo(TimeSpan.Zero);
                break;
            case Key.End:
                SeekTo(Duration() - TimeSpan.FromMilliseconds(300));
                break;
            case Key.OemOpenBrackets:
                AdjustSpeed(-0.1);
                break;
            case Key.OemCloseBrackets:
                AdjustSpeed(0.1);
                break;
            case Key.D0:
            case Key.NumPad0:
                Player.SpeedRatio = 1.0;
                RefreshSub();
                RestartSpeedTimer();
                ShowStatus("Speed 1.0\u00d7");
                break;
            case Key.F:
                ToggleFullscreen();
                break;
            case Key.Escape:
                if (_fullscreen) ToggleFullscreen();
                else Close();
                break;
            case Key.O:
                OpenDialog();
                break;
            case Key.R:
                CycleRepeat();
                break;
            case Key.S:
                ToggleSpeedMode();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void TogglePlay()
    {
        if (_files.Count == 0) return;
        if (_playing)
        {
            Player.Pause();
            _playing = false;
            ShowStatus("Paused");
        }
        else
        {
            Player.Play();
            _playing = true;
            ShowOsd();
        }
    }

    private TimeSpan Duration() => Player.NaturalDuration.HasTimeSpan ? Player.NaturalDuration.TimeSpan : TimeSpan.Zero;

    private void SeekBy(double seconds) => SeekTo(Player.Position + TimeSpan.FromSeconds(seconds));

    private void SeekTo(TimeSpan position)
    {
        var d = Duration();
        if (d <= TimeSpan.Zero) return;
        if (position > d - TimeSpan.FromMilliseconds(50)) position = d - TimeSpan.FromMilliseconds(50);
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        Player.Position = position;
        UpdateProgressBar();
        ShowOsd();
    }

    private void AdjustVolume(double delta)
    {
        _volume = Math.Clamp(Math.Round((_volume + delta) * 100) / 100, 0, 1);
        Player.Volume = _volume;
        if (Player.IsMuted && _volume > 0) Player.IsMuted = false;
        ShowStatus($"Volume {(int)Math.Round(_volume * 100)}%");
    }

    private void AdjustSpeed(double delta)
    {
        double speed = Math.Clamp(Math.Round((Player.SpeedRatio + delta) * 10) / 10, 0.25, 4.0);
        Player.SpeedRatio = speed;
        RefreshSub();
        RestartSpeedTimer();
        ShowStatus($"Speed {speed:0.0}\u00d7");
    }

    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
            _fullscreen = true;
            ThumbScroller.Visibility = Visibility.Collapsed;
            Topmost = true;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            ShowStatus("Fullscreen \u2014 press F or Esc to exit");
        }
        else
        {
            _fullscreen = false;
            Topmost = false;
            ThumbScroller.Visibility = _files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
        }
    }

    private void OpenDialog()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open video",
            Filter = "Video files|" + string.Join(";", Array.ConvertAll(VideoExtensions, x => "*" + x)) + "|All files|*.*"
        };
        if (dlg.ShowDialog(this) == true) LoadFrom(dlg.FileName);
    }

    private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (_toggleOnUp) TogglePlay();
            _toggleOnUp = false;
            ToggleFullscreen();
            return;
        }
        _toggleOnUp = false;
        _dragMoved = false;
        _downPoint = e.GetPosition(this);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        ShowOsd();
        if (_scrubbing)
        {
            ScrubTo(e.GetPosition(BarHost).X);
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed && !_dragMoved && !_fullscreen)
        {
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _downPoint.X) > 5 || Math.Abs(p.Y - _downPoint.Y) > 5)
            {
                _dragMoved = true;
                try { DragMove(); } catch (InvalidOperationException) { }
            }
        }
    }

    private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (_scrubbing)
        {
            _scrubbing = false;
            BarHost.ReleaseMouseCapture();
            return;
        }
        if (!_dragMoved && _files.Count > 0)
        {
            TogglePlay();
            _toggleOnUp = true;
        }
        _dragMoved = false;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_files.Count > 1) Open(e.Delta < 0 ? _index + 1 : _index - 1);
        e.Handled = true;
    }

    private void OnBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Duration() <= TimeSpan.Zero) return;
        _scrubbing = true;
        BarHost.CaptureMouse();
        ScrubTo(e.GetPosition(BarHost).X);
        e.Handled = true;
    }

    private void ScrubTo(double x)
    {
        var d = Duration();
        if (d <= TimeSpan.Zero || BarHost.ActualWidth <= 0) return;
        double frac = Math.Clamp(x / BarHost.ActualWidth, 0, 1);
        SeekTo(TimeSpan.FromMilliseconds(d.TotalMilliseconds * frac));
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            LoadFrom(paths[0]);
    }

    private void ShowOsd()
    {
        _osdTimer.Stop();
        OsdLayer.BeginAnimation(OpacityProperty, null);
        BarHost.BeginAnimation(OpacityProperty, null);
        OsdLayer.Opacity = 1;
        BarHost.Opacity = 1;
        BarHost.IsHitTestVisible = true;
        _osdTimer.Start();
    }

    private void HideOsd()
    {
        _osdTimer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(350));
        OsdLayer.BeginAnimation(OpacityProperty, fade);
        BarHost.BeginAnimation(OpacityProperty, fade);
        BarHost.IsHitTestVisible = false;
    }

    private void CycleRepeat()
    {
        _repeat = _repeat switch
        {
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.Off,
            _ => RepeatMode.All,
        };
        RefreshSub();
        ShowStatus(_repeat switch
        {
            RepeatMode.All => "Repeat: all",
            RepeatMode.One => "Repeat: one",
            _ => "Repeat: off",
        });
    }

    private void ToggleSpeedMode()
    {
        if (_speedMode)
        {
            HideSpeedPanel();
        }
        else
        {
            _speedMode = true;
            ShowSpeedPanel();
            ShowStatus($"Speed {Player.SpeedRatio:0.##}\u00d7");
        }
    }

    private void ShowSpeedPanel()
    {
        SpeedPanel.BeginAnimation(OpacityProperty, null);
        SpeedPanel.Opacity = 1;
        SpeedPanel.IsHitTestVisible = true;
        RestartSpeedTimer();
    }

    private void HideSpeedPanel()
    {
        _speedMode = false;
        _speedTimer.Stop();
        SpeedPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)));
        SpeedPanel.IsHitTestVisible = false;
    }

    private void RestartSpeedTimer()
    {
        if (!_speedMode) return;
        _speedTimer.Stop();
        _speedTimer.Start();
    }

    private void StepSpeed(int direction)
    {
        double current = Player.SpeedRatio;
        double target = current;
        if (direction > 0)
        {
            for (int i = 0; i < SpeedPresets.Length; i++)
            {
                if (SpeedPresets[i] > current + 0.001)
                {
                    target = SpeedPresets[i];
                    break;
                }
            }
        }
        else
        {
            for (int i = SpeedPresets.Length - 1; i >= 0; i--)
            {
                if (SpeedPresets[i] < current - 0.001)
                {
                    target = SpeedPresets[i];
                    break;
                }
            }
        }
        Player.SpeedRatio = target;
        RefreshSub();
        RestartSpeedTimer();
        ShowStatus($"Speed {target:0.##}\u00d7");
    }

    private void RefreshSub()
    {
        if (_files.Count == 0) return;
        string rep = _repeat switch
        {
            RepeatMode.All => "repeat all",
            RepeatMode.One => "repeat one",
            _ => "repeat off",
        };
        string speed = Math.Abs(Player.SpeedRatio - 1.0) > 0.001 ? $"  \u00b7  {Player.SpeedRatio:0.##}\u00d7" : "";
        SubText.Text = $"{_index + 1} / {_files.Count}  \u00b7  {rep}{speed}";
    }

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusText.BeginAnimation(OpacityProperty, null);
        StatusText.Opacity = 1;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void UpdateProgressBar()
    {
        var d = Duration();
        if (d.TotalMilliseconds > 0 && BarHost.ActualWidth > 0)
        {
            double frac = Math.Clamp(Player.Position.TotalMilliseconds / d.TotalMilliseconds, 0, 1);
            BarFill.Width = Math.Max(0, (BarHost.ActualWidth - 28) * frac);
            TimeText.Text = Format(Player.Position) + "  /  " + Format(d);
        }
        else
        {
            BarFill.Width = 0;
            TimeText.Text = "";
        }
    }

    private static string Format(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }

    private void BuildThumbs()
    {
        _thumbGeneration++;
        _thumbs.Clear();
        ThumbPanel.Children.Clear();

        if (_files.Count == 0)
        {
            ThumbScroller.Visibility = Visibility.Collapsed;
            return;
        }

        ThumbScroller.Visibility = Visibility.Visible;

        for (int i = 0; i < _files.Count; i++)
        {
            int index = i;
            string path = _files[i];
            var preview = new Image { Stretch = Stretch.Uniform };
            var glyph = new TextBlock
            {
                Text = "\u25b6",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var stack = new Grid();
            stack.Children.Add(preview);
            stack.Children.Add(glyph);
            var border = new Border
            {
                Width = ThumbWidth,
                Height = ThumbHeight,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x1A)),
                BorderThickness = new Thickness(2),
                BorderBrush = ThumbIdle,
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                ToolTip = Path.GetFileName(path),
                Child = stack,
                Clip = new RectangleGeometry(new Rect(0, 0, ThumbWidth, ThumbHeight), 6, 6)
            };
            border.MouseLeftButtonDown += (_, e) => { Open(index); e.Handled = true; };
            border.MouseLeftButtonUp += (_, e) => e.Handled = true;
            border.MouseEnter += (_, _) => { if (index != _index) border.BorderBrush = ThumbHover; };
            border.MouseLeave += (_, _) => { if (index != _index) border.BorderBrush = ThumbIdle; };

            ThumbPanel.Children.Add(border);
            _thumbs.Add(new ThumbItem { Root = border, Preview = preview, Glyph = glyph, Path = path });
        }

        int generation = _thumbGeneration;
        var items = _thumbs.ToList();
        Task.Run(() =>
        {
            foreach (var item in items)
            {
                if (generation != _thumbGeneration) return;
                var source = ShellThumbnails.TryGet(item.Path, 192, 108);
                if (source == null) continue;
                item.Preview.Dispatcher.Invoke(() =>
                {
                    if (generation != _thumbGeneration) return;
                    item.Preview.Source = source;
                    item.Glyph.Visibility = Visibility.Collapsed;
                });
            }
        });
    }

    private void SelectThumb(int index)
    {
        if (index < 0 || index >= _thumbs.Count) return;
        for (int i = 0; i < _thumbs.Count; i++)
            _thumbs[i].Root.BorderBrush = i == index ? ThumbSelected : ThumbIdle;
        ScrollThumbIntoCenter(index);
    }

    private void ScrollThumbIntoCenter(int index)
    {
        int attempts = 0;
        void TryScroll()
        {
            if (index < 0 || index >= _thumbs.Count) return;
            double viewport = ThumbScroller.ViewportWidth;
            if (viewport <= 0 && attempts++ < 10)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(TryScroll));
                return;
            }
            var el = _thumbs[index].Root;
            double x = el.TranslatePoint(new Point(0, 0), ThumbPanel).X;
            double extent = ThumbPanel.ActualWidth;
            double target = x + ThumbWidth / 2 - viewport / 2;
            ThumbScroller.ScrollToHorizontalOffset(Math.Clamp(target, 0, Math.Max(0, extent - viewport)));
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(TryScroll));
    }

    private sealed class ThumbItem
    {
        public required Border Root { get; init; }
        public required Image Preview { get; init; }
        public required TextBlock Glyph { get; init; }
        public required string Path { get; init; }
    }

    private sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x == null || y == null) return string.CompareOrdinal(x, y);
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                char cx = x[i], cy = y[j];
                if (char.IsDigit(cx) && char.IsDigit(cy))
                {
                    int si = i, sj = j;
                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;
                    string nx = x.Substring(si, i - si).TrimStart('0');
                    string ny = y.Substring(sj, j - sj).TrimStart('0');
                    if (nx.Length != ny.Length) return nx.Length - ny.Length;
                    int cmp = string.CompareOrdinal(nx, ny);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    int cmp = char.ToUpperInvariant(cx).CompareTo(char.ToUpperInvariant(cy));
                    if (cmp != 0) return cmp;
                    i++;
                    j++;
                }
            }
            return (x.Length - i) - (y.Length - j);
        }
    }
}
