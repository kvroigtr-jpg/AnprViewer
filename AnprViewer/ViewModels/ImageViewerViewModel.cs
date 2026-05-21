using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AnprViewer.Models;
using AnprViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnprViewer.ViewModels;

/// <summary>Una imagen del registro lista para binding lateral.</summary>
public sealed class ImageItem
{
    public string        Key   { get; init; } = "";
    public string        Label { get; init; } = "";
    public BitmapSource? Image { get; init; }

    public bool IsMatricula { get; init; }
    public bool IsMoto      { get; init; }
}

/// <summary>
/// ViewModel del visor de detalle (single image + thumbs selectores).
/// </summary>
public sealed partial class ImageViewerViewModel : ObservableObject
{
    private readonly IImageCacheService?    _cache;
    private readonly IPresenteImageService? _presenteImages;

    public HistoricoRecord? HistoricoRecord { get; }
    public PresenteRecord?  PresenteRecord  { get; }

    public bool IsHistorico => HistoricoRecord is not null;
    public bool IsPresente  => PresenteRecord  is not null;

    public string PlateText => HistoricoRecord?.MatriculaLeida
                            ?? PresenteRecord?.MatriculaEntrada
                            ?? "—";

    /// <summary>
    /// True SI Y SÓLO SI se ha cargado realmente una imagen de matrícula trasera.
    /// Evalúa contra <see cref="Items"/> en vez de flags de BD (que pueden ser falsos positivos).
    /// </summary>
    public bool HasMotoPlate => Items.Any(i => i.IsMoto && i.Image is not null);

    /// <summary>True sólo si se ha cargado realmente una matrícula coche (Leida/Entrada).</summary>
    public bool HasCarPlate => Items.Any(i => i.IsMatricula && !i.IsMoto && i.Image is not null);

    public ObservableCollection<ImageItem> Items { get; } = new();

    [ObservableProperty] private BitmapSource? _currentImage;
    [ObservableProperty] private string        _currentKey       = "";
    [ObservableProperty] private string        _currentKindLabel = "";
    [ObservableProperty] private bool          _isLoading;
    [ObservableProperty] private string        _statusText = "";

    public ImageViewerViewModel(HistoricoRecord record,
                                IImageCacheService cache,
                                IPresenteImageService presenteImages)
    {
        HistoricoRecord = record;
        _cache          = cache;
        _presenteImages = presenteImages;

        Items.CollectionChanged += OnItemsChanged;
    }

