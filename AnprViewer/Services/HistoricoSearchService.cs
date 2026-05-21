using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnprViewer.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AnprViewer.Services;

/// <summary>
/// Implementación SQL Server del servicio de búsqueda para HISTORICO y PRESENTES.
/// </summary>
public sealed class HistoricoSearchService : IHistoricoSearchService
{
    private readonly IDatabaseService _db;

    public HistoricoSearchService(IDatabaseService db) => _db = db;

    // ════════════════════════════════════════════════════════════════════
    //                           H I S T O R I C O
    // ════════════════════════════════════════════════════════════════════

    private const string HistoricoProjectionSql = @"
    h.CodigoAparcamiento,
    h.NumeroLinea,
    h.NumeroTerminal,
    h.FHGeneracion,
    h.FHInclusion,
    h.DescTerminal,
    h.TipoTerminal,
    h.TipoMovimiento,
    tm.Descripcion       AS TipoMovimientoDesc,
    tt.Descripcion       AS TipoTerminalDesc,
    h.ClaseTarjeta,
    h.DescClaseTarjeta,
    h.TipoTarjeta,
    h.DescTipoTarjeta,
    h.CodigoTarjeta,
    h.MatriculaLeida,
    h.MatriculaOperacion,
    h.TipoIncidencia,
    h.NumeroDeFallos,
    h.AccionResolucion,
    h.PuestoResolucion,
    h.UsuarioResolucion,
    h.Exportada,
    h.MatriculaRemolque,
    h.MatriculaRemolqueOper,
    h.FiabilidadMatricula,
    h.FiabilidadMatriculaRemolque,
    h.DuracionResolucion,
    h.IdTarjeta,
    CAST(CASE WHEN h.ImgMatriculaLeida   IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasImgLeida,
    CAST(CASE WHEN h.ImgFrontal          IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasImgFrontal,
    CAST(CASE WHEN h.ImgTrasera          IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasImgTrasera,
    CAST(CASE WHEN h.ImgFacial           IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasImgFacial,
    CAST(CASE WHEN h.ImgMatriculaTrasera IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasImgMatTras";

    private const string HistoricoFromSql = @"
FROM HISTORICO h WITH (NOLOCK)
LEFT JOIN DEF_TIPO_MOVIMIENTO tm WITH (NOLOCK) ON tm.TipoMovimiento = h.TipoMovimiento
LEFT JOIN DEF_TIPO_TERMINAL   tt WITH (NOLOCK) ON tt.TipoTerminal   = h.TipoTerminal";

    private const string HistoricoOrderSql = @"
ORDER BY h.FHGeneracion DESC, h.NumeroLinea DESC";

    public async Task<IReadOnlyList<HistoricoRecord>> GetUltimosRegistrosAsync(
        int limite = 100_000,
        CancellationToken ct = default)
    {
        if (limite <= 0) limite = 100_000;
        var settings = _db.CurrentSettings
            ?? throw new InvalidOperationException("No hay conexión configurada.");

        var sql = $@"
SELECT TOP (@limite)
{HistoricoProjectionSql}
{HistoricoFromSql}
{HistoricoOrderSql};";

        await using var cn = new SqlConnection(settings.ToConnectionString());
        await cn.OpenAsync(ct);

        var cmd = new CommandDefinition(
            sql,
            parameters: new { limite },
            commandTimeout: settings.CommandTimeoutSeconds,
            cancellationToken: ct);

        var rows = await cn.QueryAsync<HistoricoRecord>(cmd);
        return rows is IReadOnlyList<HistoricoRecord> list
            ? list
            : new List<HistoricoRecord>(rows);
    }

    public async Task<IReadOnlyList<HistoricoRecord>> BuscarAsync(
        QueryFilters filtros,
        int limite = 100_000,
        CancellationToken ct = default)
    {
        if (filtros is null) throw new ArgumentNullException(nameof(filtros));
        if (limite <= 0) limite = 100_000;

        var settings = _db.CurrentSettings
            ?? throw new InvalidOperationException("No hay conexión configurada.");

        var where = new StringBuilder();
        var p = new DynamicParameters();
        p.Add("@limite", limite, DbType.Int32);

        void And(string condition)
        {
            if (where.Length == 0) where.Append("WHERE ");
            else where.Append(" AND ");
            where.Append(condition);
        }

        if (!string.IsNullOrWhiteSpace(filtros.Plate))
        {
            And("h.MatriculaLeida LIKE @plate");
            p.Add("@plate", "%" + filtros.Plate.Trim() + "%", DbType.String, size: 42);
        }

        // Rango de fechas/horas PRECISO (incluye hora:min:seg)
        if (filtros.From.HasValue)
        {
            And("h.FHGeneracion >= @from");
            p.Add("@from", filtros.From.Value, DbType.DateTime);
        }
        if (filtros.To.HasValue)
        {
            And("h.FHGeneracion <= @to");
            p.Add("@to", filtros.To.Value, DbType.DateTime);
        }

        // MOVIMIENTO → ahora filtra por TipoTerminal (0=Entrada,1=Salida,2=Paso)
        if (filtros.TipoTerminal.HasValue)
        {
            And("h.TipoTerminal = @tipoTerminal");
            p.Add("@tipoTerminal", filtros.TipoTerminal.Value, DbType.Int16);
        }

        // TERMINAL → DescTerminal EXACTO (valor del desplegable dinámico)
        if (!string.IsNullOrWhiteSpace(filtros.Terminal))
        {
            And("h.DescTerminal = @descTerminal");
            p.Add("@descTerminal", filtros.Terminal.Trim(), DbType.String, size: 128);
        }

        if (filtros.MinConf > 0f)
        {
            And("h.FiabilidadMatricula >= @minConf");
            p.Add("@minConf", filtros.MinConf, DbType.Single);
        }

        var sql = $@"
SELECT TOP (@limite)
{HistoricoProjectionSql}
{HistoricoFromSql}
{where}
{HistoricoOrderSql};";

        await using var cn = new SqlConnection(settings.ToConnectionString());
        await cn.OpenAsync(ct);

        var cmd = new CommandDefinition(
            sql,
            parameters: p,
            commandTimeout: settings.CommandTimeoutSeconds,
            cancellationToken: ct);

        var rows = await cn.QueryAsync<HistoricoRecord>(cmd);
        return rows is IReadOnlyList<HistoricoRecord> list
            ? list
            : new List<HistoricoRecord>(rows);
    }

    // ─────────────────────── TERMINALES DINÁMICOS ────────────────────────
    public async Task<IReadOnlyList<string>> GetTerminalesPorTipoAsync(
        int tipoTerminal,
        CancellationToken ct = default)
    {
        var settings = _db.CurrentSettings
            ?? throw new InvalidOperationException("No hay conexión configurada.");

        // DISTINCT de DescTerminal para el tipo solicitado, ordenado alfabéticamente.
        var sql = @"
SELECT DISTINCT h.DescTerminal
FROM HISTORICO h WITH (NOLOCK)
WHERE h.TipoTerminal = @tipo
  AND h.DescTerminal IS NOT NULL
  AND LTRIM(RTRIM(h.DescTerminal)) <> ''
ORDER BY h.DescTerminal;";

        await using var cn = new SqlConnection(settings.ToConnectionString());
        await cn.OpenAsync(ct);

        var cmd = new CommandDefinition(
            sql,
            parameters: new { tipo = (short)tipoTerminal },
            commandTimeout: settings.CommandTimeoutSeconds,
            cancellationToken: ct);

        var rows = await cn.QueryAsync<string>(cmd);
        return rows is IReadOnlyList<string> list ? list : new List<string>(rows);
    }

    // ════════════════════════════════════════════════════════════════════
    //                           P R E S E N T E S
    // ════════════════════════════════════════════════════════════════════

    private const string PresenteProjectionSql = @"
    p.CodigoAparcamiento,
    p.IdTarjeta,
    p.ClaseTarjeta,
    p.DescClaseTarjeta,
    p.TipoTarjeta,
    p.DescTipoTarjeta,
    p.CodigoTarjeta,
    p.TerminalEntrada,
    p.DescTerminalEntrada,
    p.FechaEntrada,
    p.MatriculaEntrada,
    p.FiabilidadMatricula,
    CAST(CASE WHEN p.ImgMatriculaEntrada IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasImgEntrada,
    CAST(CASE WHEN p.ImgMatriculaTrasera IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasImgTrasera";

    private const string PresenteFromSql = @"
FROM PRESENTES p WITH (NOLOCK)";

    private const string PresenteOrderSql = @"
ORDER BY p.FechaEntrada DESC";

    public async Task<IReadOnlyList<PresenteRecord>> GetPresentesAsync(
        CancellationToken ct = default)
    {
        var settings = _db.CurrentSettings
            ?? throw new InvalidOperationException("No hay conexión configurada.");

        var sql = $@"
SELECT
{PresenteProjectionSql}
{PresenteFromSql}
{PresenteOrderSql};";

        await using var cn = new SqlConnection(settings.ToConnectionString());
        await cn.OpenAsync(ct);

        var cmd = new CommandDefinition(
            sql,
            commandTimeout: settings.CommandTimeoutSeconds,
            cancellationToken: ct);

        var rows = await cn.QueryAsync<PresenteRecord>(cmd);
        return rows is IReadOnlyList<PresenteRecord> list
            ? list
            : new List<PresenteRecord>(rows);
    }

    public async Task<IReadOnlyList<PresenteRecord>> BuscarPresentesAsync(
        QueryFilters filtros,
        CancellationToken ct = default)
    {
        if (filtros is null) throw new ArgumentNullException(nameof(filtros));

        var settings = _db.CurrentSettings
            ?? throw new InvalidOperationException("No hay conexión configurada.");

        var where = new StringBuilder();
        var p = new DynamicParameters();

        void And(string condition)
        {
            if (where.Length == 0) where.Append("WHERE ");
            else where.Append(" AND ");
            where.Append(condition);
        }

        if (!string.IsNullOrWhiteSpace(filtros.Plate))
        {
            And("p.MatriculaEntrada LIKE @plate");
            p.Add("@plate", "%" + filtros.Plate.Trim() + "%", DbType.String, size: 42);
        }
        if (filtros.From.HasValue)
        {
            And("p.FechaEntrada >= @from");
            p.Add("@from", filtros.From.Value, DbType.DateTime);
        }
        if (filtros.To.HasValue)
        {
            And("p.FechaEntrada <= @to");
            p.Add("@to", filtros.To.Value, DbType.DateTime);
        }

        // En PRESENTES el filtro Terminal sigue actuando sobre DescTerminalEntrada (exacto)
        if (!string.IsNullOrWhiteSpace(filtros.Terminal))
        {
            And("p.DescTerminalEntrada = @descTerminal");
            p.Add("@descTerminal", filtros.Terminal.Trim(), DbType.String, size: 128);
        }

        if (filtros.MinConf > 0f)
        {
            And("p.FiabilidadMatricula >= @minConf");
            p.Add("@minConf", filtros.MinConf, DbType.Single);
        }

        var sql = $@"
SELECT
{PresenteProjectionSql}
{PresenteFromSql}
{where}
{PresenteOrderSql};";

        await using var cn = new SqlConnection(settings.ToConnectionString());
        await cn.OpenAsync(ct);

        var cmd = new CommandDefinition(
            sql,
            parameters: p,
            commandTimeout: settings.CommandTimeoutSeconds,
            cancellationToken: ct);

        var rows = await cn.QueryAsync<PresenteRecord>(cmd);
        return rows is IReadOnlyList<PresenteRecord> list
            ? list
            : new List<PresenteRecord>(rows);
    }
}
