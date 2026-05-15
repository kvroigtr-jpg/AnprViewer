using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnprViewer.Models;

namespace AnprViewer.Services;

/// <summary>
/// Exporta una colección de registros a un PDF en formato "informe de listado":
/// portada con metadatos, tabla con todas las filas y paginación automática.
///
/// Complementario a <see cref="IPdfExportService"/> (que exporta UN único registro
/// con sus imágenes en resolución original).
/// </summary>
public interface IListPdfExportService
{
    /// <summary>
    /// Genera un PDF con el listado de registros.
    /// </summary>
    /// <param name="records">Filas a incluir (típicamente la página visible).</param>
    /// <param name="filters">Filtros aplicados, para mostrarlos en la portada (opcional).</param>
    /// <param name="outputPath">Ruta destino del PDF.</param>
    /// <param name="ct">Token de cancelación.</param>
    Task ExportListAsync(
        IReadOnlyList<HistoricoRecord> records,
        QueryFilters? filters,
        string outputPath,
        CancellationToken ct = default);
}
