using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AnprViewer.Models;
using AnprViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnprViewer.ViewModels;

/// <summary>
/// Wrapper de <see cref="HistoricoRecord"/> para binding en el DataGrid.
/// Expone propiedades formateadas y carga perezosa de la miniatura primaria.
/// </summary>
public sealed partial class HistoricoRowViewModel : ObservableObject
{
    private readonly IImageCacheService _cache;
    private bool _thumbRequested;

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

    // ── Miniatura primaria con lazy load ───────────────────────
    [ObservableProperty] private BitmapSource? _thumbnail;
    [ObservableProperty] private bool _thumbnailLoading;
    [ObservableProperty] private bool _thumbnailFailed;

    /// <summary>Llamar cuando la fila entra en el viewport (visibilidad real).</summary>
    public async Task EnsureThumbnailAsync(CancellationToken ct = default)
    {
        if (_thumbRequested || !HasAnyImage) return;
        _thumbRequested = true;

        var kind = PickPreferredKind();
        if (kind is null) return;

        ThumbnailLoading = true;
        try
        {
            var bmp = await _cache.GetAsync(NumeroLinea, kind.Value, ct);
            Thumbnail = bmp;
            ThumbnailFailed = bmp is null;
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

    private ImageKind? PickPreferredKind()
    {
        if (Record.HasImgFrontal)  return ImageKind.Frontal;
        if (Record.HasImgLeida)    return ImageKind.Leida;
        if (Record.HasImgTrasera)  return ImageKind.Trasera;
        if (Record.HasImgFacial)   return ImageKind.Facial;
        if (Record.HasImgMatTras)  return ImageKind.MatTras;
        return null;
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
