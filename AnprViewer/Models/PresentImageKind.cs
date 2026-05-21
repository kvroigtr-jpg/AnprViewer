namespace AnprViewer.Models;

/// <summary>
/// Tipo de imagen en la tabla PRESENTES (sólo dos columnas IMAGE disponibles).
/// </summary>
public enum PresentImageKind
{
    /// <summary>Columna ImgMatriculaEntrada (placa coche).</summary>
    Entrada,

    /// <summary>Columna ImgMatriculaTrasera (placa moto).</summary>
    Trasera,
}
