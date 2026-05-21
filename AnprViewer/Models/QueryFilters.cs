using System;

namespace AnprViewer.Models;

/// <summary>
/// Filtros de búsqueda para HISTORICO y PRESENTES.
///
/// Notas de evolución:
///  · <see cref="TipoTerminal"/> sustituye al antiguo filtro textual "Movimiento".
///    Equivalencia: 0 = Entrada, 1 = Salida, 2 = Paso (columna TipoTerminal).
///  · <see cref="Terminal"/> ahora filtra por DescTerminal EXACTO (no LIKE),
///    porque sus valores se cargan dinámicamente de la propia BD.
///  · <see cref="From"/> / <see cref="To"/> son DateTime completos (con hora,
///    minutos y segundos), de modo que el rango es preciso al segundo.
/// </summary>
public sealed class QueryFilters
{
    /// <summary>Matrícula (búsqueda parcial LIKE).</summary>
    public string? Plate { get; set; }

    /// <summary>Fecha/hora inicio inclusiva (con segundos).</summary>
    public DateTime? From { get; set; }

    /// <summary>Fecha/hora fin inclusiva (con segundos).</summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Tipo de terminal: 0 = Entrada, 1 = Salida, 2 = Paso.
    /// null = sin filtro de movimiento.
    /// </summary>
    public int? TipoTerminal { get; set; }

    /// <summary>DescTerminal EXACTO (valor seleccionado del desplegable dinámico).</summary>
    public string? Terminal { get; set; }

    /// <summary>Fiabilidad mínima (0-100).</summary>
    public float MinConf { get; set; }
}
