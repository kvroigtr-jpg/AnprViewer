# ANPR Viewer — Aplicación de escritorio WPF (.NET 8)

Migración del visor HTML a aplicación WPF empresarial, conectada a SQL Server.
Conserva la estética y experiencia del HTML original (tema oscuro, paneles, badges, miniaturas, modal de imagen con zoom) y la lleva a un `.exe` nativo de Windows con MVVM real y rendimiento pensado para 200–300 GB.

---

## 1. Arquitectura

```
AnprViewer.sln
└── AnprViewer/                ← Proyecto WPF (.NET 8)
    ├── App.xaml / App.xaml.cs ← Composition root + manejo global de excepciones
    ├── appsettings.json
    │
    ├── Models/                ← POCOs y enums
    │   ├── HistoricoRecord.cs ← 1:1 con la tabla HISTORICO (sin blobs)
    │   └── QueryFilters.cs    ← Filtros + ConnectionSettings (cadena conexión)
    │
    ├── Services/              ← Capa de acceso a datos / infra
    │   ├── IDatabaseService.cs / SqlServerDatabaseService.cs
    │   │     Queries Dapper con OFFSET/FETCH, NOLOCK, DATALENGTH()
    │   ├── IImageCacheService.cs / ImageCacheService.cs
    │   │     Caché LRU thread-safe con dedupe de peticiones concurrentes
    │   └── ConnectionSettingsStore.cs
    │         Persistencia opcional en %APPDATA% (sin contraseña)
    │
    ├── ViewModels/            ← MVVM (CommunityToolkit.Mvvm)
    │   ├── ConnectionViewModel.cs    Pantalla de conexión
    │   ├── MainViewModel.cs          Dashboard: filtros, paginación, debounce
    │   ├── HistoricoRowViewModel.cs  Wrapper de fila con lazy thumb
    │   └── ImageViewerViewModel.cs   Modal de imagen
    │
    ├── Views/                 ← XAML
    │   ├── ConnectionWindow.xaml    Login a SQL Server
    │   ├── MainWindow.xaml          Dashboard principal (replica HTML)
    │   └── ImageViewerWindow.xaml   Modal con strip + visor + detalle
    │
    ├── Controls/
    │   └── ZoomableImage.cs   ← Control personalizado: zoom rueda, pan, rotar
    │
    ├── Converters/
    │   └── Converters.cs      ← Value/MultiValue converters
    │
    └── Styles/
        ├── Theme.xaml         ← Tokens de color (idénticos al HTML)
        └── Controls.xaml      ← Estilos de Button, DataGrid, Input, etc.
```

### Decisiones técnicas

| Decisión | Por qué |
|---|---|
| **MVVM con CommunityToolkit.Mvvm** | `[ObservableProperty]` y `[RelayCommand]` generan boilerplate automáticamente. Sin frameworks pesados. |
| **Dapper + Microsoft.Data.SqlClient** | Dapper compila parámetros de forma segura (sin SQL injection) y mapea POCOs sin overhead de un ORM completo. `Microsoft.Data.SqlClient` es el driver actual mantenido. |
| **Virtualización en DataGrid** | `EnableRowVirtualization`, `EnableColumnVirtualization`, `VirtualizingPanel.VirtualizationMode=Recycling`. Sólo se materializan las filas visibles. |
| **Server-side paging** | `OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY`. Nunca traemos toda la tabla. |
| **DATALENGTH() para flags de imagen** | Saber si hay imágenes sin transferir los blobs (gigabytes). |
| **Caché LRU thread-safe** | Evita recargar imágenes al hacer scroll/back-and-forth. Capacidad 200 por defecto. |
| **BitmapImage.Freeze()** | Permite usar la imagen desde cualquier hilo y elimina coste de marshalling. |
| **Debounce 300 ms en matrícula** | Evita una query por cada tecla pulsada. |
| **CancellationToken en cada consulta** | Si el usuario lanza una nueva búsqueda, la anterior se cancela. |
| **NOLOCK en queries de lectura** | Reduce contención con escrituras concurrentes en la tabla HISTORICO. Quitar si tu negocio exige consistencia transaccional. |

---

## 2. Cómo compilar

### Opción A · Visual Studio 2022 (recomendado)

1. Abre **`AnprViewer.sln`** en Visual Studio 2022 (17.10 o posterior con la carga *.NET desktop development*).
2. Visual Studio descargará automáticamente los paquetes NuGet la primera vez (Dapper, Microsoft.Data.SqlClient, CommunityToolkit.Mvvm).
3. Pulsa **F5** para depurar o **Ctrl+Shift+B** para compilar.

