using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AnprViewer.Converters;

// ──────────────────────────────────────────────────────────────
//   Bool → Visibility
// ──────────────────────────────────────────────────────────────
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v != Visibility.Visible;
}

// ──────────────────────────────────────────────────────────────
//   Null → Visibility (con soporte "Invert" para ocultar si null)
// ──────────────────────────────────────────────────────────────
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value is null;
        var invert = parameter is string s &&
                     s.Equals("Invert", StringComparison.OrdinalIgnoreCase);

        // Sin Invert:   null → Visible (muestra placeholder)
        // Con Invert:   null → Collapsed (oculta la imagen vacía)
        if (invert) return isNull ? Visibility.Collapsed : Visibility.Visible;
        return isNull ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

// ──────────────────────────────────────────────────────────────
//   Fiabilidad → ancho de barra
// ──────────────────────────────────────────────────────────────
public sealed class ConfidenceToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double max = 40;
        if (parameter is string s && double.TryParse(s,
            NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
            max = p;

        if (value is null) return 0d;
        var v = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        v = Math.Max(0, Math.Min(100, v));
        return (v / 100.0) * max;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

// ──────────────────────────────────────────────────────────────
//   Fiabilidad → brush (verde/ámbar/rojo)
// ──────────────────────────────────────────────────────────────
public sealed class ConfidenceToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return GetBrush("Ok");
        var v = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (v >= 85) return GetBrush("Ok");
        if (v >= 60) return GetBrush("Warn");
        return GetBrush("Err");
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static Brush GetBrush(string key)
    {
        var app = System.Windows.Application.Current;
        if (app?.Resources[key] is Brush b) return b;
        return Brushes.Gray;
    }
}

// ──────────────────────────────────────────────────────────────
//   Clase de badge (movimiento) → brush
// ──────────────────────────────────────────────────────────────
public sealed class BadgeClassToBrushConverter : IValueConverter
{
    public bool Background { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = (value as string)?.ToLowerInvariant() ?? "default";
        var app = System.Windows.Application.Current;

        string brushKey = Background
            ? key switch
            {
                "entry" => "OkBg",
                "exit"  => "AccentBg",
                "warn"  => "WarnBg",
                _       => "Bg2"
            }
            : key switch
            {
                "entry" => "Ok",
                "exit"  => "Accent",
                "warn"  => "Warn",
                _       => "Text1"
            };

        if (app?.Resources[brushKey] is Brush b) return b;
        return Brushes.Gray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

// ──────────────────────────────────────────────────────────────
//   StatusKind (info/ok/err) → brush
// ──────────────────────────────────────────────────────────────
public sealed class StatusKindToBrushConverter : IValueConverter
{
    public bool Background { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = (value as string)?.ToLowerInvariant() ?? "info";
        var app = System.Windows.Application.Current;

        string brushKey = Background
            ? key switch
            {
                "ok"  => "OkBg",
                "err" => "ErrBg",
                _     => "AccentBg"
            }
            : key switch
            {
                "ok"  => "Ok",
                "err" => "Err",
                _     => "Accent"
            };

        if (app?.Resources[brushKey] is Brush b) return b;
        return Brushes.Gray;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

// ──────────────────────────────────────────────────────────────
//   Equals → AccentBrush (para resaltar selección)
// ──────────────────────────────────────────────────────────────
public sealed class EqualsToAccentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var app = System.Windows.Application.Current;
        var eq  = value?.Equals(parameter) == true;
        return eq
            ? (app?.Resources["Accent"] as Brush ?? Brushes.DodgerBlue)
            : (app?.Resources["Text2"]  as Brush ?? Brushes.Gray);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

// ──────────────────────────────────────────────────────────────
//   ImageKind → label
// ──────────────────────────────────────────────────────────────
public sealed class ImageKindToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            Models.ImageKind.Leida   => "Matrícula leída",
            Models.ImageKind.Frontal => "Cámara frontal",
            Models.ImageKind.Trasera => "Cámara trasera",
            Models.ImageKind.Facial  => "Cámara facial",
            Models.ImageKind.MatTras => "Matrícula trasera",
            _ => "—"
        };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
