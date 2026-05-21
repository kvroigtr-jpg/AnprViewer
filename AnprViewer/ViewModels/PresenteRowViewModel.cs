using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AnprViewer.Models;
using AnprViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnprViewer.ViewModels;

/// <summary>
/// Wrapper de <see cref="PresenteRecord"/> para binding en el DataGrid de presentes.
/// Carga perezosa de miniaturas (2: entrada + trasera).
/// </summary>
public sealed partial class PresenteRowViewModel : ObservableObject
{
    private readonly IPresenteImageService _images;
    private bool _thumbsRequested;

    public PresenteRecord Record { get; }

    public PresenteRowViewModel(PresenteRecord record, IPresenteImageService images)
    {
        Record  = record;
        _images = images;
    }

    // ── Propiedades de presentación ────────────────────────────
    public string   Plate           => Record.MatriculaEntrada ?? "—";
    public string   PlateFormatted  => string.IsNullOrEmpty(Record.MatriculaEntrada) ? "—" : Record.MatriculaEntrada;
    public DateTime FechaEntrada    => Record.FechaEntrada;
    public string   DayText         => Record.FechaEntrada.ToString("dd/MM/yyyy");
    public string   TimeText        => Record.FechaEntrada.ToString("HH:mm:ss");
    public string   TerminalEntrada => Record.DescTerminalEntrada ?? "—";
    public string   Tarjeta         => Record.DescTipoTarjeta ?? Record.DescClaseTarjeta ?? "—";
    public string   ClaseTarjeta    => Record.DescClaseTarjeta ?? "—";
    public string   CodigoTarjeta   => Record.CodigoTarjeta ?? "—";
    public string   IdTarjeta       => Record.IdTarjeta ?? "—";
    public float    Confidence      => Record.FiabilidadMatricula ?? 0f;
    public string   ConfidenceText  => $"{Confidence:0}%";
    public string   MovementClass   => "entry"; // todos los presentes son entradas activas

    public bool HasAnyImage => Record.HasImgEntrada || Record.HasImgTrasera;

    // ── Thumbnails (lazy) ──────────────────────────────────────
    [ObservableProperty] private BitmapSource? _thumbEntrada;
    [ObservableProperty] private BitmapSource? _thumbTrasera;
    [ObservableProperty] private bool          _thumbnailLoading;
    [ObservableProperty] private bool          _thumbnailFailed;

    /// <summary>Carga las miniaturas la primera vez que la fila entra en viewport.</summary>
    public async Task EnsureThumbnailsAsync(CancellationToken ct = default)
    {
        if (_thumbsRequested || !HasAnyImage) return;
        _thumbsRequested = true;

        ThumbnailLoading = true;
        try
        {
            if (Record.HasImgEntrada)
            {
                var bytes = await _images.FetchAsync(Record, PresentImageKind.Entrada, ct);
                ThumbEntrada = ToBitmap(bytes);
            }
            if (Record.HasImgTrasera)
            {
                var bytes = await _images.FetchAsync(Record, PresentImageKind.Trasera, ct);
                ThumbTrasera = ToBitmap(bytes);
            }

            ThumbnailFailed = ThumbEntrada is null && ThumbTrasera is null && HasAnyImage;
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

    private static BitmapSource? ToBitmap(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
