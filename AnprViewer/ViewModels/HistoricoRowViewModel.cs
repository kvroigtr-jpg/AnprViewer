using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AnprViewer.Models;
using AnprViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnprViewer.ViewModels;

/// <summary>
/// Wrapper de <see cref="HistoricoRecord"/> para binding en el DataGrid.
/// Expone propiedades formateadas y carga perezosa de las miniaturas (4 en línea).
/// </summary>
public sealed partial class HistoricoRowViewModel : ObservableObject
{
    private readonly IImageCacheService _cache;
    private bool _thumbsRequested;

    public HistoricoRecord Record { get; }

    public HistoricoRowViewModel(HistoricoRecord record, IImageCacheService cache)
    {
        Record  = record;
        _cache  = cache;
    }

    // ── Propiedades de presentación ────────────────────────────
    public int      NumeroLinea         => Record.NumeroLinea;
    public string   Plate               => Record.MatriculaLeida ?? "—";
    public string   PlateFormatted      => string.IsNullOrEmpty(Record.MatriculaLeida) ? "—" : Record.MatriculaLeida;
    public DateTime FHGeneracion        => Record.FHGeneracion;
    public string   DayText             => Record.FHGeneracion.ToString("dd/MM/yyyy");
    public string   TimeText            => Record.FHGeneracion.ToString("HH:mm:ss");
    public string   Movimiento          => Record.TipoMovimientoDesc ?? "—";
    public string   Terminal            => Record.DescTerminal ?? "—";
    public string   TipoTerminal        => Record.TipoTerminalDesc ?? "—";
    public string   Tarjeta             => Record.DescTipoTarjeta ?? "—";
    public string   CodigoTarjeta       => Record.CodigoTarjeta ?? "—";
    public float    Confidence          => Record.FiabilidadMatricula ?? 0f;
    public string   ConfidenceText      => $"{Confidence:0}%";
    public string   ConfidenceClass     => Confidence < 60 ? "low" : (Confidence < 85 ? "mid" : "high");
    public string   MovementClass       => GetMovementClass(Movimiento);

    public bool HasAnyImage =>
        Record.HasImgLeida || Record.HasImgFrontal || Record.HasImgTrasera ||
        Record.HasImgFacial || Record.HasImgMatTras;

    // ── 4 Thumbnails en línea con lazy load ────────────────────
    [ObservableProperty] private BitmapSource? _thumbLeida;
    [ObservableProperty] private BitmapSource? _thumbFrontal;
    [ObservableProperty] private BitmapSource? _thumbTrasera;
    [ObservableProperty] private BitmapSource? _thumbFacial;
    [ObservableProperty] private bool _thumbnailLoading;
    [ObservableProperty] private bool _thumbnailFailed;

    /// <summary>Llamar cuando la fila entra en el viewport (visibilidad real).</summary>
    public async Task EnsureThumbnailsAsync(CancellationToken ct = default)
    {
        if (_thumbsRequested || !HasAnyImage) return;
        _thumbsRequested = true;

        ThumbnailLoading = true;
        try
        {
            var pending = new List<(ImageKind Kind, Task<BitmapSource?> Task)>();
            if (Record.HasImgLeida)   pending.Add((ImageKind.Leida,   _cache.GetAsync(NumeroLinea, ImageKind.Leida,   ct)));
            if (Record.HasImgFrontal) pending.Add((ImageKind.Frontal, _cache.GetAsync(NumeroLinea, ImageKind.Frontal, ct)));
            if (Record.HasImgTrasera) pending.Add((ImageKind.Trasera, _cache.GetAsync(NumeroLinea, ImageKind.Trasera, ct)));
            if (Record.HasImgFacial)  pending.Add((ImageKind.Facial,  _cache.GetAsync(NumeroLinea, ImageKind.Facial,  ct)));

            await Task.WhenAll(pending.Select(p => p.Task));

            foreach (var (kind, task) in pending)
            {
                var bmp = task.Result;
                switch (kind)
                {
                    case ImageKind.Leida:   ThumbLeida   = bmp; break;
                    case ImageKind.Frontal: ThumbFrontal = bmp; break;
                    case ImageKind.Trasera: ThumbTrasera = bmp; break;
                    case ImageKind.Facial:  ThumbFacial  = bmp; break;
                }
            }

            ThumbnailFailed = pending.Count > 0 && pending.All(p => p.Task.Result is null);
        }
        catch
        {
            ThumbnailFailed = true;
        }
        finally
        {
            ThumbnailLoading = false;
        }
    }

    private static string GetMovementClass(string mov)
    {
        if (string.IsNullOrEmpty(mov)) return "default";
        var m = mov.ToLowerInvariant();
        if (m.Contains("entr")) return "entry";
        if (m.Contains("sal"))  return "exit";
        if (m.Contains("facial")) return "warn";
        if (m.Contains("ciclo") || m.Contains("moto")) return "warn";
        return "default";
    }
}
