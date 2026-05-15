using System;

namespace AnprViewer.Models;

/// <summary>
/// Registro de la tabla HISTORICO, sin los blobs de imagen.
/// Los flags HasImg* indican qué imágenes existen para poder pedirlas bajo demanda.
/// </summary>
public sealed class HistoricoRecord
{
    public int      NumeroLinea         { get; init; }
    public short    CodigoAparcamiento  { get; init; }
    public short?   NumeroTerminal      { get; init; }
    public DateTime FHGeneracion        { get; init; }
    public DateTime? FHInclusion        { get; init; }
    public string?  DescTerminal        { get; init; }
    public short?   TipoTerminal        { get; init; }
    public string?  TipoTerminalDesc    { get; init; }
    public short?   TipoMovimiento      { get; init; }
    public string?  TipoMovimientoDesc  { get; init; }

    public short?   ClaseTarjeta        { get; init; }
    public string?  DescClaseTarjeta    { get; init; }
    public short?   TipoTarjeta         { get; init; }
    public string?  DescTipoTarjeta     { get; init; }
    public string?  CodigoTarjeta       { get; init; }

    public string?  MatriculaLeida      { get; init; }
    public string?  MatriculaOperacion  { get; init; }
    public float?   FiabilidadMatricula { get; init; }
    public short?   TipoIncidencia      { get; init; }
    public short?   NumeroDeFallos      { get; init; }

    public string?  MatriculaRemolque             { get; init; }
    public float?   FiabilidadMatriculaRemolque   { get; init; }

    // Existencia de imágenes (no se traen los blobs en el listado)
    public bool HasImgLeida    { get; init; }
    public bool HasImgFrontal  { get; init; }
    public bool HasImgTrasera  { get; init; }
    public bool HasImgFacial   { get; init; }
    public bool HasImgMatTras  { get; init; }
}

/// <summary>Página de resultados.</summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Rows  { get; init; }
    public required long              Total { get; init; }
    public required int               Page  { get; init; }
    public required int               PageSize { get; init; }
    public required long              TookMs { get; init; }
}

/// <summary>Tipo de imagen asociada a un registro HISTORICO.</summary>
public enum ImageKind
{
    Leida,    // ImgMatriculaLeida
    Frontal,  // ImgFrontal
    Trasera,  // ImgTrasera
    Facial,   // ImgFacial
    MatTras   // ImgMatriculaTrasera
}
