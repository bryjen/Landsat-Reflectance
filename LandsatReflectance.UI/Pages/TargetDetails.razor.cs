using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Blazored.SessionStorage;
using LandsatReflectance.SceneBoundaries;
using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace LandsatReflectance.UI.Pages;

public partial class TargetDetails : ComponentBase
{
    [Inject]
    public required ILogger<TargetDetails> Logger { get; set; }
    
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required HttpClient HttpClient { get; set; }
    
    [Inject]
    public required JsonSerializerOptions JsonSerializerOptions { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
    [Inject]
    public required ISyncSessionStorageService SessionStorageService { get; set; }
    
    [Inject]
    public required ApiTargetService ApiTargetService { get; set; }
    
    [Inject]
    public required GeocodingService GeocodingService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }
    
    [Inject]
    public required CurrentTargetsService CurrentTargetsService { get; set; }

    
    [CascadingParameter]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; }
    
    [Parameter]
    [SupplyParameterFromQuery(Name = "target-id")]
    public string? TargetId { get; set; }
    
    [Parameter]
    [SupplyParameterFromQuery(Name = "path")]
    public string? Path { get; set; }
    
    [Parameter]
    [SupplyParameterFromQuery(Name = "row")]
    public string? Row { get; set; }
    
    [Parameter]
    [SupplyParameterFromQuery(Name = "latitude")]
    public string? Latitude { get; set; }
    
    [Parameter]
    [SupplyParameterFromQuery(Name = "longitude")]
    public string? Longitude { get; set; }
    
    [Parameter]
    [SupplyParameterFromQuery(Name = "min-cc-filter")]
    public string? MinCloudCoverFilter { get; set; }
    
    [Parameter]
    [SupplyParameterFromQuery(Name = "max-cc-filter")]
    public string? MaxCloudCoverFilter { get; set; }

    
    private bool _isLoading = true;
    private string? _loadingMsg = "Loading Images ...";
    
    private bool _showQuickSummary;
    private bool _showImageLoadingDelayWarning = true;
    
    private Target? _target;
    private string? _errorMsg;

    private int _currentSceneIndex;
    private LocationData? _locationData;
    private Dictionary<SceneData, string> _sceneDataToImgStrMap = new();

    // Zoom style value in %
    private double _imageZoom = 33.3;


    protected override void OnParametersSet()
    {
        CurrentUserService.OnUserAuthenticated += CurrentTargetsService.SaveTargetsCreatedOffline;
        CurrentUserService.OnUserAuthenticated += CurrentTargetsService.LoadUserTargets;
        
        CurrentUserService.OnUserLogout += CurrentTargetsService.OnUserLogout;
    }

    protected override async Task OnAfterRenderAsync(bool isFirstRender)
    {
        // Logic to load targets based on cookie auth
        if (isFirstRender && CurrentUserService.IsAuthenticated && !CurrentTargetsService.HasLoadedUserTargets)
        {
            await CurrentTargetsService.LoadUserTargetsCore(CurrentUserService.AccessToken);
        }

        if (isFirstRender)
        {
            TryInitTarget();
            var sceneDatas = await TryGetSceneData();
            
            _loadingMsg = "Caching Images ...";
            StateHasChanged();
            
            _sceneDataToImgStrMap = await GetSceneToImgStrMap(sceneDatas);
            
            _isLoading = false;
            StateHasChanged();
            
            TryGetLocation();
        }
    }
    
    protected void Dispose()
    {
        #nullable disable
        CurrentUserService.OnUserAuthenticated -= CurrentTargetsService.SaveTargetsCreatedOffline;
        CurrentUserService.OnUserAuthenticated -= CurrentTargetsService.LoadUserTargets;
        
        CurrentUserService.OnUserLogout -= CurrentTargetsService.OnUserLogout;
        #nullable enable
    }

    
    private void TryInitTarget()
    {
        if (Path is null)
        {
            _errorMsg = "The parameter \"path\" is missing.";
            return;
        }
        if (!int.TryParse(Path, out var path))
        {
            _errorMsg = $"Could not parse the value \"{Path}\" for \"path\". Ensure that it is a valid integer.";
            return;
        }
        
        
        if (Row is null)
        {
            _errorMsg = "The parameter \"row\" is missing.";
            return;
        }
        if (!int.TryParse(Row, out var row))
        {
            _errorMsg = $"Could not parse the value \"{Row}\" for \"row\". Ensure that it is a valid integer.";
            return;
        }
        
        
        if (Latitude is null)
        {
            _errorMsg = "The parameter \"latitude\" is missing.";
            return;
        }
        if (!double.TryParse(Latitude, out var latitude))
        {
            _errorMsg = $"Could not parse the value \"{Latitude}\" for \"latitude\". Ensure that it is a valid double.";
            return;
        }
        
        
        if (Longitude is null)
        {
            _errorMsg = "The parameter \"longitude\" is missing.";
            return;
        }
        if (!double.TryParse(Longitude, out var longitude))
        {
            _errorMsg = $"Could not parse the value \"{Longitude}\" for \"longitude\". Ensure that it is a valid double.";
            return;
        }
        
        
        if (MinCloudCoverFilter is null)
        {
            _errorMsg = "The parameter \"min-cc-filter\" is missing.";
            return;
        }
        if (!double.TryParse(MinCloudCoverFilter, out var minCloudCoverFilter))
        {
            _errorMsg = $"Could not parse the value \"{MinCloudCoverFilter}\" for \"min-cc-filter\". Ensure that it is a valid double.";
            return;
        }
        
        
        if (MaxCloudCoverFilter is null)
        {
            _errorMsg = "The parameter \"max-cc-filter\" is missing.";
            return;
        }
        if (!double.TryParse(MaxCloudCoverFilter, out var maxCloudCoverFilter))
        {
            _errorMsg = $"Could not parse the value \"{MaxCloudCoverFilter}\" for \"max-cc-filter\". Ensure that it is a valid double.";
            return;
        }

        var targetId = Guid.Empty;
        if (TargetId is not null)
        {
            if (!Guid.TryParse(TargetId, out var asGuid))
            {
                _errorMsg = $"Could not parse \"{TargetId}\" as guid.";
                return;
            }

            targetId = asGuid;
        }

        _target = new Target
        {
            Id = targetId,
            Path = path,
            Row = row,
            Latitude = latitude,
            Longitude = longitude,
            MinCloudCoverFilter = minCloudCoverFilter,
            MaxCloudCoverFilter = maxCloudCoverFilter,
        };
    }

    private async Task<List<SceneData>> TryGetSceneData()
    {
        if (_target is null)
        {
            return new();
        }
        
        var targetKey = HashTarget(_target);
        SceneData[] sceneDatas;
        if (SessionStorageService.ContainKey(targetKey))
        {
            await Task.Delay(TimeSpan.FromSeconds(0.1));  // Delay required because page loads too fast ???
            sceneDatas = SessionStorageService.GetItem<SceneData[]>(targetKey);
        }
        else
        {
            sceneDatas = await ApiTargetService.TryGetSceneData(_target.Path, _target.Row, 10);
            SessionStorageService.SetItem(targetKey, sceneDatas);
        }
        
        return sceneDatas.OrderByDescending(sceneData => sceneData.PublishDate).ToList();
    }

    private async void TryGetLocation()
    {
        if (_target is not null)
        {
            try
            {
                var asLatLong = new LatLong((float)_target.Latitude, (float)_target.Longitude);
                _locationData = await GeocodingService.GetNearestCity(asLatLong);
                StateHasChanged();
            }
            catch (Exception)
            {
                // TODO: Display some message here
            }
        }
    }

    private async Task<Dictionary<SceneData, string>> GetSceneToImgStrMap(List<SceneData> sceneDatas)
    {
        var tasks = new List<Task<string?>>();
        
        foreach (var sceneData in sceneDatas)
        {
            var sessionStorageKey = HashBrowsePath(sceneData);
            if (sceneData.BrowsePath is not null)
            {
                if (SessionStorageService.ContainKey(sessionStorageKey))
                {
                    var value = SessionStorageService.GetItem<string?>(sessionStorageKey);
                    tasks.Add(Task.FromResult(value));
                }
                else
                {
                    tasks.Add(GetSceneImgDataString(sceneData));
                }
            }
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        var imgDataStrsNullable = await Task.WhenAll(tasks);
        var imgDataStrs = sceneDatas
            .Zip(imgDataStrsNullable)
            .Where(tuple => tuple.Second is not null)
            .ToDictionary(tuple => tuple.First, tuple => tuple.Second!);
        
        stopwatch.Stop();
        if (!Environment.IsProduction())
        {
            Logger.LogInformation($"{stopwatch.Elapsed:g}");
        }

        return imgDataStrs;
    }

    private async Task<string?> GetSceneImgDataString(SceneData sceneData)
    {
        const string regexPattern = "product_id=([\\w\\s]+)";
        var match = Regex.Match(sceneData.BrowsePath!, regexPattern);

        if (match.Success && match.Groups.Count != 0)
        {
            var productId = match.Groups[1].Value;
            var response = await HttpClient.GetAsync($"scene-data-str?product-id={productId}");
            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(responseBody, JsonSerializerOptions);

            if (apiResponse is null)
            {
                return null;
            }

            if (apiResponse.ErrorMessage is not null)
            {
                return null;
            }

            var dataStr = apiResponse.Data;
            SessionStorageService.SetItem(HashBrowsePath(sceneData), dataStr);
            return dataStr;
        }

        return null;
    }

    
    private static string HashBrowsePath(SceneData sceneData) => $"browse-path:{sceneData.EntityId}";
    
    private static string HashTarget(Target target) => $"target-details:{target.Id};{target.Latitude};{target.Longitude}";
}