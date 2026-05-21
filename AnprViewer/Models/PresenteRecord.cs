using System;

namespace AnprViewer.Models;

/// <summary>
/// Registro de la tabla dbo.PRESENTES (vehículo actualmente en el parking).
/// Las columnas IMAGE NO se proyectan en el listado; se exponen flags HasImg* y la
/// carga real se hace bajo demanda vía <see cref="IDatabaseService.FetchPresentImageAsync"/>.
/// </summary>
public sealed class PresenteRecord
{
    // ── Identificación ──────────────────────────────────────────
    public string?   IdTarjeta           { get; init; }
    public short?    ClaseTarjeta        { get; init; }
    public string?   DescClaseTarjeta    { get; init; }
    public short?    TipoTarjeta         { get; init; }
    public string?   DescTipoTarjeta     { get; init; }
    public string?   CodigoTarjeta       { get; init; }

    // ── Entrada ─────────────────────────────────────────────────
    public short?    TerminalEntrada     { get; init; }
    public string?   DescTerminalEntrada { get; init; }
    public DateTime  FechaEntrada        { get; init; }
    public string?   MatriculaEntrada    { get; init; }
    public float?    FiabilidadMatricula { get; init; }

    // ── Aparcamiento ────────────────────────────────────────────
    public short?    CodigoAparcamiento  { get; init; }

    // ── Flags de imágenes (proyección ligera en listado) ───────
    public bool HasImgEntrada { get; init; }
    public bool HasImgTrasera { get; init; }

    /// <summary>
    /// Clave estable para el caché y la carga de imágenes.
    /// Combina IdTarjeta + FechaEntrada porque PRESENTES no tiene NumeroLinea.
    /// </summary>
    public string CacheKey =>
        $"{IdTarjeta ?? "_"}|{FechaEntrada:yyyyMMddHHmmss}";
}
