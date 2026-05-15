using System;
using System.Threading;
using System.Threading.Tasks;
using AnprViewer.Models;

namespace AnprViewer.Services;

/// <summary>
/// Acceso a datos sobre SQL Server. Implementado por SqlServerDatabaseService.
/// Todas las operaciones son asíncronas y respetan CancellationToken.
/// </summary>
public interface IDatabaseService
{
    /// <summary>true si hay una conexión configurada y verificada.</summary>
    bool IsConfigured { get; }

    /// <summary>Datos del servidor / BD a los que estamos conectados.</summary>
    ConnectionSettings? CurrentSettings { get; }

    /// <summary>Valida que las credenciales conectan y la BD responde.</summary>
    Task<bool> TestConnectionAsync(ConnectionSettings settings, CancellationToken ct = default);

    /// <summary>Fija la cadena de conexión activa.</summary>
    void Configure(ConnectionSettings settings);

    /// <summary>Consulta paginada con filtros sobre HISTORICO.</summary>
    Task<PagedResult<HistoricoRecord>> QueryHistoricoAsync(
        QueryFilters filters, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Lee los bytes de una imagen concreta de un registro.</summary>
    Task<byte[]?> FetchImageAsync(int numeroLinea, ImageKind kind, CancellationToken ct = default);
}
