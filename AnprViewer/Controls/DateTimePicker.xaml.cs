using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AnprViewer.Controls;

/// <summary>
/// Selector de fecha + hora/minuto/segundo, integrado con el estilo de la app.
/// Expone <see cref="SelectedDateTime"/> (DateTime?) bindable en dos vías.
///
/// · La fecha se elige con un DatePicker estándar.
/// · La hora/min/seg con tres cajas numéricas (00-23 / 00-59 / 00-59).
/// · Si no hay fecha seleccionada, SelectedDateTime es null (sin filtro).
/// </summary>
public partial class DateTimePicker : UserControl
{
    private bool _suppress;

    public static readonly DependencyProperty SelectedDateTimeProperty =
        DependencyProperty.Register(
            nameof(SelectedDateTime), typeof(DateTime?), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedDateTimeChanged));

    public DateTime? SelectedDateTime
    {
        get => (DateTime?)GetValue(SelectedDateTimeProperty);
        set => SetValue(SelectedDateTimeProperty, value);
    }

    public DateTimePicker()
    {
        InitializeComponent();
    }

    private static void OnSelectedDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DateTimePicker self) return;
        self.PushToControls(e.NewValue as DateTime?);
    }

    /// <summary>Refleja el valor externo en los controles internos sin re-disparar eventos.</summary>
    private void PushToControls(DateTime? dt)
    {
        _suppress = true;
        try
        {
            if (dt.HasValue)
            {
                DatePart.SelectedDate = dt.Value.Date;
                HourBox.Text   = dt.Value.Hour.ToString("D2");
                MinuteBox.Text = dt.Value.Minute.ToString("D2");
                SecondBox.Text = dt.Value.Second.ToString("D2");
            }
            else
            {
                DatePart.SelectedDate = null;
                HourBox.Text   = "00";
                MinuteBox.Text = "00";
                SecondBox.Text = "00";
            }
        }
        finally { _suppress = false; }
    }

    private void OnDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Recompose();
    }

    private void OnTimePartChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompose();
    }

    /// <summary>Solo permite dígitos en las cajas de hora.</summary>
    private void OnNumericInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
    }

    /// <summary>Compone SelectedDateTime a partir de la fecha + hora actual de los controles.</summary>
    private void Recompose()
    {
        // Sin fecha → sin filtro
        if (DatePart.SelectedDate is not DateTime date)
        {
            _suppress = true;
            try { SelectedDateTime = null; }
            finally { _suppress = false; }
            return;
        }

        int h = ClampInt(HourBox.Text,   0, 23);
        int m = ClampInt(MinuteBox.Text, 0, 59);
        int s = ClampInt(SecondBox.Text, 0, 59);

        var composed = new DateTime(date.Year, date.Month, date.Day, h, m, s);

        _suppress = true;
        try { SelectedDateTime = composed; }
        finally { _suppress = false; }
    }

    private static int ClampInt(string? text, int min, int max)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return min;
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}
