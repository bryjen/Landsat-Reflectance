using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Blazored.SessionStorage;
using LandsatReflectance.SceneBoundaries;
using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Components.Dialog;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;

namespace LandsatReflectance.UI.Pages.DetailedView;

public partial class DetailedView : ComponentBase
{
    [Inject]
    public required ILogger<DetailedView> Logger { get; set; }
    
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required HttpClient HttpClient { get; set; }
    
    [Inject]
    public required JsonSerializerOptions JsonSerializerOptions { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
    [Inject]
    public required IDialogService DialogService { get; set; }
    
    [Inject]
    public required ISnackbar Snackbar { get; set; }
    
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
    
    
    [CascadingParameter(Name = "OnUnhandledError")]
    public EventCallback<(Exception, Func<Task>?)> OnUnhandledError { get; set; }
    
    [CascadingParameter(Name = "FullPageLoadingOverlay")]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; }

    
    private bool _isLoading = true;
    private string? _loadingMsg = "Loading Images ...";
    
    private Target? _target;
    private string? _errorMsg;

    private int _currentSceneIndex;
    private ReverseGeocodingData? _locationData;
    private Dictionary<SceneData, string> _sceneDataToImgStrMap = new();


    private int _images = 10;
    private int _skip = 0;
    private bool _showLandsat8 = true;
    private bool _showLandsat9 = true;
    private double _minCloudCover = 0;
    private double _maxCloudCover = 1;
    public CloudCoverFilter _cloudCoverFilter { get; set; } = CloudCoverFilter.None;
    

    protected override void OnInitialized()
    {
        // Clear the cache of any previous images.
        // Should prevent memory exceeding that of the max/quota.
        foreach (var key in SessionStorageService.Keys())
        {
            if (key.StartsWith("browse-path:"))
            {
                SessionStorageService.RemoveItem(key);
            }
        }
    }

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

    private async Task RecoverPage()
    {
        _images = 10;
        _skip = 0;
        _showLandsat8 = true;
        _showLandsat9 = true;
        _minCloudCover = 0;
        _maxCloudCover = 1;
        _cloudCoverFilter = CloudCoverFilter.None;

        Snackbar.Add("Reset image search parameters.", Severity.Info);
        
        var sceneDatas = await TryGetSceneData();
        
        _loadingMsg = "Caching Images ...";
        StateHasChanged();
        
        _sceneDataToImgStrMap = await GetSceneToImgStrMap(sceneDatas);
        
        _isLoading = false;
        StateHasChanged();
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
            var minCcInt = (int)Math.Floor(_minCloudCover * 100);
            var maxCcInt = (int)Math.Ceiling(_maxCloudCover * 100);
            sceneDatas = await ApiTargetService.TryGetSceneData(_target.Path, _target.Row, _images, _skip, minCcInt, maxCcInt);
            SessionStorageService.SetItem(targetKey, sceneDatas);
        }
        
        return sceneDatas.OrderByDescending(sceneData => sceneData.Metadata.PublishDate).ToList();
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
        var browsePathKeys = sceneDatas.Select(HashBrowsePath).ToHashSet();
        foreach (var key in SessionStorageService.Keys())
        {
            if (!browsePathKeys.Contains(key))
            {
                SessionStorageService.RemoveItem(key);
            }
        }
        
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

    private async Task OpenSettings()
    {
        var onDialogSubmit = async (DetailedViewSettingsModel model) =>
        {
            try
            {
                _isLoading = true;
                _loadingMsg = "Applying settings ...";
                await InvokeAsync(StateHasChanged);
                
                _images = model.Images;
                _skip = model.Skip;
                _showLandsat8 = model.ShowLandsat8;
                _showLandsat9 = model.ShowLandsat9;

                _cloudCoverFilter = model.CloudCoverFilter;

                if (model.CloudCoverFilter is CloudCoverFilter.CustomRange)
                {
                    _minCloudCover = model.MinCloudCover;
                    _maxCloudCover = model.MaxCloudCover;
                }
                else if (model.CloudCoverFilter is CloudCoverFilter.TargetRange && model.TargetCloudCoverFilter is not null)
                {
                    _minCloudCover = model.TargetCloudCoverFilter.Value.Min;
                    _maxCloudCover = model.TargetCloudCoverFilter.Value.Max;
                }

                // Reloading the images
                if (_target is not null)
                {
                    var targetKey = HashTarget(_target);
                    if (SessionStorageService.ContainKey(targetKey))
                    {
                        SessionStorageService.RemoveItem(targetKey);
                    }
                }
                
                _loadingMsg = "Loading Images ...";
                await InvokeAsync(StateHasChanged);
                
                TryInitTarget();
                var sceneDatas = await TryGetSceneData();

                _loadingMsg = "Caching Images ...";
                await InvokeAsync(StateHasChanged);
                
                _sceneDataToImgStrMap = await GetSceneToImgStrMap(sceneDatas);
                
                _isLoading = false;
                await InvokeAsync(StateHasChanged);

                if (!Environment.IsProduction())
                {
                    Console.WriteLine($"{_minCloudCover} - {_maxCloudCover}");
                }
            }
            catch (Exception exception)
            {
                await OnUnhandledError.InvokeAsync((exception, RecoverPage));
            }
        };

        var targetCloudCoverFilter = _target is not null
            ? (_target.MinCloudCoverFilter, _target.MaxCloudCoverFilter)
            : (_minCloudCover, _maxCloudCover);
        
        var parameters = new DialogParameters<DetailedViewSettingsDialog>
        {
            { x => x.Model, new DetailedViewSettingsModel
            {
                Images = _images,
                Skip = _skip,
                ShowLandsat8 = _showLandsat8,
                ShowLandsat9 = _showLandsat9,
                MinCloudCover = _minCloudCover,
                MaxCloudCover = _maxCloudCover,
                CloudCoverFilter = _cloudCoverFilter,
                TargetCloudCoverFilter = targetCloudCoverFilter
            } },
            { x => x.OnDialogSubmit, EventCallback.Factory.Create(this, onDialogSubmit) }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            
            CloseOnEscapeKey = true,
            BackdropClick = false,
        };
        
        await DialogService.ShowAsync<DetailedViewSettingsDialog>(null, parameters, options);
    }

    private void HandleKeyPress(KeyboardEventArgs args)
    {
        if (!Environment.IsProduction())
        {
            Console.WriteLine(args.Code);
            Console.WriteLine(args.Key);
        }
    }
    
    private static string HashBrowsePath(SceneData sceneData) => $"browse-path:{sceneData.Metadata.EntityId}";
    
    private static string HashTarget(Target target) => $"target-details:{target.Id};{target.Latitude};{target.Longitude}";
}