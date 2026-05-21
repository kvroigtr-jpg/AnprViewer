using System.Threading;
using System.Threading.Tasks;
using AnprViewer.Models;

namespace AnprViewer.Services;

/// <summary>
/// Recupera bytes de las dos columnas IMAGE de PRESENTES.
/// La tabla PRESENTES no tiene NumeroLinea: se identifica por IdTarjeta + FechaEntrada.
/// </summary>
public interface IPresenteImageService
{
    Task<byte[]?> FetchAsync(
        PresenteRecord record,
        PresentImageKind kind,
        CancellationToken ct = default);
}
