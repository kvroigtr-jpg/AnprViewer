using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AnprViewer.Services;
using AnprViewer.ViewModels;
using AnprViewer.Views;
using QuestPDF.Infrastructure;

namespace AnprViewer;

public partial class App : Application
{
    public static IDatabaseService         Db              { get; private set; } = null!;
    public static IImageCacheService       ImageCache      { get; private set; } = null!;
    public static IConnectionSettingsStore ConnectionStore { get; private set; } = null!;
    public static IListPdfExportService    ListPdfExport   { get; private set; } = null!;
    public static IRecordPdfExportService  RecordPdfExport { get; private set; } = null!;
    public static IHistoricoSearchService  HistoricoSearch { get; private set; } = null!;
    public static IPresenteImageService    PresenteImages  { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        TaskScheduler.UnobservedTaskException += OnTaskException;

        try
        {
            // TEMA
            ThemeService.LoadAndApply();

            // SERVICIOS BASE
            ConnectionStore = new ConnectionSettingsStore();
            Db              = new SqlServerDatabaseService();
            ImageCache      = new ImageCacheService(Db, capacity: 200);
            PresenteImages  = new PresenteImageService(Db);

            // PDF
            ListPdfExport   = new ListPdfExportService();
            RecordPdfExport = new RecordPdfExportService(Db, PresenteImages);

            // BÚSQUEDA OPTIMIZADA (HISTORICO + PRESENTES)
            HistoricoSearch = new HistoricoSearchService(Db);

            // AUTO-CONEXIÓN
            string? autoErr  = null;
            bool    connected = false;
            var     saved     = ConnectionStore.Load();

            if (saved is { AutoConnect: true }
                && !string.IsNullOrWhiteSpace(saved.Server)
                && !string.IsNullOrWhiteSpace(saved.Database)
                && (saved.IntegratedSecurity || !string.IsNullOrWhiteSpace(saved.Username)))
            {
                try
                {
                    if (await Db.TestConnectionAsync(saved))
                    {
                        Db.Configure(saved);
                        connected = true;
                    }
                    else
                    {
                        autoErr = "La validación de la BD no pasó.";
                    }
                }
                catch (Exception ex)
                {
                    autoErr = ex.Message;
                }
            }

            // LOGIN si no hay auto-conexión
            if (!connected)
            {
                var connVm = new ConnectionViewModel(Db, ConnectionStore);
                if (autoErr is not null)
                {
                    connVm.StatusKind    = "err";
                    connVm.StatusMessage = "Auto-conexión fallida: " + autoErr;
                }
                var connWin = new ConnectionWindow { DataContext = connVm };

                if (connWin.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            // MAIN
            var mainVm  = new MainViewModel(Db, HistoricoSearch, ImageCache, PresenteImages,
                                            ListPdfExport, RecordPdfExport);
            var mainWin = new MainWindow { DataContext = mainVm };

            MainWindow = mainWin;
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            mainWin.Show();
            _ = SafeInitialLoadAsync(mainVm);
        }
        catch (Exception ex)
        {
            ShowError("Error durante el arranque", ex);
            Shutdown();
        }
    }

    private static async Task SafeInitialLoadAsync(MainViewModel vm)
    {
        try { await vm.LoadAsync(); }
        catch (Exception ex) { ShowError("Error al cargar los datos iniciales", ex); }
    }

    private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs ex)
    {
        ShowError("Error no controlado (UI)", ex.Exception);
        ex.Handled = true;
    }

    private static void OnDomainException(object sender, UnhandledExceptionEventArgs ex)
    {
        if (ex.ExceptionObject is Exception e)
            ShowError("Excepcion critica del dominio", e);
    }

    private static void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs ex)
    {
        Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            ShowError("Excepcion no observada en tarea async", ex.Exception);
        }));
        ex.SetObserved();
    }

    private static void ShowError(string title, Exception? ex)
    {
        if (ex is null)
        {
            MessageBox.Show("(Excepcion nula)", title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(new string('=', 70));
        sb.AppendLine();

        var current = ex;
        int level = 0;
        while (current != null)
        {
            sb.Append("[Nivel ").Append(level).Append("] ")
              .AppendLine(current.GetType().FullName);
            sb.AppendLine(current.Message);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                sb.AppendLine("Stack trace:");
                sb.AppendLine(current.StackTrace);
                sb.AppendLine();
            }
            current = current.InnerException;
            level++;
        }

        var full = sb.ToString();
        try { Clipboard.SetText(full); } catch { }

        MessageBox.Show(
            full + Environment.NewLine + "(Detalle copiado al portapapeles)",
            "Gestor Matrículas - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
