using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnprViewer.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AnprViewer.Services;

/// <summary>
/// Acceso a datos contra SQL Server usando Dapper.
/// Las queries son idénticas en lógica al backend Node.js: pagination
/// server-side con OFFSET/FETCH, JOIN con DEF_TIPO_MOVIMIENTO y DEF_TIPO_TERMINAL,
/// y DATALENGTH() para saber si existen las imágenes sin transferir los blobs.
/// </summary>
public sealed class SqlServerDatabaseService : IDatabaseService
{
    private string? _connectionString;

    public bool IsConfigured => !string.IsNullOrEmpty(_connectionString);
    public ConnectionSettings? CurrentSettings { get; private set; }

    public void Configure(ConnectionSettings settings)
    {
        CurrentSettings   = settings;
        _connectionString = settings.ToConnectionString();
    }

    public async Task<bool> TestConnectionAsync(ConnectionSettings settings, CancellationToken ct = default)
    {
        await using var con = new SqlConnection(settings.ToConnectionString());
        await con.OpenAsync(ct);
        var result = await con.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT 1", commandTimeout: settings.ConnectTimeoutSeconds, cancellationToken: ct));
        return result == 1;
    }

    public async Task<PagedResult<HistoricoRecord>> QueryHistoricoAsync(
        QueryFilters filters, int page, int pageSize, CancellationToken ct = default)
    {
        EnsureConfigured();

        var sw     = Stopwatch.StartNew();
        var offset = (page - 1) * pageSize;

        // ⚠️ Importante con tablas de 200-300 GB:
        //  - El WHERE prioriza FHGeneracion (suele estar indexado)
        //  - DATALENGTH() comprueba existencia sin traer el blob
        //  - WITH (NOLOCK) reduce contención en lectura analítica
        const string sql = @"
;WITH F AS (
    SELECT h.NumeroLinea
    FROM HISTORICO h WITH (NOLOCK)
    LEFT JOIN DEF_TIPO_MOVIMIENTO mov WITH (NOLOCK) ON h.TipoMovimiento = mov.TipoMovimiento
    LEFT JOIN DEF_TIPO_TERMINAL   ter WITH (NOLOCK) ON h.TipoTerminal   = ter.TipoTerminal
    WHERE
          (@Plate    IS NULL OR h.MatriculaLeida LIKE '%' + @Plate + '%')
      AND (@From     IS NULL OR h.FHGeneracion >= @From)
      AND (@To       IS NULL OR h.FHGeneracion <= @To)
      AND (@MinConf  = 0     OR h.FiabilidadMatricula >= @MinConf)
      AND (@Type     IS NULL OR mov.Descripcion LIKE '%' + @Type + '%')
      AND (@Terminal IS NULL OR ter.Descripcion LIKE '%' + @Terminal + '%')
)
SELECT
    h.NumeroLinea, h.CodigoAparcamiento, h.NumeroTerminal,
    h.FHGeneracion, h.FHInclusion, h.DescTerminal,
    h.TipoTerminal, ter.Descripcion AS TipoTerminalDesc,
    h.TipoMovimiento, mov.Descripcion AS TipoMovimientoDesc,
    h.ClaseTarjeta, h.DescClaseTarjeta,
    h.TipoTarjeta, h.DescTipoTarjeta, h.CodigoTarjeta,
    h.MatriculaLeida, h.MatriculaOperacion,
    h.FiabilidadMatricula, h.TipoIncidencia, h.NumeroDeFallos,
    h.MatriculaRemolque, h.FiabilidadMatriculaRemolque,
    CAST(CASE WHEN DATALENGTH(h.ImgMatriculaLeida)   > 0 THEN 1 ELSE 0 END AS bit) AS HasImgLeida,
    CAST(CASE WHEN DATALENGTH(h.ImgFrontal)          > 0 THEN 1 ELSE 0 END AS bit) AS HasImgFrontal,
    CAST(CASE WHEN DATALENGTH(h.ImgTrasera)          > 0 THEN 1 ELSE 0 END AS bit) AS HasImgTrasera,
    CAST(CASE WHEN DATALENGTH(h.ImgFacial)           > 0 THEN 1 ELSE 0 END AS bit) AS HasImgFacial,
    CAST(CASE WHEN DATALENGTH(h.ImgMatriculaTrasera) > 0 THEN 1 ELSE 0 END AS bit) AS HasImgMatTras
FROM F
INNER JOIN HISTORICO h WITH (NOLOCK)            ON h.NumeroLinea = F.NumeroLinea
LEFT JOIN  DEF_TIPO_MOVIMIENTO mov WITH (NOLOCK) ON h.TipoMovimiento = mov.TipoMovimiento
LEFT JOIN  DEF_TIPO_TERMINAL   ter WITH (NOLOCK) ON h.TipoTerminal   = ter.TipoTerminal
ORDER BY h.FHGeneracion DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT_BIG(*) AS Total
FROM HISTORICO h WITH (NOLOCK)
LEFT JOIN DEF_TIPO_MOVIMIENTO mov WITH (NOLOCK) ON h.TipoMovimiento = mov.TipoMovimiento
LEFT JOIN DEF_TIPO_TERMINAL   ter WITH (NOLOCK) ON h.TipoTerminal   = ter.TipoTerminal
WHERE
      (@Plate    IS NULL OR h.MatriculaLeida LIKE '%' + @Plate + '%')
  AND (@From     IS NULL OR h.FHGeneracion >= @From)
  AND (@To       IS NULL OR h.FHGeneracion <= @To)
  AND (@MinConf  = 0     OR h.FiabilidadMatricula >= @MinConf)
  AND (@Type     IS NULL OR mov.Descripcion LIKE '%' + @Type + '%')
  AND (@Terminal IS NULL OR ter.Descripcion LIKE '%' + @Terminal + '%');
";

        var parameters = new
        {
            Plate    = NullIfBlank(filters.Plate),
            From     = filters.From,
            To       = filters.To,
            Type     = NullIfBlank(filters.Type),
            Terminal = NullIfBlank(filters.Terminal),
            MinConf  = filters.MinConf,
            Offset   = offset,
            PageSize = pageSize,
        };

        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync(ct);

        var cmd = new CommandDefinition(sql, parameters,
            commandTimeout: CurrentSettings?.CommandTimeoutSeconds ?? 60,
            cancellationToken: ct);

        using var multi = await con.QueryMultipleAsync(cmd);
        var rows  = (await multi.ReadAsync<HistoricoRecord>()).AsList();
        var total = await multi.ReadFirstAsync<long>();

        sw.Stop();
        return new PagedResult<HistoricoRecord>
        {
            Rows = rows, Total = total, Page = page, PageSize = pageSize, TookMs = sw.ElapsedMilliseconds
        };
    }

