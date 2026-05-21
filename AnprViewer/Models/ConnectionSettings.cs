using Microsoft.Data.SqlClient;

namespace AnprViewer.Models;

/// <summary>
/// Modelo de configuración de conexión a SQL Server.
/// Lo persiste <see cref="AnprViewer.Services.ConnectionSettingsStore"/> en
/// %APPDATA%\AnprViewer\connection.json y lo consumen los servicios de datos.
///
/// Las propiedades están alineadas EXACTAMENTE con ConnectionSettingsStore
/// (que copia Server, Port, InstanceName, Database, IntegratedSecurity, Username,
/// Password, TrustServerCertificate, Encrypt, ConnectTimeoutSeconds,
/// CommandTimeoutSeconds y AutoConnect) y con ConnectionViewModel
/// (que asigna Username/Database/Server como string no-null).
/// </summary>
public sealed class ConnectionSettings
{
    /// <summary>Host o IP del servidor SQL (p.ej. "localhost", "192.168.1.10").</summary>
    public string Server { get; set; } = "";

    /// <summary>Instancia con nombre (p.ej. "SQLEXPRESS"). Opcional.</summary>
    public string? InstanceName { get; set; }

    /// <summary>Puerto TCP. Opcional (por defecto 1433 si no se indica).</summary>
    public int? Port { get; set; }

    /// <summary>Base de datos destino.</summary>
    public string Database { get; set; } = "";

    /// <summary>
    /// Usuario SQL (cuando no se usa seguridad integrada).
    /// NOTA: string no-null porque ConnectionViewModel.LoadStored() hace
    /// Username = s.Username; sobre una propiedad string.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Contraseña SQL. No se persiste (el Store la guarda como "").
    /// Se introduce en la ventana de conexión en cada arranque.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>Usar autenticación integrada de Windows.</summary>
    public bool IntegratedSecurity { get; set; }

    /// <summary>Cifrar la conexión.</summary>
    public bool Encrypt { get; set; }

    /// <summary>Confiar en el certificado del servidor (entornos sin CA).</summary>
    public bool TrustServerCertificate { get; set; } = true;

    /// <summary>Conectar automáticamente al iniciar si la última conexión fue válida.</summary>
    public bool AutoConnect { get; set; }

    /// <summary>Timeout de apertura de conexión en segundos.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 15;

    /// <summary>Timeout de los comandos en segundos (consultas largas sobre HISTORICO).</summary>
    public int CommandTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Construye la cadena de conexión de Microsoft.Data.SqlClient.
    /// </summary>
    public string ToConnectionString()
    {
        // Componer DataSource como host[\instancia][,puerto]
        var dataSource = Server ?? "";
        if (!string.IsNullOrWhiteSpace(InstanceName))
            dataSource += "\\" + InstanceName.Trim();
        if (Port.HasValue && Port.Value > 0)
            dataSource += "," + Port.Value;

        var builder = new SqlConnectionStringBuilder
        {
            DataSource             = dataSource,
            InitialCatalog         = Database ?? "",
            IntegratedSecurity     = IntegratedSecurity,
            Encrypt                = Encrypt,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout         = ConnectTimeoutSeconds > 0 ? ConnectTimeoutSeconds : 15,
        };

        if (!IntegratedSecurity)
        {
            builder.UserID   = Username ?? "";
            builder.Password = Password ?? "";
        }

        return builder.ConnectionString;
    }

    /// <summary>Copia superficial (útil para editar sin tocar el original).</summary>
    public ConnectionSettings Clone() => (ConnectionSettings)MemberwiseClone();
}
