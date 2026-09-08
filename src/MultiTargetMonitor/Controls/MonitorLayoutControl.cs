using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MultiTargetMonitor.ViewModels;

namespace MultiTargetMonitor.Controls;

/// <summary>
/// Draws a scaled diagram of the monitor layout (like the one in Windows Display Settings) and lets
/// the user click a monitor to toggle its selection. Bind <see cref="Monitors"/> to a collection of
/// <see cref="MonitorRow"/>. The brush properties are meant to be bound to theme resources.
/// </summary>
public sealed class MonitorLayoutControl : FrameworkElement
{
    private readonly List<(MonitorRow Row, Rect Box)> _hitBoxes = new();
    private readonly List<MonitorRow> _tracked = new();

    public MonitorLayoutControl()
    {
        Focusable = false;
        SnapsToDevicePixels = true;
    }

    // ---- data ---------------------------------------------------------

    public static readonly DependencyProperty MonitorsProperty = DependencyProperty.Register(
        nameof(Monitors), typeof(IEnumerable), typeof(MonitorLayoutControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnMonitorsChanged));

    public IEnumerable? Monitors
    {
        get => (IEnumerable?)GetValue(MonitorsProperty);
        set => SetValue(MonitorsProperty, value);
    }

    // ---- themeable brushes ------------------------------------------

    private static DependencyProperty Brush(string name, byte r, byte g, byte b) => DependencyProperty.Register(
        name, typeof(Brush), typeof(MonitorLayoutControl),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(r, g, b)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedFillProperty = Brush(nameof(SelectedFill), 0x12, 0xA5, 0x94);
    public static readonly DependencyProperty SelectedBorderProperty = Brush(nameof(SelectedBorder), 0x0B, 0x75, 0x68);
    public static readonly DependencyProperty SelectedTextProperty = Brush(nameof(SelectedText), 0xFF, 0xFF, 0xFF);
    public static readonly DependencyProperty UnselectedFillProperty = Brush(nameof(UnselectedFill), 0xE6, 0xE9, 0xEE);
    public static readonly DependencyProperty UnselectedBorderProperty = Brush(nameof(UnselectedBorder), 0xC2, 0xC8, 0xD1);
    public static readonly DependencyProperty UnselectedTextProperty = Brush(nameof(UnselectedText), 0x67, 0x70, 0x7C);

    public Brush SelectedFill { get => (Brush)GetValue(SelectedFillProperty); set => SetValue(SelectedFillProperty, value); }
    public Brush SelectedBorder { get => (Brush)GetValue(SelectedBorderProperty); set => SetValue(SelectedBorderProperty, value); }
    public Brush SelectedText { get => (Brush)GetValue(SelectedTextProperty); set => SetValue(SelectedTextProperty, value); }
    public Brush UnselectedFill { get => (Brush)GetValue(UnselectedFillProperty); set => SetValue(UnselectedFillProperty, value); }
    public Brush UnselectedBorder { get => (Brush)GetValue(UnselectedBorderProperty); set => SetValue(UnselectedBorderProperty, value); }
    public Brush UnselectedText { get => (Brush)GetValue(UnselectedTextProperty); set => SetValue(UnselectedTextProperty, value); }

    // ---- row tracking ----------------------------------------------

    private static void OnMonitorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MonitorLayoutControl)d;

        if (e.OldValue is INotifyCollectionChanged oldObservable)
            oldObservable.CollectionChanged -= control.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newObservable)
            newObservable.CollectionChanged += control.OnCollectionChanged;

        control.RebindRows();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebindRows();

    private void RebindRows()
    {
        foreach (var row in _tracked)
            row.PropertyChanged -= OnRowPropertyChanged;
        _tracked.Clear();

        if (Monitors is not null)
        {
            foreach (var item in Monitors)
            {
                if (item is not MonitorRow row) continue;
                row.PropertyChanged += OnRowPropertyChanged;
                _tracked.Add(row);
            }
        }

        InvalidateVisual();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorRow.IsSelected))
            InvalidateVisual();
    }

    // ---- interaction ---------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? 260 : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsEnabled) return;

        var point = e.GetPosition(this);
        for (int i = _hitBoxes.Count - 1; i >= 0; i--)
        {
            if (_hitBoxes[i].Box.Contains(point))
            {
                _hitBoxes[i].Row.IsSelected = !_hitBoxes[i].Row.IsSelected;
                e.Handled = true;
                return;
            }
        }
    }

    // ---- rendering ----------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        _hitBoxes.Clear();
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        double dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        if (_tracked.Count == 0)
        {
            var empty = MakeText("No monitors detected", 13, UnselectedText, dpiScale);
            dc.DrawText(empty, new Point((ActualWidth - empty.Width) / 2, (ActualHeight - empty.Height) / 2));
            return;
        }

        int minX = _tracked.Min(r => r.Info.Left);
        int minY = _tracked.Min(r => r.Info.Top);
        int maxX = _tracked.Max(r => r.Info.Right);
        int maxY = _tracked.Max(r => r.Info.Bottom);

        const double pad = 16;
        double virtualWidth = Math.Max(1, maxX - minX);
        double virtualHeight = Math.Max(1, maxY - minY);
        double scale = Math.Min(
            (ActualWidth - 2 * pad) / virtualWidth,
            (ActualHeight - 2 * pad) / virtualHeight);
        if (scale <= 0 || double.IsInfinity(scale)) return;

        double offsetX = pad + (ActualWidth - 2 * pad - virtualWidth * scale) / 2;
        double offsetY = pad + (ActualHeight - 2 * pad - virtualHeight * scale) / 2;

        var selectedPen = new Pen(SelectedBorder, 2);
        var unselectedPen = new Pen(UnselectedBorder, 1);
        selectedPen.Freeze();
        unselectedPen.Freeze();

        dc.PushOpacity(IsEnabled ? 1.0 : 0.4);

        foreach (var row in _tracked)
        {
            var box = new Rect(
                offsetX + (row.Info.Left - minX) * scale,
                offsetY + (row.Info.Top - minY) * scale,
                row.Info.Width * scale,
                row.Info.Height * scale);
            _hitBoxes.Add((row, box));

            var inner = box;
            inner.Inflate(-3, -3);
            if (inner.Width <= 0 || inner.Height <= 0) continue;

            bool selected = row.IsSelected;
            dc.DrawRoundedRectangle(
                selected ? SelectedFill : UnselectedFill,
                selected ? selectedPen : unselectedPen,
                inner, 6, 6);

            var textBrush = selected ? SelectedText : UnselectedText;

            double numberSize = Math.Clamp(inner.Height * 0.4, 12, 40);
            var number = MakeText(row.Id.ToString(), numberSize, textBrush, dpiScale, bold: true);
            var caption = MakeText(
                $"{row.Info.Width}×{row.Info.Height}" + (row.Info.IsPrimary ? "  •  primary" : ""),
                Math.Clamp(inner.Height * 0.12, 9, 12), textBrush, dpiScale);

            double blockHeight = number.Height + caption.Height + 2;
            double top = inner.Y + (inner.Height - blockHeight) / 2;

            if (number.Width < inner.Width && blockHeight < inner.Height)
            {
                dc.DrawText(number, new Point(inner.X + (inner.Width - number.Width) / 2, top));
                if (caption.Width < inner.Width)
                    dc.DrawText(caption, new Point(inner.X + (inner.Width - caption.Width) / 2, top + number.Height + 2));
            }
        }

        dc.Pop();
    }

    private static FormattedText MakeText(string text, double size, Brush brush, double dpiScale, bool bold = false)
        => new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            dpiScale);
}
