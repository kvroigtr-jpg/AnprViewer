using System.Threading.Tasks;
using AnprViewer.Models;

namespace AnprViewer.Services;

/// <summary>
/// PDF de detalle de un registro individual, con imágenes y placa.
/// Soporta los dos modos de vista (HISTORICO y PRESENTES).
/// </summary>
public interface IRecordPdfExportService
{
    /// <summary>PDF de un registro de HISTORICO (placa coche = Leida, placa moto = MatTras).</summary>
    Task ExportHistoricoAsync(HistoricoRecord record, string filePath);

    /// <summary>PDF de un registro de PRESENTES (placa coche = Entrada, placa moto = Trasera).</summary>
    Task ExportPresenteAsync(PresenteRecord record, string filePath);

    /// <summary>Alias para compatibilidad: redirige a ExportHistoricoAsync.</summary>
    Task ExportRecordAsync(HistoricoRecord record, string filePath);
}
