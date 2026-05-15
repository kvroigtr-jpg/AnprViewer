using System;
using System.Threading.Tasks;
using System.Windows;
using AnprViewer.Models;
using AnprViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnprViewer.ViewModels;

/// <summary>
/// ViewModel de la pantalla de conexión a SQL Server.
/// Devuelve true al cerrar la ventana cuando la conexión fue validada.
/// </summary>
public sealed partial class ConnectionViewModel : ObservableObject
{
    private readonly IDatabaseService          _db;
    private readonly IConnectionSettingsStore  _store;

    [ObservableProperty] private string _server = "localhost";
    [ObservableProperty] private string _port = "1433";
    [ObservableProperty] private string _instanceName = "";
    [ObservableProperty] private string _database = "";
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private bool   _integratedSecurity;
    [ObservableProperty] private bool   _encrypt;
    [ObservableProperty] private bool   _trustServerCertificate = true;
    [ObservableProperty] private bool   _rememberSettings = true;

    // Password se enlaza desde code-behind por seguridad (PasswordBox no expone Password como DP)
    public string Password { get; set; } = "";

    [ObservableProperty] private string _statusMessage = "Listo para conectar.";
    [ObservableProperty] private string _statusKind = "info";   // info | ok | err
    [ObservableProperty] private bool   _isBusy;

    /// <summary>Resultado al cerrar la ventana (true = conectado).</summary>
    public bool? DialogResult { get; private set; }

    /// <summary>El host (Window) se enlaza aquí para poder cerrar desde el VM.</summary>
    public Action<bool>? CloseRequest { get; set; }

    public ConnectionViewModel(IDatabaseService db, IConnectionSettingsStore store)
    {
        _db    = db;
        _store = store;
        LoadStored();
    }

    private void LoadStored()
    {
        var s = _store.Load();
        if (s is null) return;
        Server                 = s.Server;
        Port                   = s.Port?.ToString() ?? "1433";
        InstanceName           = s.InstanceName ?? "";
        Database               = s.Database;
        Username               = s.Username;
        IntegratedSecurity     = s.IntegratedSecurity;
        Encrypt                = s.Encrypt;
        TrustServerCertificate = s.TrustServerCertificate;
    }

    private ConnectionSettings BuildSettings() => new()
    {
        Server                 = Server.Trim(),
        Port                   = int.TryParse(Port, out var p) ? p : null,
        InstanceName           = string.IsNullOrWhiteSpace(InstanceName) ? null : InstanceName.Trim(),
        Database               = Database.Trim(),
        IntegratedSecurity     = IntegratedSecurity,
        Username               = Username.Trim(),
        Password               = Password,
        Encrypt                = Encrypt,
        TrustServerCertificate = TrustServerCertificate,
    };

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task TestAsync()
    {
        IsBusy = true;
        StatusKind = "info";
        StatusMessage = "Probando conexión…";
        try
        {
            var ok = await _db.TestConnectionAsync(BuildSettings());
            StatusKind    = ok ? "ok" : "err";
            StatusMessage = ok ? "Conexión correcta · BD responde." : "La BD no respondió como se esperaba.";
        }
        catch (Exception ex)
        {
            StatusKind    = "err";
            StatusMessage = "Fallo: " + FlattenMessage(ex);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        StatusKind = "info";
        StatusMessage = "Conectando…";
        try
        {
            var settings = BuildSettings();
            var ok = await _db.TestConnectionAsync(settings);
            if (!ok)
            {
                StatusKind    = "err";
                StatusMessage = "No se pudo validar la BD.";
                return;
            }

            _db.Configure(settings);
            if (RememberSettings) _store.Save(settings);

            DialogResult = true;
            CloseRequest?.Invoke(true);
        }
        catch (Exception ex)
        {
            StatusKind    = "err";
            StatusMessage = "Fallo: " + FlattenMessage(ex);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        CloseRequest?.Invoke(false);
    }

    private bool CanExecute()
        => !IsBusy
        && !string.IsNullOrWhiteSpace(Server)
        && !string.IsNullOrWhiteSpace(Database)
        && (IntegratedSecurity || !string.IsNullOrWhiteSpace(Username));

    // CommunityToolkit.Mvvm regenera RaiseCanExecuteChanged cuando cambian
    // las propiedades observables que se referencian en CanExecute. Por si acaso,
    // notificamos manualmente desde el setter:
    partial void OnIsBusyChanged(bool value)             => NotifyAll();
    partial void OnServerChanged(string value)           => NotifyAll();
    partial void OnDatabaseChanged(string value)         => NotifyAll();
    partial void OnUsernameChanged(string value)         => NotifyAll();
    partial void OnIntegratedSecurityChanged(bool value) => NotifyAll();

    private void NotifyAll()
    {
        TestCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
    }

    private static string FlattenMessage(Exception ex)
    {
        var msg = ex.Message;
        var cur = ex.InnerException;
        while (cur is not null) { msg += " · " + cur.Message; cur = cur.InnerException; }
        return msg;
    }
}
