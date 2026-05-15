using System.Threading.Tasks;
using AnprViewer.Models;

namespace AnprViewer.Services;

public interface IRecordPdfExportService
{
    Task ExportRecordAsync(HistoricoRecord record, string filePath);
}