    public async Task<byte[]?> FetchImageAsync(int numeroLinea, ImageKind kind, CancellationToken ct = default)
    {
        EnsureConfigured();

        var column = kind switch
        {
            ImageKind.Leida    => "ImgMatriculaLeida",
            ImageKind.Frontal  => "ImgFrontal",
            ImageKind.Trasera  => "ImgTrasera",
            ImageKind.Facial   => "ImgFacial",
            ImageKind.MatTras  => "ImgMatriculaTrasera",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        // Lectura columnar única; el WHERE usa la PK NumeroLinea (rápido).
        var sql = $"SELECT {column} FROM HISTORICO WITH (NOLOCK) WHERE NumeroLinea = @Id";

        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync(ct);
        var cmd = new CommandDefinition(sql, new { Id = numeroLinea },
            commandTimeout: CurrentSettings?.CommandTimeoutSeconds ?? 60,
            cancellationToken: ct);

        var data = await con.ExecuteScalarAsync<byte[]?>(cmd);

        // ⚠️ Si tus imágenes están cifradas, aquí va el descifrado:
        //     data = MiDescifrador.Decrypt(data, claveSecreta);

        return data is { Length: > 0 } ? data : null;
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("La conexión no ha sido configurada. Llama a Configure() primero.");
    }

    private static string? NullIfBlank(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