    public ImageViewerViewModel(PresenteRecord record,
                                IImageCacheService cache,
                                IPresenteImageService presenteImages)
    {
        PresenteRecord  = record;
        _cache          = cache;
        _presenteImages = presenteImages;

        Items.CollectionChanged += OnItemsChanged;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasMotoPlate));
        OnPropertyChanged(nameof(HasCarPlate));
    }

    public async Task LoadAllAsync()
    {
        IsLoading  = true;
        StatusText = "Cargando imágenes…";
        try
        {
            if (IsHistorico) await LoadHistoricoAsync(HistoricoRecord!);
            else if (IsPresente) await LoadPresenteAsync(PresenteRecord!);

            OnPropertyChanged(nameof(ThumbLeida));
            OnPropertyChanged(nameof(ThumbFrontal));
            OnPropertyChanged(nameof(ThumbTrasera));
            OnPropertyChanged(nameof(ThumbFacial));
            OnPropertyChanged(nameof(ThumbMatTras));
            OnPropertyChanged(nameof(ThumbEntrada));
            OnPropertyChanged(nameof(ThumbPresTrasera));
            OnPropertyChanged(nameof(HasMotoPlate));
            OnPropertyChanged(nameof(HasCarPlate));

            var first = Items.FirstOrDefault();
            if (first is not null)
            {
                CurrentKey       = first.Key;
                CurrentImage     = first.Image;
                CurrentKindLabel = first.Label;
                StatusText = first.Image is { } b ? $"{b.PixelWidth}×{b.PixelHeight} px" : "";
            }
            else
            {
                StatusText = "Sin imágenes asociadas a este registro.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadHistoricoAsync(HistoricoRecord r)
    {
        if (_cache is null) return;

        var pending = new List<(string Key, string Label, ImageKind Kind, bool IsMat, bool IsMoto)>();
        if (r.HasImgLeida)   pending.Add(("Leida",   "Matrícula leída",   ImageKind.Leida,   true,  false));
        if (r.HasImgFrontal) pending.Add(("Frontal", "Cámara frontal",    ImageKind.Frontal, false, false));
        if (r.HasImgTrasera) pending.Add(("Trasera", "Cámara trasera",    ImageKind.Trasera, false, false));
        if (r.HasImgFacial)  pending.Add(("Facial",  "Cámara facial",     ImageKind.Facial,  false, false));
        if (r.HasImgMatTras) pending.Add(("MatTras", "Matrícula trasera", ImageKind.MatTras, true,  true));

        var tasks = pending.Select(p => _cache.GetAsync(r.NumeroLinea, p.Kind)).ToArray();
        await Task.WhenAll(tasks);

        Items.Clear();
        for (int i = 0; i < pending.Count; i++)
        {
            var bmp = tasks[i].Result;
            // CRÍTICO: descartar imágenes que vinieron null aunque el flag diga true
            if (bmp is null) continue;
            var p = pending[i];
            Items.Add(new ImageItem
            {
                Key         = p.Key,
                Label       = p.Label,
                Image       = bmp,
                IsMatricula = p.IsMat,
                IsMoto      = p.IsMoto,
            });
        }
    }

    private async Task LoadPresenteAsync(PresenteRecord r)
    {
        if (_presenteImages is null) return;

        Items.Clear();
        if (r.HasImgEntrada)
        {
            var bytes = await _presenteImages.FetchAsync(r, PresentImageKind.Entrada);
            var bmp   = ToBitmap(bytes);
            if (bmp is not null)
                Items.Add(new ImageItem
                {
                    Key = "Entrada", Label = "Matrícula entrada",
                    Image = bmp, IsMatricula = true, IsMoto = false,
                });
        }
        if (r.HasImgTrasera)
        {
            var bytes = await _presenteImages.FetchAsync(r, PresentImageKind.Trasera);
            var bmp   = ToBitmap(bytes);
            if (bmp is not null)
                Items.Add(new ImageItem
                {
                    Key = "PresTrasera", Label = "Matrícula trasera (moto)",
                    Image = bmp, IsMatricula = true, IsMoto = true,
                });
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
        catch { return null; }
    }

    [RelayCommand]
    private void SelectKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var item = Items.FirstOrDefault(i => i.Key == key);
        if (item is null) return;
        CurrentKey       = item.Key;
        CurrentImage     = item.Image;
        CurrentKindLabel = item.Label;
        StatusText       = item.Image is { } b ? $"{b.PixelWidth}×{b.PixelHeight} px" : "";
    }

    [RelayCommand]
    private void PrevKind()
    {
        if (Items.Count == 0) return;
        var i = Items.ToList().FindIndex(it => it.Key == CurrentKey);
        if (i <= 0) return;
        SelectKey(Items[i - 1].Key);
    }

    [RelayCommand]
    private void NextKind()
    {
        if (Items.Count == 0) return;
        var i = Items.ToList().FindIndex(it => it.Key == CurrentKey);
        if (i < 0 || i >= Items.Count - 1) return;
        SelectKey(Items[i + 1].Key);
    }

    public BitmapSource? ThumbLeida        => Items.FirstOrDefault(i => i.Key == "Leida")?.Image;
    public BitmapSource? ThumbFrontal      => Items.FirstOrDefault(i => i.Key == "Frontal")?.Image;
    public BitmapSource? ThumbTrasera      => Items.FirstOrDefault(i => i.Key == "Trasera")?.Image;
    public BitmapSource? ThumbFacial       => Items.FirstOrDefault(i => i.Key == "Facial")?.Image;
    public BitmapSource? ThumbMatTras      => Items.FirstOrDefault(i => i.Key == "MatTras")?.Image;
    public BitmapSource? ThumbEntrada      => Items.FirstOrDefault(i => i.Key == "Entrada")?.Image;
    public BitmapSource? ThumbPresTrasera  => Items.FirstOrDefault(i => i.Key == "PresTrasera")?.Image;
}
