using System.Diagnostics;
using System.IO.Compression;
using GoogleMapsComponents.Maps;
using LandsatReflectance.SceneBoundaries;
// using LandsatReflectance.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Exception = System.Exception;

namespace LandsatReflectance.UI.Services;

public class Wrs2AreasService
{
    private readonly ILogger<Wrs2AreasService> _logger;
    private readonly IWebAssemblyHostEnvironment m_environment;
    private readonly IJSRuntime m_jsRuntime;

    private SceneManager? _sceneManager = null;

    public Wrs2AreasService(ILogger<Wrs2AreasService> logger, IWebAssemblyHostEnvironment environment, IJSRuntime jsRuntime)
    {
        _logger = logger;
        m_environment = environment;
        m_jsRuntime = jsRuntime;
    }

    public async Task<SceneManager> GetSceneManager()
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        var regionsZipFileBytes = await m_jsRuntime.InvokeAsync<byte[]>("fetchRegionsZipBytes");

        if (regionsZipFileBytes == null || regionsZipFileBytes.Length == 0)
        {
            throw new Exception("Failed to fetch or retrieve the Gzip file.");
        }

        var sceneManager = SceneManager.TryLoadFromBytes(regionsZipFileBytes);
        
        stopwatch.Stop();
        if (!m_environment.IsProduction())
        {
            _logger.LogInformation($"Loaded \"SceneManager\" from memory in {stopwatch.Elapsed.TotalMilliseconds:F} ms");
        }

        return sceneManager;
    }

    public async Task<List<Wrs2Scene>> GetScenes(LatLong latLong)
    {
        _sceneManager ??= await GetSceneManager();
        
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var scenes = _sceneManager.GetScenes(latLong).ToList();
        
        stopwatch.Stop();
        if (!m_environment.IsProduction())
        {
            _logger.LogInformation($"Loaded {scenes.Count} scenes at \"{latLong}\" in {stopwatch.Elapsed.TotalMilliseconds:F} ms");
        }

        return scenes;
    }

    /*
    public bool IsInitialized()
    {
        return Wrs2Areas.Count == 0;
    }

    public async Task InitWrs2Areas()
    {
        if (Wrs2Areas.Count == 0)
        {
            Wrs2Areas = await FetchWrs2Areas();
        }
        
        if (m_environment.IsDevelopment())
        {
            Console.WriteLine($"[Wrs2AreasService] Loaded {Wrs2Areas.Count} areas.");
        }
    }
    
    private async Task<List<Wrs2Area>> FetchWrs2Areas()
    {
        var fileBytes = await m_jsRuntime.InvokeAsync<byte[]>("fetchWrs2AreasGz");

        if (fileBytes == null || fileBytes.Length == 0)
        {
            // TODO: Create custom exception & figure out exception handling
            throw new Exception("Failed to fetch or retrieve the Gzip file.");
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        using var inputStream = new MemoryStream(fileBytes);
        await using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var decompressedStream = new MemoryStream();
        await gzipStream.CopyToAsync(decompressedStream);
        var decompressedBytes = decompressedStream.ToArray();
        
        stopwatch.Stop();

        if (m_environment.IsDevelopment())
        {
            Console.WriteLine($"[Wrs2AreasService] Loaded wrs2 areas file with #bytes = {decompressedBytes.Length}");
            Console.WriteLine($"[Wrs2AreasService] Elapsed: {stopwatch.Elapsed:g}");
        }
        
        stopwatch.Restart();
        
        using var decompressedByteMemoryStream = new MemoryStream(decompressedBytes);
        var data = ProtoBuf.Serializer.Deserialize<List<Wrs2Area>>(decompressedByteMemoryStream);
        
        stopwatch.Stop();
        
        if (m_environment.IsDevelopment())
        {
            Console.WriteLine($"[Wrs2AreasService] Deserialized Wrs2 areas with protobuf. Elapsed: {stopwatch.Elapsed:g}");
        }

        return data;
    }
     */
}