using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AnprViewer.Controls;

/// <summary>
/// Placa MOTO/ciclomotor (formato español):
///   ┌──┬───────┐
///   │ E│ NNNN  │   ← banda azul UE vertical a la izquierda
///   │UE├───────┤   ← 4 números arriba
///   │  │ LLL   │   ← 3 letras abajo
///   └──┴───────┘
///
/// Acepta cualquier matrícula vía <see cref="Plate"/> y la divide:
///   · Si hay dígitos y letras → dígitos arriba, letras abajo (independientemente del orden).
///   · Si sólo hay dígitos o sólo letras → todo va a una de las dos filas.
///   · Si no se puede partir → muestra el texto entero en ambas filas (mejor que parecer vacío).
/// Tolera guiones, espacios, puntos y caracteres no alfanuméricos.
/// </summary>
public partial class PlateBadgeMoto : UserControl
{
    public static readonly DependencyProperty PlateProperty =
        DependencyProperty.Register(
            nameof(Plate), typeof(string), typeof(PlateBadgeMoto),
            new PropertyMetadata("", OnPlateChanged));

    public static readonly DependencyProperty NumberPartProperty =
        DependencyProperty.Register(nameof(NumberPart), typeof(string),
            typeof(PlateBadgeMoto), new PropertyMetadata(""));

    public static readonly DependencyProperty LetterPartProperty =
        DependencyProperty.Register(nameof(LetterPart), typeof(string),
            typeof(PlateBadgeMoto), new PropertyMetadata(""));

    /// <summary>Matrícula completa, p.ej. "1234ABC", "1234-ABC", "C1234 ABC".</summary>
    public string Plate
    {
        get => (string)GetValue(PlateProperty);
        set => SetValue(PlateProperty, value);
    }

    public string NumberPart
    {
        get => (string)GetValue(NumberPartProperty);
        private set => SetValue(NumberPartProperty, value);
    }

    public string LetterPart
    {
        get => (string)GetValue(LetterPartProperty);
        private set => SetValue(LetterPartProperty, value);
    }

    public PlateBadgeMoto()
    {
        InitializeComponent();
        // Inicializar con el valor por defecto (ej. en designer)
        ApplySplit(Plate);
    }

    private static void OnPlateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlateBadgeMoto self)
            self.ApplySplit(e.NewValue as string);
    }

    private void ApplySplit(string? raw)
    {
        // ── 1. Normaliza: mayúsculas y quita todo lo que no sea alfanumérico ──
        var plate = (raw ?? "").ToUpperInvariant();
        plate = new string(plate.Where(char.IsLetterOrDigit).ToArray());

        // Tooltip de diagnóstico para detectar qué OCR llega
        ToolTip = $"Plate='{raw}' → '{plate}'";

        if (string.IsNullOrEmpty(plate))
        {
            NumberPart = "—";
            LetterPart = "";
            return;
        }

        // ── 2. Particiona dígitos y letras ──
        var digits  = new string(plate.Where(char.IsDigit).ToArray());
        var letters = new string(plate.Where(char.IsLetter).ToArray());

        // ── 3. Decide cómo mostrar ──
        if (digits.Length > 0 && letters.Length > 0)
        {
            // Caso ideal: hay de las dos → números arriba, letras abajo
            NumberPart = digits;
            LetterPart = letters;
        }
        else if (digits.Length > 0)
        {
            // Sólo dígitos: parte por la mitad para no dejar fila vacía
            int half = (digits.Length + 1) / 2;
            NumberPart = digits.Substring(0, half);
            LetterPart = digits.Substring(half);
            if (LetterPart.Length == 0) LetterPart = NumberPart;
        }
        else if (letters.Length > 0)
        {
            // Sólo letras: parte por la mitad
            int half = (letters.Length + 1) / 2;
            NumberPart = letters.Substring(0, half);
            LetterPart = letters.Substring(half);
            if (LetterPart.Length == 0) LetterPart = NumberPart;
        }
        else
        {
            // Sin nada útil: muestra el texto crudo arriba para que se vea algo
            NumberPart = plate;
            LetterPart = "";
        }
    }
}
