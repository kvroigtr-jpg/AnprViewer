using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AnprViewer.Models;
using AnprViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnprViewer.ViewModels;

/// <summary>
/// ViewModel del visor de imagen ampliado.
/// </summary>
public sealed partial class ImageViewerViewModel : ObservableObject
{
    private readonly IImageCacheService _cache;
    private readonly List<ImageKind>    _availableKinds;

    public HistoricoRecord Record { get; }

    [ObservableProperty] private BitmapSource? _currentImage;
    [ObservableProperty] private ImageKind _currentKind;
    [ObservableProperty] private string _currentKindLabel = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public IReadOnlyList<ImageKind> AvailableKinds => _availableKinds;

    public ImageViewerViewModel(HistoricoRecord record, IImageCacheService cache, ImageKind? initialKind = null)
    {
        Record = record;
        _cache = cache;

        _availableKinds = new List<ImageKind>();
        if (record.HasImgLeida)   _availableKinds.Add(ImageKind.Leida);
        if (record.HasImgFrontal) _availableKinds.Add(ImageKind.Frontal);
        if (record.HasImgTrasera) _availableKinds.Add(ImageKind.Trasera);
        if (record.HasImgFacial)  _availableKinds.Add(ImageKind.Facial);
        if (record.HasImgMatTras) _availableKinds.Add(ImageKind.MatTras);

        if (_availableKinds.Count > 0)
        {
            CurrentKind = (initialKind.HasValue && _availableKinds.Contains(initialKind.Value))
                ? initialKind.Value
                : _availableKinds[0];
        }
    }

    public async Task LoadCurrentAsync()
    {
        if (_availableKinds.Count == 0) return;
        IsLoading = true;
        StatusText = "Cargando imagen…";
        CurrentKindLabel = LabelOf(CurrentKind);
        try
        {
            var bmp = await _cache.GetAsync(Record.NumeroLinea, CurrentKind);
            CurrentImage = bmp;
            StatusText = bmp is null ? "Imagen no disponible." : $"{bmp.PixelWidth}×{bmp.PixelHeight} px";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SelectKindAsync(ImageKind kind)
    {
        if (!_availableKinds.Contains(kind)) return;
        CurrentKind = kind;
        await LoadCurrentAsync();
    }

    [RelayCommand]
    private async Task PrevKindAsync()
    {
        var i = _availableKinds.IndexOf(CurrentKind);
        if (i > 0) { CurrentKind = _availableKinds[i - 1]; await LoadCurrentAsync(); }
    }

    [RelayCommand]
    private async Task NextKindAsync()
    {
        var i = _availableKinds.IndexOf(CurrentKind);
        if (i >= 0 && i < _availableKinds.Count - 1) { CurrentKind = _availableKinds[i + 1]; await LoadCurrentAsync(); }
    }

    public static string LabelOf(ImageKind k) => k switch
    {
        ImageKind.Leida   => "Mat. leída",
        ImageKind.Frontal => "Frontal",
        ImageKind.Trasera => "Trasera",
        ImageKind.Facial  => "Facial",
        ImageKind.MatTras => "Mat. trasera",
        _ => "—"
    };
}
