using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AnprViewer.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AnprViewer.Services;

/// <summary>
/// Genera un PDF profesional con el listado de registros en formato tabla.
/// Usa QuestPDF (Community License). Apaisado A4 para que entren todas las columnas.
/// </summary>
public sealed class ListPdfExportService : IListPdfExportService
{
    // Cache del logo embebido para no leerlo del recurso WPF en cada exportación
    private static byte[]? _logoBytes;
    private static readonly object _logoLock = new();

    public Task ExportListAsync(
        IReadOnlyList<HistoricoRecord> records,
        QueryFilters? filters,
        string outputPath,
        CancellationToken ct = default)
    {
        // QuestPDF es síncrono: pool para no bloquear UI
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Segoe UI").FontColor("#0F141B"));

                    page.Header().Element(c => ComposeHeader(c, records.Count, filters));
                    page.Content().Element(c => ComposeContent(c, records));
                    page.Footer().Element(ComposeFooter);
                });
            })
            .GeneratePdf(outputPath);
        }, ct);
    }

    // ─────────────────────────────────────────────────────────────────
    // HEADER
    // ─────────────────────────────────────────────────────────────────
    private static void ComposeHeader(IContainer c, int total, QueryFilters? f)
    {
        c.PaddingBottom(10).BorderBottom(1).BorderColor("#222B38").Row(row =>
        {
            // Logo
            var logo = TryGetLogo();
            if (logo is { Length: > 0 })
            {
                row.ConstantItem(110).AlignMiddle().Image(logo).FitArea();
            }

            // Título central
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("INFORME DE MATRÍCULAS")
                    .FontSize(9).FontColor("#6B7787").LetterSpacing(0.2f);
                col.Item().Text("Listado de registros HISTORICO")
                    .FontSize(16).Bold().FontColor("#0F141B");
                col.Item().Text($"{total} {(total == 1 ? "registro" : "registros")} · " +
                                $"Generado {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                    .FontSize(9).FontColor("#6B7787");
            });

            // Resumen de filtros aplicados
            row.ConstantItem(220).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("FILTROS APLICADOS")
                    .FontSize(8).FontColor("#6B7787").LetterSpacing(0.15f);
                col.Item().PaddingTop(2).AlignRight()
                    .Text(BuildFilterSummary(f))
                    .FontSize(8).FontColor("#475063");
            });
        });
    }

    private static string BuildFilterSummary(QueryFilters? f)
    {
        if (f is null) return "Sin filtros";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Plate))    parts.Add($"Matrícula: {f.Plate}");
        if (f.From.HasValue)                        parts.Add($"Desde: {f.From:dd/MM/yyyy}");
        if (f.To.HasValue)                          parts.Add($"Hasta: {f.To:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(f.Type))     parts.Add($"Movimiento: {f.Type}");
        if (!string.IsNullOrWhiteSpace(f.Terminal)) parts.Add($"Terminal: {f.Terminal}");
        if (f.MinConf > 0)                          parts.Add($"Fiab. ≥ {f.MinConf:0}%");
        return parts.Count == 0 ? "Sin filtros" : string.Join(" · ", parts);
    }

    // ─────────────────────────────────────────────────────────────────
    // CONTENIDO · TABLA
    // ─────────────────────────────────────────────────────────────────
    private static void ComposeContent(IContainer c, IReadOnlyList<HistoricoRecord> records)
    {
        c.PaddingVertical(12).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(38);     // #
                cd.ConstantColumn(85);     // Matrícula
                cd.ConstantColumn(45);     // Fiab.
                cd.ConstantColumn(115);    // Fecha/Hora
                cd.ConstantColumn(85);     // Movimiento
                cd.RelativeColumn();       // Terminal (variable)
                cd.ConstantColumn(85);     // Tipo terminal
                cd.ConstantColumn(80);     // Tarjeta
                cd.ConstantColumn(45);     // Imgs
            });

            // Cabecera (se repite en cada página)
            t.Header(h =>
            {
                h.Cell().Element(HeaderCell).Text("#");
                h.Cell().Element(HeaderCell).Text("Matrícula");
                h.Cell().Element(HeaderCell).AlignRight().Text("Fiab.");
                h.Cell().Element(HeaderCell).Text("Fecha / Hora");
                h.Cell().Element(HeaderCell).Text("Movimiento");
                h.Cell().Element(HeaderCell).Text("Terminal");
                h.Cell().Element(HeaderCell).Text("Tipo");
                h.Cell().Element(HeaderCell).Text("Tarjeta");
                h.Cell().Element(HeaderCell).AlignCenter().Text("Imgs");
            });

            // Filas con cebra
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                var zebra = i % 2 == 0
                ? Colors.White
                : Color.FromRGB(247, 249, 251);

                t.Cell().Element(c => Cell(c, zebra))
                    .Text(rec.NumeroLinea.ToString("D6"))
                    .FontFamily("Consolas").FontSize(8).FontColor("#6B7787");

                t.Cell().Element(c => Cell(c, zebra))
                    .Text(rec.MatriculaLeida ?? "—")
                    .FontFamily("Consolas").FontSize(10).Bold().FontColor("#0F141B");

                t.Cell().Element(c => Cell(c, zebra)).AlignRight()
                    .Text((rec.FiabilidadMatricula ?? 0).ToString("0.0") + "%")
                    .FontFamily("Consolas").FontSize(9)
                    .FontColor(ConfidenceColor(rec.FiabilidadMatricula));

                t.Cell().Element(c => Cell(c, zebra))
                    .Text(rec.FHGeneracion.ToString("dd/MM/yyyy HH:mm:ss"))
                    .FontFamily("Consolas").FontSize(9);

                t.Cell().Element(c => Cell(c, zebra))
                    .Text(rec.TipoMovimientoDesc ?? "—").FontSize(9);

                t.Cell().Element(c => Cell(c, zebra))
                    .Text(rec.DescTerminal ?? "—").FontSize(9);

                t.Cell().Element(c => Cell(c, zebra))
                    .Text(rec.TipoTerminalDesc ?? "—").FontSize(9);

                t.Cell().Element(c => Cell(c, zebra))
                    .Text(rec.DescTipoTarjeta ?? "—").FontSize(9);

                t.Cell().Element(c => Cell(c, zebra)).AlignCenter()
                    .Text(CountImages(rec).ToString())
                    .FontFamily("Consolas").FontSize(9).FontColor("#5CF5D3");
            }

            // Estilos
            static IContainer HeaderCell(IContainer x)
                => x.Background("#0F141B")
                    .PaddingVertical(6).PaddingHorizontal(6)
                    .DefaultTextStyle(s => s.FontSize(8).Bold().FontColor("#E6EDF5").LetterSpacing(0.1f));

            static IContainer Cell(IContainer x, string bg)
                => x.Background(bg)
                    .PaddingVertical(5).PaddingHorizontal(6)
                    .BorderBottom(0.3f).BorderColor("#E1E6EC");
        });
    }

    private static int CountImages(HistoricoRecord r)
    {
        int n = 0;
        if (r.HasImgLeida)   n++;
        if (r.HasImgFrontal) n++;
        if (r.HasImgTrasera) n++;
        if (r.HasImgFacial)  n++;
        if (r.HasImgMatTras) n++;
        return n;
    }

    private static string ConfidenceColor(float? c)
    {
        var v = c ?? 0;
        if (v >= 90) return "#1F8A3B";
        if (v >= 75) return "#9A7B0F";
        if (v >= 50) return "#A65A00";
        return "#B33A3A";
    }

    // ─────────────────────────────────────────────────────────────────
    // FOOTER
    // ─────────────────────────────────────────────────────────────────
    private static void ComposeFooter(IContainer c)
    {
        c.BorderTop(1).BorderColor("#E1E6EC").PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("Gestor Matrículas Extendida").FontSize(8).FontColor("#6B7787");
                text.Span($" · Generado {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#AAB4C2");
            });
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Página ").FontSize(8).FontColor("#6B7787");
                text.CurrentPageNumber().FontSize(8).Bold().FontColor("#0F141B");
                text.Span(" / ").FontSize(8).FontColor("#6B7787");
                text.TotalPages().FontSize(8).Bold().FontColor("#0F141B");
            });
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // LOGO
    // ─────────────────────────────────────────────────────────────────
    private static byte[]? TryGetLogo()
    {
        if (_logoBytes is not null) return _logoBytes;
        lock (_logoLock)
        {
            if (_logoBytes is not null) return _logoBytes;
            try
            {
                var uri = new Uri("pack://application:,,,/Resources/Images/logo.png", UriKind.Absolute);
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info?.Stream is null) return null;
                using var ms = new MemoryStream();
                info.Stream.CopyTo(ms);
                _logoBytes = ms.ToArray();
                return _logoBytes;
            }
            catch
            {
                return null;
            }
        }
    }
}
