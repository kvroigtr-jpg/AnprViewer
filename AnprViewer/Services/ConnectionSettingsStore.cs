using System;
using System.IO;
using System.Text.Json;
using AnprViewer.Models;

namespace AnprViewer.Services;

public interface IConnectionSettingsStore
{
    ConnectionSettings? Load();
    void Save(ConnectionSettings settings);
    void Clear();
}

/// <summary>
/// Persistencia de la configuración de conexión en %APPDATA%\AnprViewer\connection.json
/// La contraseña NO se persiste. Si en producción necesitas guardarla, usa
/// ProtectedData (DPAPI) por usuario.
/// </summary>
public sealed class ConnectionSettingsStore : IConnectionSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AnprViewer", "connection.json");

    public ConnectionSettings? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<ConnectionSettings>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Save(ConnectionSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            // Clonar y eliminar la contraseña antes de persistir
            var safe = new ConnectionSettings
            {
                Server = settings.Server,
                Port = settings.Port,
                InstanceName = settings.InstanceName,
                Database = settings.Database,
                IntegratedSecurity = settings.IntegratedSecurity,
                Username = settings.Username,
                Password = "",
                TrustServerCertificate = settings.TrustServerCertificate,
                Encrypt = settings.Encrypt,
                ConnectTimeoutSeconds = settings.ConnectTimeoutSeconds,
                CommandTimeoutSeconds = settings.CommandTimeoutSeconds,
                AutoConnect = settings.AutoConnect,
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(safe,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Persistencia silenciosa: no impedir trabajar si no se puede guardar.
        }
    }

    public void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch { /* ignore */ }
    }
}
