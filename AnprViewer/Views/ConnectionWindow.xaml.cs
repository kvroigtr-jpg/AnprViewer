using System.Windows;
using System.Windows.Input;
using AnprViewer.ViewModels;

namespace AnprViewer.Views;

public partial class ConnectionWindow : Window
{
    public ConnectionWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is ConnectionViewModel vm)
            {
                // El VM controla el cierre de la ventana
                vm.CloseRequest = ok => { DialogResult = ok; Close(); };
                // Sincronizar la contraseña del PasswordBox al VM (no es bindable directo)
                PwdBox.PasswordChanged += (_, _) => vm.Password = PwdBox.Password;
                // Reflejar el estado inicial del radio "SQL Server"
                RbSqlAuth.IsChecked = !vm.IntegratedSecurity;
            }
        };
    }

    private void OnTopbarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void OnSqlAuthChecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectionViewModel vm) vm.IntegratedSecurity = false;
    }
    private void OnWinAuthChecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectionViewModel vm) vm.IntegratedSecurity = true;
    }
}
