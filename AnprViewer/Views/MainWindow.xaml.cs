using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AnprViewer.Services;
using AnprViewer.ViewModels;

namespace AnprViewer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // ────────── tema ──────────
    private void OnThemeDarkClick(object sender, RoutedEventArgs e)  => ThemeService.Apply(ThemeService.Mode.Dark);
    private void OnThemeLightClick(object sender, RoutedEventArgs e) => ThemeService.Apply(ThemeService.Mode.Light);

    // ─────── HISTORICO row handlers ───────
    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.SelectedRow is null) return;
        OpenHistoricoViewer(vm.SelectedRow);
    }

    private void OnOpenViewerClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not HistoricoRowViewModel row) return;
        OpenHistoricoViewer(row);
    }

    private void OnCopyPlateClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not HistoricoRowViewModel row) return;
        try { Clipboard.SetText(row.Record.MatriculaLeida ?? ""); } catch { }
    }

    private async void OnThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not HistoricoRowViewModel row) return;
        try { await row.EnsureThumbnailsAsync(); } catch { }
    }

    private void OpenHistoricoViewer(HistoricoRowViewModel row)
    {
        var vm = new ImageViewerViewModel(row.Record, App.ImageCache, App.PresenteImages);
        var win = new ImageViewerWindow
        {
            DataContext = vm,
            Owner = this,
        };
        win.Loaded += async (_, _) =>
        {
            try { await vm.LoadAllAsync(); } catch { }
        };
        win.Show();
    }

    // ─────── PRESENTES row handlers ───────
    private void OnPresenteRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.SelectedPresente is null) return;
        OpenPresenteViewer(vm.SelectedPresente);
    }

    private void OnOpenPresenteViewerClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not PresenteRowViewModel row) return;
        OpenPresenteViewer(row);
    }

    private void OnCopyPresentePlateClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not PresenteRowViewModel row) return;
        try { Clipboard.SetText(row.Record.MatriculaEntrada ?? ""); } catch { }
    }

    private async void OnPresenteThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not PresenteRowViewModel row) return;
        try { await row.EnsureThumbnailsAsync(); } catch { }
    }

    private void OpenPresenteViewer(PresenteRowViewModel row)
    {
        var vm = new ImageViewerViewModel(row.Record, App.ImageCache, App.PresenteImages);
        var win = new ImageViewerWindow
        {
            DataContext = vm,
            Owner = this,
        };
        win.Loaded += async (_, _) =>
        {
            try { await vm.LoadAllAsync(); } catch { }
        };
        win.Show();
    }
}
