using AnprViewer.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using AnprViewer.Services;

namespace AnprViewer.Services;

public class PdfExportService : IPdfExportService
{
    private readonly IDatabaseService _db;

    public PdfExportService(IDatabaseService db)
    {
        _db = db;
    }

    public async Task ExportRecordAsync(HistoricoRecord record, string filePath)
    {
        var imgFrontal = await _db.FetchImageAsync(record.NumeroLinea, ImageKind.Frontal);
        var imgTrasera = await _db.FetchImageAsync(record.NumeroLinea, ImageKind.Trasera);
        var imgMat = await _db.FetchImageAsync(record.NumeroLinea, ImageKind.Leida);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);

                page.Header().Text("Detalle de Matrícula")
                    .FontSize(20)
                    .Bold();

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text($"Matrícula: {record.MatriculaLeida}");
                    col.Item().Text($"Fecha: {record.FHGeneracion}");
                    col.Item().Text($"Terminal: {record.DescTerminal}");
                    col.Item().Text($"Movimiento: {record.TipoMovimientoDesc}");
                    col.Item().Text($"Fiabilidad: {record.FiabilidadMatricula}");

                    if (imgMat != null)
                        col.Item().Image(imgMat);

                    if (imgFrontal != null)
                        col.Item().Image(imgFrontal);

                    if (imgTrasera != null)
                        col.Item().Image(imgTrasera);
                });
            });
        })
        .GeneratePdf(filePath);

        await Task.CompletedTask;
    }
}