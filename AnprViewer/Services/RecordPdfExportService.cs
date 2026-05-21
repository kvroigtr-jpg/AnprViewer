using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AnprViewer.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AnprViewer.Services;

/// <summary>
/// PDF profesional de un registro individual. Cabecera con placa formato coche,
/// secciones de datos, y a partir de la página 2 una imagen por página a tamaño máximo.
/// Si existe matrícula trasera (moto) se renderiza también una placa formato moto.
/// </summary>
public sealed class RecordPdfExportService : IRecordPdfExportService
{
    private readonly IDatabaseService      _db;
    private readonly IPresenteImageService _presenteImages;

    private static byte[]? _logoBytes;
    private static readonly object _logoLock = new();

    public RecordPdfExportService(IDatabaseService db, IPresenteImageService presenteImages)
    {
        _db             = db;
        _presenteImages = presenteImages;
    }

    // ══════════════════════════════ HISTORICO ══════════════════════════════
    public Task ExportRecordAsync(HistoricoRecord record, string filePath)
        => ExportHistoricoAsync(record, filePath);

    public async Task ExportHistoricoAsync(HistoricoRecord record, string filePath)
    {
        var images = await LoadHistoricoImagesAsync(record);

        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(t => t.FontSize(10)
                        .FontFamily("Segoe UI").FontColor("#0F141B"));

                    page.Header().Element(c => ComposeHeaderHistorico(c, record));
                    page.Content().Element(c => ComposeContentHistorico(c, record, images));
                    page.Footer().Element(c => ComposeFooterHistorico(c, record));
                });
            }).GeneratePdf(filePath);
        });
    }

    // ══════════════════════════════ PRESENTES ══════════════════════════════
    public async Task ExportPresenteAsync(PresenteRecord record, string filePath)
    {
        var images = await LoadPresenteImagesAsync(record);

        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(t => t.FontSize(10)
                        .FontFamily("Segoe UI").FontColor("#0F141B"));

                    page.Header().Element(c => ComposeHeaderPresente(c, record));
                    page.Content().Element(c => ComposeContentPresente(c, record, images));
                    page.Footer().Element(c => ComposeFooterPresente(c, record));
                });
            }).GeneratePdf(filePath);
        });
    }

    // ────────────────────────── carga imágenes HIST ───────────────────────
    private async Task<List<(string Label, byte[] Bytes, bool IsMatricula, bool IsMoto)>> LoadHistoricoImagesAsync(HistoricoRecord r)
    {
        var src = new (ImageKind kind, bool has, string label, bool isMat, bool isMoto)[]
        {
            (ImageKind.Leida,   r.HasImgLeida,   "Imagen de matrícula leída",  true,  false),
            (ImageKind.Frontal, r.HasImgFrontal, "Cámara frontal",             false, false),
            (ImageKind.Trasera, r.HasImgTrasera, "Cámara trasera",             false, false),
            (ImageKind.Facial,  r.HasImgFacial,  "Cámara facial",              false, false),
            (ImageKind.MatTras, r.HasImgMatTras, "Matrícula trasera (moto)",   true,  true),
        };
        var list = new List<(string, byte[], bool, bool)>();
        foreach (var (kind, has, label, isMat, isMoto) in src)
        {
            if (!has) continue;
            var bytes = await _db.FetchImageAsync(r.NumeroLinea, kind);
            if (bytes is { Length: > 0 }) list.Add((label, bytes, isMat, isMoto));
        }
        return list;
    }

    // ────────────────────────── carga imágenes PRESENTES ───────────────────
    private async Task<List<(string Label, byte[] Bytes, bool IsMatricula, bool IsMoto)>> LoadPresenteImagesAsync(PresenteRecord r)
    {
        var list = new List<(string, byte[], bool, bool)>();

        if (r.HasImgEntrada)
        {
            var bytes = await _presenteImages.FetchAsync(r, PresentImageKind.Entrada);
            if (bytes is { Length: > 0 })
                list.Add(("Imagen de matrícula de entrada", bytes, true, false));
        }
        if (r.HasImgTrasera)
        {
            var bytes = await _presenteImages.FetchAsync(r, PresentImageKind.Trasera);
            if (bytes is { Length: > 0 })
                list.Add(("Matrícula trasera (moto)", bytes, true, true));
        }
        return list;
    }

    // ════════════════════════════ HEADERS ════════════════════════════
    private static void ComposeHeaderHistorico(IContainer c, HistoricoRecord r)
    {
        c.PaddingBottom(10).BorderBottom(1).BorderColor("#222B38").Row(row =>
        {
            var logo = TryGetLogo();
            if (logo is { Length: > 0 })
                row.ConstantItem(110).AlignMiddle().Image(logo).FitArea();

            row.RelativeItem().Column(col =>
            {
                col.Item().Text("INFORME DE REGISTRO · HISTÓRICO")
                    .FontSize(9).FontColor("#6B7787").LetterSpacing(0.2f);
                col.Item().Text($"Matrícula  {r.MatriculaLeida ?? "—"}")
                    .FontSize(20).Bold().FontFamily("Consolas").FontColor("#0F141B");
                col.Item().Text($"Registro #{r.NumeroLinea:D6} · {r.FHGeneracion:dd/MM/yyyy HH:mm:ss}")
                    .FontSize(9).FontColor("#6B7787");
            });

            row.ConstantItem(160).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("FIABILIDAD")
                    .FontSize(8).FontColor("#6B7787").LetterSpacing(0.15f);
                col.Item().PaddingTop(2).AlignRight()
                    .Text($"{(r.FiabilidadMatricula ?? 0):0.0}%")
                    .FontSize(16).Bold()
                    .FontColor(ConfidenceColor(r.FiabilidadMatricula));
            });
        });
    }

    private static void ComposeHeaderPresente(IContainer c, PresenteRecord r)
    {
        c.PaddingBottom(10).BorderBottom(1).BorderColor("#222B38").Row(row =>
        {
            var logo = TryGetLogo();
            if (logo is { Length: > 0 })
                row.ConstantItem(110).AlignMiddle().Image(logo).FitArea();

            row.RelativeItem().Column(col =>
            {
                col.Item().Text("INFORME DE VEHÍCULO · PRESENTE EN EL PARKING")
                    .FontSize(9).FontColor("#6B7787").LetterSpacing(0.2f);
                col.Item().Text($"Matrícula  {r.MatriculaEntrada ?? "—"}")
                    .FontSize(20).Bold().FontFamily("Consolas").FontColor("#0F141B");
                col.Item().Text($"Entrada: {r.FechaEntrada:dd/MM/yyyy HH:mm:ss}")
                    .FontSize(9).FontColor("#6B7787");
            });

            row.ConstantItem(160).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("FIABILIDAD")
                    .FontSize(8).FontColor("#6B7787").LetterSpacing(0.15f);
                col.Item().PaddingTop(2).AlignRight()
                    .Text($"{(r.FiabilidadMatricula ?? 0):0.0}%")
                    .FontSize(16).Bold()
                    .FontColor(ConfidenceColor(r.FiabilidadMatricula));
            });
        });
    }

    // ════════════════════════════ CONTENT HISTORICO ════════════════════════════
    private static void ComposeContentHistorico(IContainer c, HistoricoRecord r,
        List<(string Label, byte[] Bytes, bool IsMatricula, bool IsMoto)> images)
    {
        c.PaddingVertical(12).Column(col =>
        {
            col.Item().Element(x => Section(x, "DATOS GENERALES"));
            col.Item().Element(x => DataGrid(x, new[]
            {
                ("Nº de línea",    r.NumeroLinea.ToString("D6")),
                ("Aparcamiento",   r.CodigoAparcamiento.ToString()),
                ("Nº terminal",    r.NumeroTerminal?.ToString() ?? "—"),
                ("FH generación",  r.FHGeneracion.ToString("dd/MM/yyyy HH:mm:ss")),
                ("FH inclusión",   r.FHInclusion?.ToString("dd/MM/yyyy HH:mm:ss") ?? "—"),
            }));

            col.Item().PaddingTop(14).Element(x => Section(x, "IDENTIFICACIÓN"));
            col.Item().Element(x => DataGrid(x, new[]
            {
                ("Matrícula leída",     r.MatriculaLeida ?? "—"),
                ("Matrícula operación", r.MatriculaOperacion ?? "—"),
                ("Fiabilidad",          $"{(r.FiabilidadMatricula ?? 0):0.00}%"),
                ("Nº de fallos",        r.NumeroDeFallos?.ToString() ?? "—"),
                ("Tipo de incidencia",  r.TipoIncidencia?.ToString() ?? "—"),
                ("Matrícula remolque",  r.MatriculaRemolque ?? "—"),
            }));

            col.Item().PaddingTop(14).Element(x => Section(x, "MOVIMIENTO"));
            col.Item().Element(x => DataGrid(x, new[]
            {
                ("Tipo movimiento", r.TipoMovimientoDesc ?? "—"),
                ("Terminal",        r.DescTerminal ?? "—"),
                ("Tipo terminal",   r.TipoTerminalDesc ?? "—"),
            }));

            col.Item().PaddingTop(14).Element(x => Section(x, "TARJETA"));
            col.Item().Element(x => DataGrid(x, new[]
            {
                ("Clase",   r.DescClaseTarjeta ?? "—"),
                ("Tipo",    r.DescTipoTarjeta ?? "—"),
                ("Código",  r.CodigoTarjeta ?? "—"),
            }));

            // Imágenes
            foreach (var (label, bytes, isMat, isMoto) in images)
            {
                col.Item().PageBreak();
                col.Item().Element(x => Section(x, label.ToUpperInvariant()));

                if (isMat)
                {
                    // Las imágenes de matrícula van con marco "placa"
                    col.Item().PaddingTop(8)
                        .Element(x => PlateFrame(x, bytes, r.MatriculaLeida ?? "", isMoto));
                }
                else
                {
                    col.Item().PaddingTop(8)
                        .Border(1).BorderColor("#E1E6EC").Padding(4)
                        .Image(bytes).FitArea();
                }
            }
        });
    }

    // ════════════════════════════ CONTENT PRESENTES ════════════════════════════
    private static void ComposeContentPresente(IContainer c, PresenteRecord r,
        List<(string Label, byte[] Bytes, bool IsMatricula, bool IsMoto)> images)
    {
        c.PaddingVertical(12).Column(col =>
        {
            col.Item().Element(x => Section(x, "DATOS GENERALES"));
            col.Item().Element(x => DataGrid(x, new[]
            {
                ("Aparcamiento",    r.CodigoAparcamiento?.ToString() ?? "—"),
                ("Fecha entrada",   r.FechaEntrada.ToString("dd/MM/yyyy HH:mm:ss")),
                ("Terminal entrada",r.DescTerminalEntrada ?? "—"),
                ("Nº terminal",     r.TerminalEntrada?.ToString() ?? "—"),
            }));

            col.Item().PaddingTop(14).Element(x => Section(x, "IDENTIFICACIÓN"));
            col.Item().Element(x => DataGrid(x, new[]
            {
                ("Matrícula entrada",  r.MatriculaEntrada ?? "—"),
                ("Fiabilidad",         $"{(r.FiabilidadMatricula ?? 0):0.00}%"),
            }));

            col.Item().PaddingTop(14).Element(x => Section(x, "TARJETA"));
            col.Item().Element(x => DataGrid(x, new[]
            {
                ("Id Tarjeta", r.IdTarjeta ?? "—"),
                ("Clase",      r.DescClaseTarjeta ?? "—"),
                ("Tipo",       r.DescTipoTarjeta ?? "—"),
                ("Código",     r.CodigoTarjeta ?? "—"),
            }));

            // Imágenes (sólo si existen, sin huecos)
            foreach (var (label, bytes, _, isMoto) in images)
            {
                col.Item().PageBreak();
                col.Item().Element(x => Section(x, label.ToUpperInvariant()));
                col.Item().PaddingTop(8)
                    .Element(x => PlateFrame(x, bytes, r.MatriculaEntrada ?? "", isMoto));
            }
        });
    }

    // ════════════════════════════ Helpers comunes ════════════════════════════
    /// <summary>
    /// Marco visual tipo placa con la imagen real dentro. Diseño coche (horizontal)
    /// o moto (cuadrado/vertical) según parámetro.
    /// </summary>
    private static void PlateFrame(IContainer c, byte[] bytes, string plate, bool isMoto)
    {
        c.Column(col =>
        {
            col.Item().Border(2).BorderColor("#111111").Background("#F5F5F0")
                .Padding(4)
                .Column(inner =>
                {
                    // Banda azul "E" arriba (siempre)
                    inner.Item().Background("#003399").Padding(4).AlignCenter()
                        .Text(t =>
                        {
                            t.Span("★ ").FontColor("#FFCC00").FontSize(10);
                            t.Span("E").FontColor(Colors.White).FontSize(11).Bold();
                        });

                    // Imagen real (la matrícula recortada por el ANPR)
                    inner.Item().PaddingVertical(6)
                        .MaxHeight(isMoto ? 320 : 180)
                        .AlignCenter()
                        .Image(bytes).FitArea();

                    // Texto OCR
                    if (!string.IsNullOrEmpty(plate))
                    {
                        inner.Item().AlignCenter().Text(plate)
                            .FontFamily("Consolas").Bold()
                            .FontSize(isMoto ? 32 : 24).FontColor("#111111");
                    }
                });
        });
    }

    private static void Section(IContainer c, string title)
    {
        c.Column(col =>
        {
            col.Item().Text(title).FontSize(9).Bold()
                .FontColor("#475063").LetterSpacing(0.5f);
            col.Item().PaddingTop(2).BorderBottom(1).BorderColor("#222B38");
        });
    }

    private static void DataGrid(IContainer c, IReadOnlyList<(string Label, string Value)> rows)
    {
        c.PaddingTop(6).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(140);
                cd.RelativeColumn();
                cd.ConstantColumn(140);
                cd.RelativeColumn();
            });

            for (int i = 0; i < rows.Count; i += 2)
            {
                t.Cell().PaddingVertical(3).Text(rows[i].Label)
                    .FontSize(8).FontColor("#6B7787");
                t.Cell().PaddingVertical(3).Text(rows[i].Value)
                    .FontSize(10).FontFamily("Consolas").FontColor("#0F141B");

                if (i + 1 < rows.Count)
                {
                    t.Cell().PaddingVertical(3).Text(rows[i + 1].Label)
                        .FontSize(8).FontColor("#6B7787");
                    t.Cell().PaddingVertical(3).Text(rows[i + 1].Value)
                        .FontSize(10).FontFamily("Consolas").FontColor("#0F141B");
                }
                else
                {
                    t.Cell().Text("");
                    t.Cell().Text("");
                }
            }
        });
    }

    private static void ComposeFooterHistorico(IContainer c, HistoricoRecord r)
    {
        c.BorderTop(1).BorderColor("#E1E6EC").PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("Gestor Matrículas Extendido")
                    .FontSize(8).FontColor("#6B7787");
                text.Span($" · Reg. #{r.NumeroLinea:D6} · {r.MatriculaLeida}")
                    .FontSize(8).FontColor("#AAB4C2");
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

    private static void ComposeFooterPresente(IContainer c, PresenteRecord r)
    {
        c.BorderTop(1).BorderColor("#E1E6EC").PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("Gestor Matrículas Extendido · Presente")
                    .FontSize(8).FontColor("#6B7787");
                text.Span($" · {r.MatriculaEntrada} · {r.FechaEntrada:dd/MM/yyyy HH:mm}")
                    .FontSize(8).FontColor("#AAB4C2");
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

    private static string ConfidenceColor(float? c)
    {
        var v = c ?? 0;
        if (v >= 90) return "#1F8A3B";
        if (v >= 75) return "#9A7B0F";
        if (v >= 50) return "#A65A00";
        return "#B33A3A";
    }

    private static byte[]? TryGetLogo()
    {
        if (_logoBytes is not null) return _logoBytes;
        lock (_logoLock)
        {
            if (_logoBytes is not null) return _logoBytes;
            try
            {
                var uri = new Uri("pack://application:,,,/Resources/Images/logo_1.png", UriKind.Absolute);
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info?.Stream is null) return null;
                using var ms = new MemoryStream();
                info.Stream.CopyTo(ms);
                _logoBytes = ms.ToArray();
                return _logoBytes;
            }
            catch { return null; }
        }
    }
}
