using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AnprViewer.Models;
using AnprViewer.ViewModels;
using Microsoft.Win32;

namespace AnprViewer.Views;

public partial class ImageViewerWindow : Window
{
    public ImageViewerWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Zoomer.ZoomChanged += z => ZoomLabel.Text = $"{Math.Round(z * 100)}%";
        };
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // ── Botones del visor ──────────────────────────────────────
    private void OnZoomIn(object sender, RoutedEventArgs e)  => Zoomer.ZoomBy(1.25);
    private void OnZoomOut(object sender, RoutedEventArgs e) => Zoomer.ZoomBy(0.8);
    private void OnFit(object sender, RoutedEventArgs e)     => Zoomer.ResetZoom();
    private void OnRotate(object sender, RoutedEventArgs e)  => Zoomer.Rotate90();

    private void OnPrevKind(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImageViewerViewModel vm)
            _ = vm.PrevKindCommand.ExecuteAsync(null);
    }
    private void OnNextKind(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImageViewerViewModel vm)
            _ = vm.NextKindCommand.ExecuteAsync(null);
    }

    private async void OnStripItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ImageKind kind
            && DataContext is ImageViewerViewModel vm)
        {
            await vm.SelectKindCommand.ExecuteAsync(kind);
        }
    }

    // ── Atajos de teclado ──────────────────────────────────────
    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ImageViewerViewModel vm) return;

        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.Add: case Key.OemPlus:   Zoomer.ZoomBy(1.25); break;
            case Key.Subtract: case Key.OemMinus: Zoomer.ZoomBy(0.8); break;
            case Key.D0: case Key.NumPad0:    Zoomer.ResetZoom(); break;
            case Key.R: Zoomer.Rotate90(); break;
            case Key.Left:  await vm.PrevKindCommand.ExecuteAsync(null); break;
            case Key.Right: await vm.NextKindCommand.ExecuteAsync(null); break;
            case Key.D: OnSave(this, new RoutedEventArgs()); break;
        }
    }

    // ── Guardar a disco ────────────────────────────────────────
    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ImageViewerViewModel vm || vm.CurrentImage is null) return;

        var dlg = new SaveFileDialog
        {
            FileName = $"{vm.Record.MatriculaLeida ?? "img"}_{vm.CurrentKind}.jpg",
            Filter   = "JPEG (*.jpg)|*.jpg|PNG (*.png)|*.png",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            BitmapEncoder encoder = dlg.FilterIndex == 2
                ? new PngBitmapEncoder()
                : new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)vm.CurrentImage));
            using var fs = File.Create(dlg.FileName);
            encoder.Save(fs);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo guardar la imagen:\n{ex.Message}", "Guardar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
