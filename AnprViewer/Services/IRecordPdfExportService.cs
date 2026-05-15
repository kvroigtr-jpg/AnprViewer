using System;
using System.IO;
using System.Threading.Tasks;
using AnprViewer.Models;
using AnprViewer.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AnprViewer.Services;

public class RecordPdfExportService : IRecordPdfExportService
{
    private readonly IDatabaseService _db;

    public RecordPdfExportService(IDatabaseService db)
    {
        _db = db;
    }

    public async Task ExportRecordAsync(HistoricoRecord record, string filePath)
    {
        var frontal = await _db.FetchImageAsync(record.NumeroLinea, ImageKind.Frontal);
        var trasera = await _db.FetchImageAsync(record.NumeroLinea, ImageKind.Trasera);
        var leida   = await _db.FetchImageAsync(record.NumeroLinea, ImageKind.Leida);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Content().Column(col =>
                {
                    // ================= HEADER =================
                    col.Item().Text($"MATRÍCULA {record.MatriculaLeida}")
                        .FontSize(26)
                        .Bold()
                        .FontColor(Colors.Blue.Medium);

                    col.Item().Text($"Registro #{record.NumeroLinea}")
                        .FontSize(12)
                        .FontColor(Colors.Grey.Darken2);

                    col.Item().PaddingBottom(10);

                    // ================= INFO =================
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Fecha: {record.FHGeneracion}");
                            c.Item().Text($"Terminal: {record.DescTerminal ?? "N/A"}");
                            c.Item().Text($"Movimiento: {record.TipoMovimientoDesc ?? "N/A"}");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Fiabilidad: {(record.FiabilidadMatricula ?? 0):0.00}%")
                                .Bold()
                                .FontSize(12);
                        });
                    });

                    col.Item().PaddingBottom(15);

                    // ================= IMAGEN LEÍDA =================
                    if (leida != null)
                    {
                        col.Item().Text("Imagen matrícula detectada")
                            .Bold();

                        col.Item().Image(leida).FitWidth();
                        col.Item().PaddingBottom(10);
                    }

                    // ================= IMÁGENES VEHÍCULO =================
                    col.Item().Row(row =>
                    {
                        if (frontal != null)
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Frontal").Bold();
                                c.Item().Image(frontal).FitWidth();
                            });
                        }

                        if (trasera != null)
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Trasera").Bold();
                                c.Item().Image(trasera).FitWidth();
                            });
                        }
                    });

                    col.Item().PaddingTop(15);

                    // ================= FOOTER =================
                    col.Item().Text($"Generado: {DateTime.Now}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });
            });
        })
        .GeneratePdf(filePath);
    }
}