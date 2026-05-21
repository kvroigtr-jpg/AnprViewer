using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using AnprViewer.Services;

namespace AnprViewer.Converters;

/// <summary>
/// Devuelve un BitmapImage congelado con el logo correspondiente al tema actual.
///   - Dark  → logo_2.png (letras blancas)
///   - Light → logo_1.png (letras negras)
///
/// El binding se hace contra <see cref="ThemeService.Version"/>, que se
/// incrementa cada vez que el tema cambia, para que la imagen se refresque.
/// </summary>
public sealed class ThemeLogoConverter : IValueConverter
{
    private static readonly BitmapImage _dark  = Load("pack://application:,,,/Resources/Images/logo_2.png");
    private static readonly BitmapImage _light = Load("pack://application:,,,/Resources/Images/logo_1.png");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ThemeService.Current == ThemeService.Mode.Light ? _light : _dark;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static BitmapImage Load(string uri)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption  = BitmapCacheOption.OnLoad;
        bmp.UriSource    = new Uri(uri, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
