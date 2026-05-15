using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AnprViewer.Models;

namespace AnprViewer.Services;

/// <summary>
/// Caché en memoria de imágenes ya cargadas, evitando volver a leer
/// el blob desde SQL Server al hacer scroll y rebotar entre filas.
/// </summary>
public interface IImageCacheService
{
    /// <summary>Obtiene una imagen, cargándola si no está en caché.</summary>
    Task<BitmapSource?> GetAsync(int numeroLinea, ImageKind kind, CancellationToken ct = default);

    /// <summary>Vacía la caché. Llamar al cambiar de conexión.</summary>
    void Clear();
}

/// <summary>
/// Caché LRU con tamaño máximo en número de entradas. Construye BitmapImage
/// como <see cref="BitmapSource"/> congelado (Freeze) para poder usarlo desde
/// cualquier hilo y maximizar throughput de render.
/// </summary>
public sealed class ImageCacheService : IImageCacheService
{
    private readonly IDatabaseService _db;
    private readonly int              _capacity;
    private readonly LinkedList<string> _order = new();
    private readonly Dictionary<string, (LinkedListNode<string> Node, BitmapSource Image)> _map = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Tareas en curso para deduplicar peticiones simultáneas de la misma imagen
    private readonly ConcurrentDictionary<string, Task<BitmapSource?>> _inflight = new();

    public ImageCacheService(IDatabaseService db, int capacity)
    {
        _db       = db;
        _capacity = Math.Max(10, capacity);
    }

    public async Task<BitmapSource?> GetAsync(int numeroLinea, ImageKind kind, CancellationToken ct = default)
    {
        var key = MakeKey(numeroLinea, kind);

        // 1) Cache hit
        await _gate.WaitAsync(ct);
        try
        {
            if (_map.TryGetValue(key, out var entry))
            {
                // Mover al frente (LRU)
                _order.Remove(entry.Node);
                _order.AddFirst(entry.Node);
                return entry.Image;
            }
        }
        finally { _gate.Release(); }

        // 2) Cache miss: deduplicar peticiones concurrentes de la misma key
        var task = _inflight.GetOrAdd(key, _ => LoadAndCacheAsync(numeroLinea, kind, key, ct));
        try { return await task; }
        finally { _inflight.TryRemove(key, out _); }
    }

    private async Task<BitmapSource?> LoadAndCacheAsync(int id, ImageKind kind, string key, CancellationToken ct)
    {
        var bytes = await _db.FetchImageAsync(id, kind, ct);
        if (bytes is null || bytes.Length == 0) return null;

        var bmp = DecodeFrozen(bytes);
        if (bmp is null) return null;

        await _gate.WaitAsync(ct);
        try
        {
            if (!_map.ContainsKey(key))
            {
                var node = _order.AddFirst(key);
                _map[key] = (node, bmp);

                // Expulsar el menos recientemente usado si excedemos capacidad
                while (_map.Count > _capacity && _order.Last is { } last)
                {
                    _map.Remove(last.Value);
                    _order.RemoveLast();
                }
            }
        }
        finally { _gate.Release(); }

        return bmp;
    }

    public void Clear()
    {
        _gate.Wait();
        try { _map.Clear(); _order.Clear(); }
        finally { _gate.Release(); }
        _inflight.Clear();
    }

    /// <summary>
    /// Decodifica bytes JPEG/PNG/etc a BitmapSource y lo congela para
    /// poder cruzar hilos sin penalización.
    /// </summary>
    public static BitmapSource? DecodeFrozen(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();   // ← imprescindible para uso cross-thread
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static string MakeKey(int id, ImageKind kind) => $"{id}:{(int)kind}";
}
