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
/// PDF profesional del listado actual (tabla apaisada).
/// Soporta listado de HISTORICO y de PRESENTES.
/// </summary>
public sealed class ListPdfExportService : IListPdfExportService
{
    private static byte[]? _logoBytes;
    private static readonly object _logoLock = new();

    // ─────────────────────────── HISTORICO ───────────────────────────
    public async Task ExportListAsync(
        IReadOnlyList<HistoricoRecord> records,
        QueryFilters? filters,
        string filePath)
    {
        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.2f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(t => t.FontSize(9)
                        .FontFamily("Segoe UI").FontColor("#0F141B"));

                    page.Header().Element(c => ComposeListHeader(c,
                        "INFORME · HISTÓRICO DE LECTURAS",
                        records.Count,
                        filters));
                    page.Content().Element(c => ComposeHistoricoTable(c, records));
                    page.Footer().Element(c => ComposeFooter(c));
                });
            }).GeneratePdf(filePath);
        });
    }

    // ─────────────────────────── PRESENTES ───────────────────────────
    public async Task ExportPresentesListAsync(
        IReadOnlyList<PresenteRecord> records,
        QueryFilters? filters,
        string filePath)
    {
        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.2f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(t => t.FontSize(9)
                        .FontFamily("Segoe UI").FontColor("#0F141B"));

                    page.Header().Element(c => ComposeListHeader(c,
                        "INFORME · VEHÍCULOS PRESENTES",
                        records.Count,
                        filters));
                    page.Content().Element(c => ComposePresentesTable(c, records));
                    page.Footer().Element(c => ComposeFooter(c));
                });
            }).GeneratePdf(filePath);
        });
    }

    // ────────────────────────── header común ─────────────────────────
    private static void ComposeListHeader(IContainer c, string title, int total, QueryFilters? f)
    {
        c.PaddingBottom(8).BorderBottom(1).BorderColor("#222B38").Row(row =>
        {
            var logo = TryGetLogo();
            if (logo is { Length: > 0 })
                row.ConstantItem(110).AlignMiddle().Image(logo).FitArea();

            row.RelativeItem().Column(col =>
            {
                col.Item().Text(title)
                    .FontSize(9).FontColor("#6B7787").LetterSpacing(0.2f);
                col.Item().Text($"{total:N0} registros · generado {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                    .FontSize(14).Bold().FontColor("#0F141B");

                if (f is not null)
                {
                    var filterParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(f.Plate))    filterParts.Add($"Matrícula='{f.Plate}'");
                    if (f.From.HasValue)                         filterParts.Add($"Desde={f.From:dd/MM/yyyy HH:mm:ss}");
                    if (f.To.HasValue)                           filterParts.Add($"Hasta={f.To:dd/MM/yyyy HH:mm:ss}");
                    if (f.TipoTerminal.HasValue)                 filterParts.Add($"Movimiento={MovimientoLabel(f.TipoTerminal.Value)}");
                    if (!string.IsNullOrWhiteSpace(f.Terminal)) filterParts.Add($"Terminal='{f.Terminal}'");
                    if (f.MinConf > 0)                          filterParts.Add($"Fiab≥{f.MinConf:0}%");

                    if (filterParts.Count > 0)
                        col.Item().PaddingTop(2).Text("Filtros: " + string.Join(" · ", filterParts))
                            .FontSize(8).FontColor("#6B7787");
                }
            });
        });
    }

    private static string MovimientoLabel(int tipo) => tipo switch
    {
        0 => "Entrada",
        1 => "Salida",
        2 => "Paso",
        _ => tipo.ToString(),
    };

    // ──────────────────────── tabla HISTORICO ────────────────────────
    private static void ComposeHistoricoTable(IContainer c, IReadOnlyList<HistoricoRecord> rows)
    {
        c.PaddingVertical(8).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(50);   // #
                cd.ConstantColumn(80);   // Matrícula
                cd.ConstantColumn(45);   // Fiab
                cd.ConstantColumn(105);  // Fecha
                cd.ConstantColumn(80);   // Movimiento
                cd.RelativeColumn();     // Terminal
                cd.RelativeColumn();     // Tarjeta
                cd.ConstantColumn(60);   // Imgs
            });

            t.Header(h =>
            {
                Th(h.Cell(), "#");
                Th(h.Cell(), "Matrícula");
                Th(h.Cell(), "Fiab.");
                Th(h.Cell(), "Fecha/Hora");
                Th(h.Cell(), "Movimiento");
                Th(h.Cell(), "Terminal");
                Th(h.Cell(), "Tarjeta");
                Th(h.Cell(), "Imgs");
            });

            int idx = 0;
            foreach (var r in rows)
            {
                var bg = (idx++ % 2 == 0) ? "#FFFFFF" : "#F8F9FB";

                Td(t.Cell(), bg, r.NumeroLinea.ToString("D6"));
                Td(t.Cell(), bg, r.MatriculaLeida ?? "—", bold: true);
                Td(t.Cell(), bg, $"{(r.FiabilidadMatricula ?? 0):0}%");
                Td(t.Cell(), bg, r.FHGeneracion.ToString("dd/MM/yyyy HH:mm:ss"));
                Td(t.Cell(), bg, r.TipoMovimientoDesc ?? "—");
                Td(t.Cell(), bg, r.DescTerminal ?? "—");
                Td(t.Cell(), bg, r.DescTipoTarjeta ?? r.DescClaseTarjeta ?? "—");
                Td(t.Cell(), bg, ImagesFlag(r));
            }
        });
    }

    // ──────────────────────── tabla PRESENTES ────────────────────────
    private static void ComposePresentesTable(IContainer c, IReadOnlyList<PresenteRecord> rows)
    {
        c.PaddingVertical(8).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(80);   // Matrícula
                cd.ConstantColumn(45);   // Fiab
                cd.ConstantColumn(105);  // Fecha entrada
                cd.RelativeColumn();     // Terminal
                cd.RelativeColumn();     // Tarjeta
                cd.ConstantColumn(80);   // Id Tarjeta
                cd.ConstantColumn(50);   // Imgs
            });

            t.Header(h =>
            {
                Th(h.Cell(), "Matrícula");
                Th(h.Cell(), "Fiab.");
                Th(h.Cell(), "Entrada");
                Th(h.Cell(), "Terminal");
                Th(h.Cell(), "Tarjeta");
                Th(h.Cell(), "Id Tarjeta");
                Th(h.Cell(), "Imgs");
            });

            int idx = 0;
            foreach (var r in rows)
            {
                var bg = (idx++ % 2 == 0) ? "#FFFFFF" : "#F8F9FB";

                Td(t.Cell(), bg, r.MatriculaEntrada ?? "—", bold: true);
                Td(t.Cell(), bg, $"{(r.FiabilidadMatricula ?? 0):0}%");
                Td(t.Cell(), bg, r.FechaEntrada.ToString("dd/MM/yyyy HH:mm:ss"));
                Td(t.Cell(), bg, r.DescTerminalEntrada ?? "—");
                Td(t.Cell(), bg, r.DescTipoTarjeta ?? r.DescClaseTarjeta ?? "—");
                Td(t.Cell(), bg, r.IdTarjeta ?? "—");
                Td(t.Cell(), bg, (r.HasImgEntrada ? "E" : "") + (r.HasImgTrasera ? "T" : ""));
            }
        });
    }

    private static string ImagesFlag(HistoricoRecord r)
    {
        var s = "";
        if (r.HasImgLeida)   s += "M";
        if (r.HasImgFrontal) s += "F";
        if (r.HasImgTrasera) s += "T";
        if (r.HasImgFacial)  s += "C";
        if (r.HasImgMatTras) s += "X";
        return s.Length == 0 ? "—" : s;
    }

    // ────────────────────────── helpers tabla ────────────────────────
    private static void Th(IContainer cell, string text)
    {
        cell.Background("#0F141B").Padding(4)
            .Text(text).FontSize(8).Bold().FontColor("#FFFFFF").LetterSpacing(0.3f);
    }

    private static void Td(IContainer cell, string bg, string text, bool bold = false)
    {
        var styled = cell.Background(bg).Padding(3);
        if (bold)
            styled.Text(text).FontSize(9).Bold().FontFamily("Consolas").FontColor("#0F141B");
        else
            styled.Text(text).FontSize(8).FontFamily("Consolas").FontColor("#0F141B");
    }

    private static void ComposeFooter(IContainer c)
    {
        c.BorderTop(1).BorderColor("#E1E6EC").PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text("Gestor Matrículas Extendido · CAME Parkare")
                .FontSize(8).FontColor("#6B7787");
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Página ").FontSize(8).FontColor("#6B7787");
                text.CurrentPageNumber().FontSize(8).Bold().FontColor("#0F141B");
                text.Span(" / ").FontSize(8).FontColor("#6B7787");
                text.TotalPages().FontSize(8).Bold().FontColor("#0F141B");
            });
        });
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
