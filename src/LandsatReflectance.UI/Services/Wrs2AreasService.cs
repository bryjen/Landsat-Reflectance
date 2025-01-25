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

    public bool IsInit() => _sceneManager is not null;

    public async Task<SceneManager> InitIfNull()
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
        _sceneManager ??= await InitIfNull();
        
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
}