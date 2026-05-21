using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnprViewer.Models;

namespace AnprViewer.Services;

/// <summary>
/// Servicio de búsqueda optimizado para HISTORICO y PRESENTES.
/// </summary>
public interface IHistoricoSearchService
{
    // ───────────────────────────── HISTORICO ─────────────────────────────
    Task<IReadOnlyList<HistoricoRecord>> GetUltimosRegistrosAsync(
        int limite = 100_000,
        CancellationToken ct = default);

    Task<IReadOnlyList<HistoricoRecord>> BuscarAsync(
        QueryFilters filtros,
        int limite = 100_000,
        CancellationToken ct = default);

    // ───────────────────────────── PRESENTES ─────────────────────────────
    Task<IReadOnlyList<PresenteRecord>> GetPresentesAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<PresenteRecord>> BuscarPresentesAsync(
        QueryFilters filtros,
        CancellationToken ct = default);

    // ─────────────────────── TERMINALES DINÁMICOS ────────────────────────
    /// <summary>
    /// Devuelve los DescTerminal DISTINTOS de HISTORICO que tienen el
    /// TipoTerminal indicado (0=Entrada, 1=Salida, 2=Paso), ordenados.
    /// Sin duplicados. Usado por el desplegable dinámico de "Terminal".
    /// </summary>
    Task<IReadOnlyList<string>> GetTerminalesPorTipoAsync(
        int tipoTerminal,
        CancellationToken ct = default);
}
