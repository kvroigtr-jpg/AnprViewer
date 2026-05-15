using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AnprViewer.ViewModels;

namespace AnprViewer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Lazy load real: cuando el contenedor de la miniatura se carga
    /// (= la fila ha entrado en el viewport virtualizado), pedimos la imagen.
    /// El propio DataGrid virtualiza con `Recycling`, así que sólo se
    /// ejecuta para las filas realmente visibles.
    /// </summary>
    private async void OnThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is HistoricoRowViewModel row)
        {
            await row.EnsureThumbnailAsync();
        }
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Evitar disparar el detalle al hacer doble click sobre cabeceras / scrollbars
        if (e.OriginalSource is DependencyObject src && FindAncestor<DataGridRow>(src) is null)
            return;

        OpenViewerForCurrentSelection();
    }

    private void OnOpenViewerClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HistoricoRowViewModel row)
            OpenViewer(row);
    }

    private void OnCopyPlateClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HistoricoRowViewModel row
            && !string.IsNullOrEmpty(row.Plate) && row.Plate != "—")
        {
            try { Clipboard.SetText(row.Plate); } catch { /* ignore */ }
        }
    }

    private void OpenViewerForCurrentSelection()
    {
        if (DataContext is MainViewModel vm && vm.SelectedRow is { } row)
            OpenViewer(row);
    }

    private void OpenViewer(HistoricoRowViewModel row)
    {
        if (!row.HasAnyImage) return;
        var vm = new ImageViewerViewModel(row.Record, App.ImageCache);
        var win = new ImageViewerWindow
        {
            Owner = this,
            DataContext = vm,
        };
        _ = vm.LoadCurrentAsync();
        win.Show();
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            if (d is T t) return t;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}
