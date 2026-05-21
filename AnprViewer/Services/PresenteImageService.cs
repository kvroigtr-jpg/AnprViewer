using System;
using System.Threading;
using System.Threading.Tasks;
using AnprViewer.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AnprViewer.Services;

/// <summary>
/// Recupera los bytes de las columnas IMAGE de la tabla PRESENTES.
/// Identifica el registro por IdTarjeta + FechaEntrada (no hay NumeroLinea).
/// </summary>
public sealed class PresenteImageService : IPresenteImageService
{
    private readonly IDatabaseService _db;

    public PresenteImageService(IDatabaseService db) => _db = db;

    public async Task<byte[]?> FetchAsync(
        PresenteRecord record,
        PresentImageKind kind,
        CancellationToken ct = default)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));

        var settings = _db.CurrentSettings
            ?? throw new InvalidOperationException("No hay conexión configurada.");

        var column = kind switch
        {
            PresentImageKind.Entrada => "ImgMatriculaEntrada",
            PresentImageKind.Trasera => "ImgMatriculaTrasera",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        var sql = $@"
SELECT TOP (1) {column}
FROM PRESENTES WITH (NOLOCK)
WHERE IdTarjeta = @id
  AND FechaEntrada = @fecha";

        await using var cn = new SqlConnection(settings.ToConnectionString());
        await cn.OpenAsync(ct);

        var cmd = new CommandDefinition(
            sql,
            parameters: new
            {
                id    = record.IdTarjeta ?? string.Empty,
                fecha = record.FechaEntrada,
            },
            commandTimeout: settings.CommandTimeoutSeconds,
            cancellationToken: ct);

        return await cn.ExecuteScalarAsync<byte[]?>(cmd);
    }
}
