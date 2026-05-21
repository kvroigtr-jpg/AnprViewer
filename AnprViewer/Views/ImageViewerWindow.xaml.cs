using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AnprViewer.ViewModels;
using Microsoft.Win32;

namespace AnprViewer.Views;

public partial class ImageViewerWindow : Window
{
    private ImageViewerViewModel? _vm;

    public ImageViewerWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            // Sincroniza la etiqueta de zoom con el ZoomableImage
            Zoomer.ZoomChanged += z => ZoomLabel.Text = $"{Math.Round(z * 100)}%";

            // Engancha el VM una vez cargado el visual tree
            HookViewModel();
            PushCurrentImage();
        };

        DataContextChanged += (_, _) =>
        {
            HookViewModel();
            PushCurrentImage();
        };
    }

    private void HookViewModel()
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as ImageViewerViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageViewerViewModel.CurrentImage))
            PushCurrentImage();
    }

    /// <summary>Empuja explícitamente la imagen actual al ZoomableImage y resetea zoom.</summary>
    private void PushCurrentImage()
    {
        if (_vm is null) return;
        Zoomer.Source = _vm.CurrentImage;
        Zoomer.ResetZoom();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // ── Click en thumb del panel lateral ──
    private void OnThumbClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.Tag is not string key) return;
        if (DataContext is not ImageViewerViewModel vm) return;

        vm.SelectKeyCommand.Execute(key);
    }

    /// <summary>Click en placa coche/moto: selecciona la imagen de matrícula correspondiente.</summary>
    private void OnPlateClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (DataContext is not ImageViewerViewModel vm) return;
        var which = fe.Tag as string;

        string? key = which switch
        {
            "Car"  => vm.IsHistorico ? "Leida"   : "Entrada",
            "Moto" => vm.IsHistorico ? "MatTras" : "PresTrasera",
            _      => null,
        };

        if (key is null) return;
        vm.SelectKeyCommand.Execute(key);
    }

    // ── HUD inferior ────────────────────────────────────────────
    private void OnZoomIn(object sender, RoutedEventArgs e)  => Zoomer.ZoomBy(1.25);
    private void OnZoomOut(object sender, RoutedEventArgs e) => Zoomer.ZoomBy(0.8);
    private void OnFit(object sender, RoutedEventArgs e)     => Zoomer.ResetZoom();
    private void OnRotate(object sender, RoutedEventArgs e)  => Zoomer.Rotate90();

    private void OnPrevKind(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImageViewerViewModel vm)
            vm.PrevKindCommand.Execute(null);
    }

    private void OnNextKind(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImageViewerViewModel vm)
            vm.NextKindCommand.Execute(null);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ImageViewerViewModel vm || vm.CurrentImage is null) return;

        var plate = (vm.PlateText ?? "img").Replace(" ", "").Replace("/", "_");
        var dlg = new SaveFileDialog
        {
            FileName = $"{plate}_{vm.CurrentKey}.jpg",
            Filter   = "JPEG (*.jpg)|*.jpg|PNG (*.png)|*.png",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            BitmapEncoder encoder = dlg.FilterIndex == 2
                ? new PngBitmapEncoder()
                : new JpegBitmapEncoder { QualityLevel = 95 };
            encoder.Frames.Add(BitmapFrame.Create(vm.CurrentImage));
            using var fs = File.Create(dlg.FileName);
            encoder.Save(fs);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo guardar la imagen:\n{ex.Message}", "Guardar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Atajos de teclado ───────────────────────────────────────
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:                       Close(); break;
            case Key.Add: case Key.OemPlus:        Zoomer.ZoomBy(1.25); break;
            case Key.Subtract: case Key.OemMinus:  Zoomer.ZoomBy(0.8); break;
            case Key.D0: case Key.NumPad0:         Zoomer.ResetZoom(); break;
            case Key.R:                            Zoomer.Rotate90(); break;
            case Key.Left:                         OnPrevKind(this, new RoutedEventArgs()); break;
            case Key.Right:                        OnNextKind(this, new RoutedEventArgs()); break;
            case Key.D:                            OnSave(this, new RoutedEventArgs()); break;
        }
    }
}
