using System.Collections.Generic;
using System.Threading.Tasks;
using AnprViewer.Models;

namespace AnprViewer.Services;

/// <summary>
/// Exportación del LISTADO a PDF (formato apaisado con tabla).
/// Soporta listado de HISTORICO y de PRESENTES.
/// </summary>
public interface IListPdfExportService
{
    /// <summary>Listado de HISTORICO con filtros aplicados.</summary>
    Task ExportListAsync(
        IReadOnlyList<HistoricoRecord> records,
        QueryFilters? filters,
        string filePath);

    /// <summary>Listado de PRESENTES con filtros aplicados.</summary>
    Task ExportPresentesListAsync(
        IReadOnlyList<PresenteRecord> records,
        QueryFilters? filters,
        string filePath);
}