### Opción B · Línea de comandos

```bash
cd AnprViewer
dotnet restore
dotnet build -c Release
dotnet run --project AnprViewer
```

### Opción C · Publicar como `.exe` único (sin dependencias)

```bash
dotnet publish AnprViewer\AnprViewer.csproj ^
  -c Release -r win-x64 --self-contained ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o publish
```

El ejecutable queda en `publish\AnprViewer.exe` (~80 MB, incluye .NET runtime). Si el equipo destino ya tiene .NET 8 instalado, puedes omitir `--self-contained` y obtendrás ~5 MB.

Para generar un **instalador MSI**, lo más rápido es WiX 4 o Inno Setup, partiendo de la carpeta `publish`.

---

## 3. Flujo de uso

1. Al arrancar se abre la **ventana de conexión**:
   - Servidor + puerto (o instancia con nombre tipo `SQLEXPRESS`).
   - Base de datos.
   - Autenticación SQL (user/pass) o Windows Auth.
   - Pulsar **"Probar conexión"** verifica que la BD responde sin entrar al dashboard.
   - **"Conectar a SQL Server"** valida y abre el dashboard.
   - "Recordar configuración" guarda todo excepto la contraseña en `%APPDATA%\AnprViewer\connection.json`.

2. **Dashboard**:
   - Barra de filtros: matrícula (con debounce), rango de fechas, tipo de movimiento, tipo de terminal, fiabilidad mínima.
   - DataGrid virtualizado con miniatura lazy-loaded.
   - Paginación server-side: 50/100/250/500 por página.

3. **Doble click en una fila** (o icono ◉) abre el **visor de imagen**:
   - Strip lateral con las imágenes disponibles del registro (frontal, trasera, leída, facial, mat. trasera).
   - Zoom con rueda del ratón centrado en el cursor.
   - Pan arrastrando.
   - Rotación 90°.
   - Atajos: `Esc` cerrar · `+`/`-` zoom · `0` ajustar · `R` rotar · `←`/`→` cambiar imagen · `D` guardar.

---

## 4. Imágenes cifradas

Si tus blobs están cifrados, el punto de descifrado está marcado con `⚠️` en
`Services/SqlServerDatabaseService.cs`, dentro de `FetchImageAsync`:

```csharp
var data = await con.ExecuteScalarAsync<byte[]?>(cmd);
// ⚠️ Si tus imágenes están cifradas, aquí va el descifrado:
//     data = MiDescifrador.Decrypt(data, claveSecreta);
return data is { Length: > 0 } ? data : null;
```

Sustituye la línea por tu desencriptado real. El resto del pipeline (decodificación con `BitmapImage`, caché, render) no necesita cambios.

---

## 5. Rendimiento — checklist real

- **Índices SQL recomendados** sobre `HISTORICO`:
  ```sql
  CREATE INDEX IX_HISTORICO_FHGeneracion ON HISTORICO(FHGeneracion DESC);
  CREATE INDEX IX_HISTORICO_Matricula    ON HISTORICO(MatriculaLeida);
  ```
  El primero es crítico: el dashboard ordena `ORDER BY FHGeneracion DESC` y la mayoría de filtros acotan por fecha.

- **OFFSET grande es caro**: páginas profundas (`OFFSET 5000000`) tardan. En la práctica, el usuario nunca llega ahí porque siempre filtra. Si necesitas saltar a páginas profundas habitualmente, considera keyset pagination (`WHERE FHGeneracion < @lastSeen`).

- **CommandTimeout = 60 s** por defecto. Si tus queries tardan más, ajusta `CommandTimeoutSeconds` en `ConnectionSettings`.

- **Caché LRU de 200 imágenes**: ajusta el valor en `App.xaml.cs` (parámetro `capacity` al construir `ImageCacheService`).

---

## 6. Estructura entregada

```
AnprViewer/
├── AnprViewer.sln
└── AnprViewer/
    ├── AnprViewer.csproj
    ├── App.xaml / App.xaml.cs
    ├── appsettings.json
    ├── Models/
    ├── Services/
    ├── ViewModels/
    ├── Views/
    ├── Controls/
    ├── Converters/
    └── Styles/
```

Total ~25 archivos, ~3.000 líneas de C# + XAML. Listo para `dotnet build` o abrir en VS 2022.
