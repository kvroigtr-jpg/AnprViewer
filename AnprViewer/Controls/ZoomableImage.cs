using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AnprViewer.Controls;

/// <summary>
/// Control para visualizar imágenes con zoom (rueda y centrado en el cursor),
/// pan por arrastre y rotación. Pensado para imágenes grandes; el origen del
/// render se reposiciona, no se recrea el bitmap.
/// </summary>
public sealed class ZoomableImage : Border
{
    private readonly Image           _image;
    private readonly ScaleTransform  _scale     = new(1, 1);
    private readonly TranslateTransform _translate = new(0, 0);
    private readonly RotateTransform   _rotate    = new(0);

    private Point _lastMousePos;
    private bool  _dragging;

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ZoomableImage),
            new PropertyMetadata(null, OnSourceChanged));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double MinZoom { get; set; } = 0.05;
    public double MaxZoom { get; set; } = 20.0;

    public double CurrentZoom => _scale.ScaleX;

    public event Action<double>? ZoomChanged;

    public ZoomableImage()
    {
        ClipToBounds = true;
        // ⚠️ DynamicResource para que reaccione al cambio de tema global
        SetResourceReference(BackgroundProperty, "Bg0");
        Cursor = Cursors.Hand;

        _image = new Image
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Stretch             = Stretch.None,
            UseLayoutRounding   = false,
            SnapsToDevicePixels = false,
        };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        Child = _image;

        var tg = new TransformGroup();
        tg.Children.Add(_scale);
        tg.Children.Add(_rotate);
        tg.Children.Add(_translate);
        _image.RenderTransform = tg;
        _image.RenderTransformOrigin = new Point(0.5, 0.5);

        MouseWheel          += OnMouseWheel;
        MouseLeftButtonDown += OnMouseDown;
        MouseLeftButtonUp   += OnMouseUp;
        MouseMove           += OnMouseMove;
        SizeChanged         += (_, _) => FitToView();
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (ZoomableImage)d;
        c._image.Source = (ImageSource?)e.NewValue;
        c.Dispatcher.BeginInvoke(new Action(c.FitToView), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void FitToView()
    {
        if (_image.Source is not BitmapSource bmp) return;
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        double w = bmp.PixelWidth, h = bmp.PixelHeight;
        if (w <= 0 || h <= 0) return;

        double s = Math.Min(ActualWidth * 0.92 / w, ActualHeight * 0.88 / h);
        if (double.IsNaN(s) || double.IsInfinity(s) || s <= 0) s = 1;

        SetZoom(s);
        _translate.X = 0;
        _translate.Y = 0;
        _rotate.Angle = 0;
    }

    public void ResetZoom() => FitToView();

    public void Rotate90()
    {
        _rotate.Angle = (_rotate.Angle + 90) % 360;
    }

    public void ZoomBy(double factor, Point? center = null)
    {
        var oldScale = _scale.ScaleX;
        var newScale = Math.Clamp(oldScale * factor, MinZoom, MaxZoom);
        if (Math.Abs(newScale - oldScale) < 1e-6) return;

        if (center is { } c)
        {
            var rect = new Rect(0, 0, ActualWidth, ActualHeight);
            var ox = c.X - rect.Width / 2;
            var oy = c.Y - rect.Height / 2;
            _translate.X = ox - (ox - _translate.X) * (newScale / oldScale);
            _translate.Y = oy - (oy - _translate.Y) * (newScale / oldScale);
        }
        SetZoom(newScale);
    }

    private void SetZoom(double s)
    {
        _scale.ScaleX = s;
        _scale.ScaleY = s;
        ZoomChanged?.Invoke(s);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.15 : 0.87;
        ZoomBy(factor, e.GetPosition(this));
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _lastMousePos = e.GetPosition(this);
        Cursor = Cursors.SizeAll;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(this);
        _translate.X += p.X - _lastMousePos.X;
        _translate.Y += p.Y - _lastMousePos.Y;
        _lastMousePos = p;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        Cursor = Cursors.Hand;
        ReleaseMouseCapture();
    }
}
