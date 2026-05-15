using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AnprViewer.Models;

namespace AnprViewer.Converters;

/// <summary>bool → Visibility (true = Visible).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>!bool → Visibility (true = Collapsed, false = Visible).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Confianza 0..100 a anchura proporcional (parámetro = anchura máxima).</summary>
public sealed class ConfidenceToWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double v = value switch
        {
            float f  => f,
            double d => d,
            int i    => i,
            _ => 0
        };
        var max = parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 60.0;
        return Math.Clamp(v, 0, 100) / 100.0 * max;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Confianza → Brush (rojo / amarillo / verde).</summary>
public sealed class ConfidenceToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double v = value switch { float f => f, double d => d, int i => i, _ => 0 };
        if (v < 60) return new SolidColorBrush((Color)Application.Current.Resources["ErrColor"]);
        if (v < 85) return new SolidColorBrush((Color)Application.Current.Resources["WarnColor"]);
        return new SolidColorBrush((Color)Application.Current.Resources["AccentColor"]);
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Cadena de clase de badge → SolidColorBrush.</summary>
public sealed class BadgeClassToBrushConverter : IValueConverter
{
    public bool Background { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "entry" => Background ? "OkBg"   : "Ok",
            "exit"  => Background ? "InfoBg" : "Info",
            "warn"  => Background ? "WarnBg" : "Warn",
            "err"   => Background ? "ErrBg"  : "Err",
            _       => Background ? "Bg2"    : "Text1",
        };
        return Application.Current.Resources[key]!;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>ImageKind → etiqueta legible.</summary>
public sealed class ImageKindToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ImageKind k ? ViewModels.ImageViewerViewModel.LabelOf(k) : "—";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Compara dos valores con .Equals — devuelve Brush Accent o Transparent.</summary>
public sealed class EqualsToAccentBrushConverter : System.Windows.Markup.MarkupExtension, IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is { Length: >= 2 } && Equals(values[0], values[1]))
            return Application.Current.Resources["Accent"]!;
        return System.Windows.Media.Brushes.Transparent;
    }
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

/// <summary>Cadena de estado (ok/err/info) → Brush.</summary>
public sealed class StatusKindToBrushConverter : IValueConverter
{
    public bool Background { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "ok"  => Background ? "OkBg"   : "Ok",
            "err" => Background ? "ErrBg"  : "Err",
            _     => Background ? "InfoBg" : "Info",
        };
        return Application.Current.Resources[key]!;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
