using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace AnprViewer.Services;

/// <summary>
/// Gestor de tema global. Aplica el cambio MUTANDO la propiedad Color
/// de los SolidColorBrush definidos en Theme.xaml.
/// Requiere que dichos brushes estén marcados con po:Freeze="False".
///
/// Implementa <see cref="INotifyPropertyChanged"/> sobre la propiedad
/// <see cref="Version"/> para poder bindear contenido theme-aware
/// (p. ej. selección de logo) y refrescar tras Toggle.
/// </summary>
public sealed class ThemeService : INotifyPropertyChanged
{
    public enum Mode { Dark, Light }

    // Singleton para poder bindear desde XAML (x:Static)
    public static ThemeService Instance { get; } = new();

    private static readonly string PrefPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AnprViewer", "theme.txt");

    public static Mode Current { get; private set; } = Mode.Dark;

    /// <summary>Contador que se incrementa en cada cambio de tema.</summary>
    public int Version { get; private set; }

    public static event Action<Mode>? ThemeChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    private ThemeService() { }

    // (brushKey, dark RGB, light RGB)
    private static readonly (string Key, byte Dr, byte Dg, byte Db, byte Lr, byte Lg, byte Lb)[] Map =
    {
        ("Bg0",      0x0A,0x0D,0x12,   0xF5,0xF7,0xFA),
        ("Bg1",      0x0F,0x14,0x1B,   0xFF,0xFF,0xFF),
        ("Bg2",      0x16,0x1D,0x27,   0xF0,0xF3,0xF7),
        ("Bg3",      0x1D,0x26,0x32,   0xE4,0xE9,0xEF),
        ("BgHover",  0x22,0x2D,0x3A,   0xEC,0xF1,0xF7),
        ("Line",     0x22,0x2B,0x38,   0xD8,0xDF,0xE6),
        ("LineSoft", 0x1A,0x21,0x2C,   0xEA,0xEE,0xF3),
        ("Text0",    0xE6,0xED,0xF5,   0x0F,0x14,0x1B),
        ("Text1",    0xAA,0xB4,0xC2,   0x32,0x3B,0x47),
        ("Text2",    0x6B,0x77,0x87,   0x5B,0x66,0x74),
        ("Text3",    0x47,0x50,0x63,   0x8A,0x95,0xA3),
    };

    public static void LoadAndApply()
    {
        var mode = Mode.Dark;
        try
        {
            if (File.Exists(PrefPath))
            {
                var txt = File.ReadAllText(PrefPath).Trim();
                if (Enum.TryParse<Mode>(txt, true, out var parsed)) mode = parsed;
            }
        }
        catch { /* ignore */ }
        Apply(mode);
    }

    public static void Apply(Mode mode)
    {
        Current = mode;
        var res = Application.Current?.Resources;
        if (res is null) return;

        foreach (var e in Map)
        {
            try
            {
                if (res[e.Key] is not SolidColorBrush b) continue;

                var target = mode == Mode.Dark
                    ? Color.FromRgb(e.Dr, e.Dg, e.Db)
                    : Color.FromRgb(e.Lr, e.Lg, e.Lb);

                if (b.IsFrozen)
                {
                    Debug.WriteLine($"[ThemeService] Brush '{e.Key}' está frozen — falta po:Freeze=\"False\" en Theme.xaml.");
                    res[e.Key] = new SolidColorBrush(target);
                }
                else
                {
                    b.Color = target;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThemeService] No se pudo aplicar '{e.Key}': {ex.Message}");
            }
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefPath)!);
            File.WriteAllText(PrefPath, mode.ToString());
        }
        catch { /* ignore */ }

        // Forzar refresco de bindings theme-aware (logo, etc.)
        Instance.Version++;
        Instance.Raise(nameof(Version));

        ThemeChanged?.Invoke(mode);
    }

    public static void Toggle()
        => Apply(Current == Mode.Dark ? Mode.Light : Mode.Dark);

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
