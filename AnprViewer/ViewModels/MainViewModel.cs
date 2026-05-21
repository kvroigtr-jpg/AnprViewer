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
using AnprViewer.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AnprViewer.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const int CargaMaxima = 100_000;

    private readonly IDatabaseService        _db;
    private readonly IHistoricoSearchService _search;
    private readonly IImageCacheService      _cache;
    private readonly IPresenteImageService   _presenteImages;
    private readonly IListPdfExportService   _listPdf;
    private readonly IRecordPdfExportService _recordPdf;
    private readonly DispatcherTimer         _debounceTimer;
    private CancellationTokenSource?         _currentLoad;

    private IReadOnlyList<HistoricoRecord> _loadedHistorico = Array.Empty<HistoricoRecord>();
    private IReadOnlyList<PresenteRecord>  _loadedPresentes = Array.Empty<PresenteRecord>();
    private QueryFilters? _lastFilters;

    public MainViewModel(
        IDatabaseService db,
        IHistoricoSearchService search,
        IImageCacheService cache,
        IPresenteImageService presenteImages,
        IListPdfExportService listPdf,
        IRecordPdfExportService recordPdf)
    {
        _db             = db;
        _search         = search;
        _cache          = cache;
        _presenteImages = presenteImages;
        _listPdf        = listPdf;
        _recordPdf      = recordPdf;

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            _ = LoadAsync();
        };

        ConnectionLabel = _db.CurrentSettings is { } s
            ? $"SQL · {s.Server}/{s.Database}"
            : "Sin conexión";
    }

    // ── Modo de vista ─────────────────────────────────────────
    [ObservableProperty] private ModoVista _modoVistaActual = ModoVista.Historico;

    public bool IsHistoricoMode => ModoVistaActual == ModoVista.Historico;
    public bool IsPresentesMode => ModoVistaActual == ModoVista.Presentes;

    partial void OnModoVistaActualChanged(ModoVista value)
    {
        OnPropertyChanged(nameof(IsHistoricoMode));
        OnPropertyChanged(nameof(IsPresentesMode));
        CurrentPage = 1;
        _ = LoadAsync();
    }

    // ── Filtros ────────────────────────────────────────────────
    [ObservableProperty] private string   _plateFilter = "";
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string   _minConfidence = "0";

    /// <summary>
    /// Movimiento → mapeado a TipoTerminal. Valores Tag del combo:
    /// "" (Todos), "0" (Entrada), "1" (Salida), "2" (Paso).
    /// </summary>
    [ObservableProperty] private string _movimientoTipo = "";

    /// <summary>DescTerminal seleccionado (valor exacto del desplegable dinámico).</summary>
    [ObservableProperty] private string? _terminalSeleccionado;

    /// <summary>Lista dinámica de terminales según el Movimiento elegido.</summary>
    public ObservableCollection<string> TerminalesDisponibles { get; } = new();

    [ObservableProperty] private bool _terminalEnabled;

    // ── Paginación cliente ─────────────────────────────────────
    [ObservableProperty] private int  _pageSize = 100;
    [ObservableProperty] private int  _currentPage = 1;
    [ObservableProperty] private long _totalRecords;
    [ObservableProperty] private long _totalPages = 1;
    [ObservableProperty] private long _queryTimeMs;

    // ── Estado UI ──────────────────────────────────────────────
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _connectionLabel = "";
    [ObservableProperty] private string _statusText = "";

    [ObservableProperty] private HistoricoRowViewModel? _selectedRow;
    [ObservableProperty] private PresenteRowViewModel?  _selectedPresente;

    public ObservableCollection<HistoricoRowViewModel> Rows        { get; } = new();
    public ObservableCollection<PresenteRowViewModel>  PresentRows { get; } = new();

    // ── Reactividad de filtros ─────────────────────────────────
    partial void OnPlateFilterChanged(string value)    => RestartDebounce();
    partial void OnFromDateChanged(DateTime? value)    { CurrentPage = 1; _ = LoadAsync(); }
    partial void OnToDateChanged(DateTime? value)      { CurrentPage = 1; _ = LoadAsync(); }
    partial void OnMinConfidenceChanged(string value)  { CurrentPage = 1; _ = LoadAsync(); }
    partial void OnPageSizeChanged(int value)          { CurrentPage = 1; RepaginateFromBuffer(); }

    /// <summary>Al cambiar Movimiento: recarga terminales dinámicos y relanza búsqueda.</summary>
    partial void OnMovimientoTipoChanged(string value)
    {
        CurrentPage = 1;
        // Limpia la selección de terminal previa (ya no aplica al nuevo tipo)
        TerminalSeleccionado = null;
        _ = ReloadTerminalesYBuscarAsync();
    }

    /// <summary>Al elegir un terminal concreto: relanza búsqueda.</summary>
    partial void OnTerminalSeleccionadoChanged(string? value)
    {
        CurrentPage = 1;
        _ = LoadAsync();
    }

    private async Task ReloadTerminalesYBuscarAsync()
    {
        await ReloadTerminalesAsync();
        await LoadAsync();
    }

    /// <summary>
    /// Carga DescTerminal DISTINTOS para el TipoTerminal del Movimiento actual.
    /// Si "Todos" (sin tipo) → vacía y deshabilita el desplegable de terminal.
    /// </summary>
    private async Task ReloadTerminalesAsync()
    {
        TerminalesDisponibles.Clear();

        if (!TryParseTipoTerminal(MovimientoTipo, out var tipo))
        {
            // "Todos": no hay subconjunto de terminales
            TerminalEnabled = false;
            return;
        }

        try
        {
            var terminales = await _search.GetTerminalesPorTipoAsync(tipo);
            foreach (var t in terminales)
                TerminalesDisponibles.Add(t);
            TerminalEnabled = TerminalesDisponibles.Count > 0;
        }
        catch
        {
            TerminalEnabled = false;
        }
    }

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        CurrentPage = 1;
        _debounceTimer.Start();
    }

    [RelayCommand]
    private async Task ClearFilters()
    {
        _debounceTimer.Stop();
        PlateFilter          = "";
        FromDate             = null;
        ToDate               = null;
        MinConfidence        = "0";
        MovimientoTipo       = "";
        TerminalSeleccionado = null;
        TerminalesDisponibles.Clear();
        TerminalEnabled      = false;
        CurrentPage          = 1;
        await LoadAsync();
    }

    // ── Paginación cliente (sin BD) ────────────────────────────
    [RelayCommand] private void FirstPage() { if (CurrentPage > 1)          { CurrentPage = 1; RepaginateFromBuffer(); } }
    [RelayCommand] private void PrevPage()  { if (CurrentPage > 1)          { CurrentPage--;   RepaginateFromBuffer(); } }
    [RelayCommand] private void NextPage()  { if (CurrentPage < TotalPages) { CurrentPage++;   RepaginateFromBuffer(); } }
    [RelayCommand] private void LastPage()  { if (CurrentPage < TotalPages) { CurrentPage = (int)TotalPages; RepaginateFromBuffer(); } }

    [RelayCommand]
    private void Refresh() => _ = LoadAsync();

    [RelayCommand]
    private void SwitchView(string mode)
    {
        if (string.IsNullOrEmpty(mode)) return;
        if (Enum.TryParse<ModoVista>(mode, ignoreCase: true, out var m))
            ModoVistaActual = m;
    }

    [RelayCommand]
    private async Task ExportSelectedRecordPdfAsync(object row)
    {
        if (row is HistoricoRowViewModel hr)
        {
            var record = hr.Record;
            var dlg = new SaveFileDialog
            {
                FileName = $"Registro_{record.MatriculaLeida}_{record.FHGeneracion:yyyyMMdd_HHmmss}.pdf",
                Filter   = "PDF (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() != true) return;
            await _recordPdf.ExportHistoricoAsync(record, dlg.FileName);
        }
        else if (row is PresenteRowViewModel pr)
        {
            var record = pr.Record;
            var dlg = new SaveFileDialog
            {
                FileName = $"Presente_{record.MatriculaEntrada}_{record.FechaEntrada:yyyyMMdd_HHmmss}.pdf",
                Filter   = "PDF (*.pdf)|*.pdf"
            };
            if (dlg.ShowDialog() != true) return;
            await _recordPdf.ExportPresenteAsync(record, dlg.FileName);
        }
    }

    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        var defaultName = ModoVistaActual switch
        {
            ModoVista.Presentes => $"Listado_Presentes_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
            _                    => $"Listado_Matriculas_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
        };

        var totalCargados = ModoVistaActual == ModoVista.Historico
            ? _loadedHistorico.Count
            : _loadedPresentes.Count;

        if (totalCargados == 0)
        {
            MessageBox.Show(
                "No hay registros que exportar.\nCarga primero una página de resultados.",
                "Exportar listado a PDF",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

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
        StatusText = $"Generando PDF con {totalCargados} registros…";
        try
        {
            if (ModoVistaActual == ModoVista.Historico)
                await _listPdf.ExportListAsync(_loadedHistorico, _lastFilters, dlg.FileName);
            else
                await _listPdf.ExportPresentesListAsync(_loadedPresentes, _lastFilters, dlg.FileName);

            StatusText = $"PDF guardado: {Path.GetFileName(dlg.FileName)} ({totalCargados} registros)";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dlg.FileName,
                    UseShellExecute = true,
                });
            }
            catch { }
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

    [RelayCommand]
    private async Task ChangeConnectionAsync()
    {
        var vm  = new ConnectionViewModel(_db, App.ConnectionStore);
        var win = new ConnectionWindow
        {
            DataContext = vm,
            Owner       = Application.Current.MainWindow,
        };

        if (win.ShowDialog() != true) return;

        App.ImageCache.Clear();

        ConnectionLabel = _db.CurrentSettings is { } s
            ? $"SQL · {s.Server}/{s.Database}"
            : "Sin conexión";

        CurrentPage = 1;
        await LoadAsync();
    }

    // ═════════════════════════════════════════════════════════════
    //                    CARGA DESDE BASE DE DATOS
    // ═════════════════════════════════════════════════════════════
    public async Task LoadAsync()
    {
        _currentLoad?.Cancel();
        var cts = new CancellationTokenSource();
        _currentLoad = cts;

        IsLoading  = true;
        StatusText = "Consultando…";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var filters      = BuildFilters();
            var hasAnyFilter = HasAnyFilter(filters);

            if (ModoVistaActual == ModoVista.Historico)
            {
                var rows = hasAnyFilter
                    ? await _search.BuscarAsync(filters, CargaMaxima, cts.Token)
                    : await _search.GetUltimosRegistrosAsync(CargaMaxima, cts.Token);

                if (cts.IsCancellationRequested) return;
                _loadedHistorico = rows;
                _loadedPresentes = Array.Empty<PresenteRecord>();
            }
            else
            {
                var rows = hasAnyFilter
                    ? await _search.BuscarPresentesAsync(filters, cts.Token)
                    : await _search.GetPresentesAsync(cts.Token);

                if (cts.IsCancellationRequested) return;
                _loadedPresentes = rows;
                _loadedHistorico = Array.Empty<HistoricoRecord>();
            }

            sw.Stop();
            _lastFilters = filters;
            QueryTimeMs  = sw.ElapsedMilliseconds;

            RepaginateFromBuffer();
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
                IsLoading    = false;
                _currentLoad = null;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    //          Paginación cliente sobre el buffer cargado
    // ═════════════════════════════════════════════════════════════
    private void RepaginateFromBuffer()
    {
        if (ModoVistaActual == ModoVista.Historico)
            RepaginateHistorico();
        else
            RepaginatePresentes();
    }

    private void RepaginateHistorico()
    {
        PresentRows.Clear();

        var total = _loadedHistorico.Count;
        TotalRecords = total;
        TotalPages   = Math.Max(1, (long)Math.Ceiling(total / (double)PageSize));

        if (CurrentPage > TotalPages) CurrentPage = (int)TotalPages;
        if (CurrentPage < 1)          CurrentPage = 1;

        var skip = (CurrentPage - 1) * PageSize;
        var take = Math.Min(PageSize, Math.Max(0, total - skip));

        Rows.Clear();
        for (int i = 0; i < take; i++)
            Rows.Add(new HistoricoRowViewModel(_loadedHistorico[skip + i], _cache));

        var from = total == 0 ? 0 : skip + 1;
        var to   = skip + take;
        var cap  = _loadedHistorico.Count >= CargaMaxima
            ? $" · LÍMITE {CargaMaxima:N0} (afina filtros para ver más antiguos)"
            : "";
        StatusText = $"Histórico · Mostrando {from:N0}–{to:N0} de {total:N0} · {QueryTimeMs} ms{cap}";
    }

    private void RepaginatePresentes()
    {
        Rows.Clear();

        var total = _loadedPresentes.Count;
        TotalRecords = total;
        TotalPages   = Math.Max(1, (long)Math.Ceiling(total / (double)PageSize));

        if (CurrentPage > TotalPages) CurrentPage = (int)TotalPages;
        if (CurrentPage < 1)          CurrentPage = 1;

        var skip = (CurrentPage - 1) * PageSize;
        var take = Math.Min(PageSize, Math.Max(0, total - skip));

        PresentRows.Clear();
        for (int i = 0; i < take; i++)
            PresentRows.Add(new PresenteRowViewModel(_loadedPresentes[skip + i], _presenteImages));

        var from = total == 0 ? 0 : skip + 1;
        var to   = skip + take;
        StatusText = $"Presentes · Mostrando {from:N0}–{to:N0} de {total:N0} · {QueryTimeMs} ms";
    }

    // ─────────────────────────── Helpers ──────────────────────────
    private QueryFilters BuildFilters()
    {
        int? tipo = TryParseTipoTerminal(MovimientoTipo, out var t) ? t : (int?)null;

        return new QueryFilters
        {
            Plate        = string.IsNullOrWhiteSpace(PlateFilter) ? null : PlateFilter.Trim().ToUpperInvariant(),
            From         = FromDate,
            To           = ToDate,
            TipoTerminal = tipo,
            Terminal     = string.IsNullOrWhiteSpace(TerminalSeleccionado) ? null : TerminalSeleccionado!.Trim(),
            MinConf      = float.TryParse(MinConfidence,
                               System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture,
                               out var mc) ? mc : 0f,
        };
    }

    private static bool TryParseTipoTerminal(string? tag, out int tipo)
    {
        tipo = 0;
        if (string.IsNullOrWhiteSpace(tag)) return false;
        return int.TryParse(tag, out tipo);
    }

    private static bool HasAnyFilter(QueryFilters f)
        => !string.IsNullOrWhiteSpace(f.Plate)
        || f.From.HasValue
        || f.To.HasValue
        || f.TipoTerminal.HasValue
        || !string.IsNullOrWhiteSpace(f.Terminal)
        || f.MinConf > 0f;
}
