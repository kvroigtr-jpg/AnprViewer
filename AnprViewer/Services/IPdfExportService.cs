using AnprViewer.Models;
using System.Threading.Tasks;

namespace AnprViewer.Services;

public interface IPdfExportService
{
    Task ExportRecordAsync(HistoricoRecord record, string filePath);
}