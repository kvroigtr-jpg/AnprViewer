using System;

namespace AnprViewer.Models;

/// <summary>Filtros aplicables al listado HISTORICO. Todos opcionales.</summary>
public sealed class QueryFilters
{
    public string?   Plate    { get; set; }
    public DateTime? From     { get; set; }
    public DateTime? To       { get; set; }
    public string?   Type     { get; set; }
    public string?   Terminal { get; set; }
    public float     MinConf  { get; set; }
}

/// <summary>
/// Parámetros de conexión a SQL Server.
/// Soporta autenticación SQL y autenticación integrada de Windows.
/// </summary>
public sealed class ConnectionSettings
{
    public string Server        { get; set; } = "localhost";
    public int?   Port          { get; set; } = 1433;
    public string? InstanceName { get; set; }
    public string Database      { get; set; } = "";

    /// <summary>true → Windows Auth · false → user/password</summary>
    public bool   IntegratedSecurity { get; set; }
    public string Username { get; set; } = "";

    /// <summary>Solo en memoria; no persistir en disco en producción.</summary>
    public string Password { get; set; } = "";

    public bool TrustServerCertificate { get; set; } = true;
    public bool Encrypt                { get; set; }

    public int ConnectTimeoutSeconds { get; set; } = 15;
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>Construye la cadena de conexión sin filtrar la contraseña en logs.</summary>
    public string ToConnectionString()
    {
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(InstanceName)
                ? (Port.HasValue ? $"{Server},{Port.Value}" : Server)
                : $"{Server}\\{InstanceName}",
            InitialCatalog          = Database,
            TrustServerCertificate  = TrustServerCertificate,
            Encrypt                 = Encrypt,
            ConnectTimeout          = ConnectTimeoutSeconds,
            ApplicationName         = "ANPR Viewer",
            MultipleActiveResultSets = true,
        };

        if (IntegratedSecurity)
        {
            b.IntegratedSecurity = true;
        }
        else
        {
            b.UserID   = Username;
            b.Password = Password;
        }
        return b.ConnectionString;
    }
}
