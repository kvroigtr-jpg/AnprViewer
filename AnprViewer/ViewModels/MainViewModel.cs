using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AnprViewer.Models;
using AnprViewer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AnprViewer.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDatabaseService       _db;
    private readonly IImageCacheService     _cache;
    private readonly IListPdfExportService  _listPdf;
    private readonly IRecordPdfExportService _recordPdf;
    private readonly DispatcherTimer        _debounceTimer;
    private CancellationTokenSource?        _currentLoad;

    // Buffer de la última página cargada (usado por la exportación a PDF de listado)
    private IReadOnlyList<HistoricoRecord> _lastLoadedRecords = Array.Empty<HistoricoRecord>();
    private QueryFilters?                  _lastFilters;

public MainViewModel(
    IDatabaseService db,
    IImageCacheService cache,
    IListPdfExportService listPdf,
    IRecordPdfExportService recordPdf)
{
    _db      = db;
    _cache   = cache;
    _listPdf = listPdf;
    _recordPdf = recordPdf;

    _debounceTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(300)
    };

    _debounceTimer.Tick += (_, _) =>
    {
        _debounceTimer.Stop();
        _ = LoadAsync();
    };

    ConnectionLabel = _db.CurrentSettings is { } s
        ? $"SQL · {s.Server}/{s.Database}"
        : "Sin conexión";
}

    // ── Filtros ────────────────────────────────────────────────
    [ObservableProperty] private string _plateFilter = "";
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string _typeFilter = "";
    [ObservableProperty] private string _terminalFilter = "";
    [ObservableProperty] private string _minConfidence = "0";

    // ── Paginación ─────────────────────────────────────────────
    [ObservableProperty] private int   _pageSize = 100;
    [ObservableProperty] private int   _currentPage = 1;
    [ObservableProperty] private long  _totalRecords;
    [ObservableProperty] private long  _totalPages = 1;
    [ObservableProperty] private long  _queryTimeMs;

    // ── Estado UI ──────────────────────────────────────────────
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _connectionLabel = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private HistoricoRowViewModel? _selectedRow;

    public ObservableCollection<HistoricoRowViewModel> Rows { get; } = new();

    partial void OnPlateFilterChanged(string value)     => RestartDebounce();
    partial void OnFromDateChanged(DateTime? value)      { CurrentPage = 1; _ = LoadAsync(); }
    partial void OnToDateChanged(DateTime? value)        { CurrentPage = 1; _ = LoadAsync(); }
    partial void OnTypeFilterChanged(string value)       { CurrentPage = 1; _ = LoadAsync(); }
    partial void OnTerminalFilterChanged(string value)   { CurrentPage = 1; _ = LoadAsync(); }
    partial void OnMinConfidenceChanged(string value)    { CurrentPage = 1; _ = LoadAsync(); }
    partial void OnPageSizeChanged(int value)            { CurrentPage = 1; _ = LoadAsync(); }

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        CurrentPage = 1;
        _debounceTimer.Start();
    }

    [RelayCommand] private void ClearFilters()
    {
        _debounceTimer.Stop();
        PlateFilter = "";
        FromDate    = null;
        ToDate      = null;
        TypeFilter  = "";
        TerminalFilter = "";
        MinConfidence  = "0";
        CurrentPage    = 1;
        _ = LoadAsync();
    }

    [RelayCommand] private void FirstPage() { if (CurrentPage > 1) { CurrentPage = 1; _ = LoadAsync(); } }
    [RelayCommand] private void PrevPage()  { if (CurrentPage > 1) { CurrentPage--; _ = LoadAsync(); } }
    [RelayCommand] private void NextPage()  { if (CurrentPage < TotalPages) { CurrentPage++; _ = LoadAsync(); } }
    [RelayCommand] private void LastPage()  { if (CurrentPage < TotalPages) { CurrentPage = (int)TotalPages; _ = LoadAsync(); } }
    [RelayCommand] private void Refresh()   => _ = LoadAsync();
    [RelayCommand]
    private async Task ExportSelectedRecordPdfAsync(HistoricoRowViewModel row)
    {
        if (row is null)
            return;

        var record = row.Record;

        var dlg = new SaveFileDialog
        {
            FileName = $"Registro_{record.MatriculaLeida}_{record.FHGeneracion:yyyyMMdd_HHmmss}.pdf",
            Filter = "PDF (*.pdf)|*.pdf"
        };

        if (dlg.ShowDialog() != true)
            return;

        await _recordPdf.ExportRecordAsync(record, dlg.FileName);
    }

    // ── Exportar PDF del LISTADO de la página actual ───────────
    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        if (_lastLoadedRecords.Count == 0)
        {
            MessageBox.Show(
                "No hay registros que exportar.\nCarga primero una página de resultados.",
                "Exportar listado a PDF",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var defaultName = $"Listado_Matriculas_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        var dlg = new SaveFileDialog
        {
            FileName     = defaultName,
            Filter       = "PDF (*.pdf)|*.pdf",
            Title        = "Exportar listado a PDF",
            DefaultExt   = ".pdf",
            AddExtension = true,
        };
        if (dlg.ShowDialog() != true) return;

        IsLoading = true;
        var prevStatus = StatusText;
        StatusText = $"Generando PDF con {_lastLoadedRecords.Count} registros…";
        try
        {
            await _listPdf.ExportListAsync(_lastLoadedRecords, _lastFilters, dlg.FileName);
            StatusText = $"PDF guardado: {Path.GetFileName(dlg.FileName)} ({_lastLoadedRecords.Count} registros)";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dlg.FileName,
                    UseShellExecute = true,
                });
            }
            catch { /* sin visor PDF asociado */ }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo generar el PDF:\n\n{ex.Message}",
                "Exportar listado a PDF",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText = prevStatus;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Carga de datos ─────────────────────────────────────────
    public async Task LoadAsync()
    {
        _currentLoad?.Cancel();
        var cts = new CancellationTokenSource();
        _currentLoad = cts;

        IsLoading = true;
        StatusText = "Consultando…";
        try
        {
            var filters = new QueryFilters
            {
                Plate    = string.IsNullOrWhiteSpace(PlateFilter)    ? null : PlateFilter.Trim().ToUpperInvariant(),
                From     = FromDate,
                To       = ToDate,
                Type     = string.IsNullOrWhiteSpace(TypeFilter)     ? null : TypeFilter.Trim(),
                Terminal = string.IsNullOrWhiteSpace(TerminalFilter) ? null : TerminalFilter.Trim(),
                MinConf  = float.TryParse(MinConfidence, System.Globalization.NumberStyles.Float,
                                          System.Globalization.CultureInfo.InvariantCulture, out var mc) ? mc : 0f,
            };

            var result = await _db.QueryHistoricoAsync(filters, CurrentPage, PageSize, cts.Token);
            if (cts.IsCancellationRequested) return;

            // Guardar para exportación a PDF
            _lastLoadedRecords = result.Rows;
            _lastFilters       = filters;

            Rows.Clear();
            foreach (var r in result.Rows)
                Rows.Add(new HistoricoRowViewModel(r, _cache));

            TotalRecords = result.Total;
            TotalPages   = Math.Max(1, (long)Math.Ceiling(result.Total / (double)PageSize));
            QueryTimeMs  = result.TookMs;

            var from = result.Total == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
            var to   = Math.Min(result.Total, (long)CurrentPage * PageSize);
            StatusText = $"Mostrando {from:N0}–{to:N0} de {result.Total:N0} · {result.TookMs} ms";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            if (_currentLoad == cts)
            {
                IsLoading = false;
                _currentLoad = null;
            }
        }
    }
}
