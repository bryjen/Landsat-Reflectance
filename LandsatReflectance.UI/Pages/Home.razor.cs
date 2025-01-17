using System.Collections.Concurrent;
using Blazored.SessionStorage;
using LandsatReflectance.SceneBoundaries;
using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;

namespace LandsatReflectance.UI.Pages;

class AdditionalTargetInformation
{
    public bool LoadedSceneData { get; set; }
    public SceneData? SceneData { get; set; }
    
    public bool LoadedLocationData { get; set; }
    public LocationData? LocationData { get; set; }
}

public partial class Home : ComponentBase
{
    [Inject]
    public required ILogger<Home> Logger { get; set; }
    
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
    [Inject]
    public required ISnackbar Snackbar { get; set; }
    
    [Inject]
    public required ISyncSessionStorageService SessionStorage { get; set; }
    
    [Inject]
    public required ApiTargetService ApiTargetService { get; set; }
    
    [Inject]
    public required CurrentTargetsService CurrentTargetsService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }
    
    [Inject]
    public required GeocodingService GeocodingService { get; set; }
    
    
    [CascadingParameter]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; }


    // private readonly ConcurrentDictionary<Target, SceneData?> _targetSceneDataMap = new();
    private readonly ConcurrentDictionary<Target, AdditionalTargetInformation> _targetSceneDataMap = new();

    
    
    protected override void OnParametersSet()
    {
        CurrentUserService.OnUserAuthenticated += CurrentTargetsService.SaveTargetsCreatedOffline;
        CurrentUserService.OnUserAuthenticated += CurrentTargetsService.LoadUserTargets;
        
        CurrentUserService.OnUserLogout += CurrentTargetsService.OnUserLogout;
        
        CurrentTargetsService.RegisteredTargets.CollectionChanged += OnDataChanged;
        CurrentTargetsService.UnregisteredTargets.CollectionChanged += OnDataChanged;
        CurrentTargetsService.OnIsLoadingTargetsChanged += OnDataChanged;
    }

    protected override async Task OnAfterRenderAsync(bool isFirstRender)
    {
        // Logic to load targets based on cookie auth
        if (isFirstRender && CurrentUserService.IsAuthenticated && !CurrentTargetsService.HasLoadedUserTargets)
        {
            await CurrentTargetsService.LoadUserTargetsCore(CurrentUserService.AccessToken);
        }
        else if (isFirstRender && CurrentUserService.IsAuthenticated && CurrentTargetsService.HasLoadedUserTargets)
        {
            ReInitDictionary();
        }
        // duplicate conditions because I forgot what scenario the above if statement represents, we leave it like that
        // this is whenever the user isn't authenticated and has unregistered targets
        else if (isFirstRender && !CurrentUserService.IsAuthenticated)
        {
            ReInitDictionary();
        }
    }

    protected void Dispose()
    {
        #nullable disable
        CurrentUserService.OnUserAuthenticated -= CurrentTargetsService.SaveTargetsCreatedOffline;
        CurrentUserService.OnUserAuthenticated -= CurrentTargetsService.LoadUserTargets;
        
        CurrentUserService.OnUserLogout -= CurrentTargetsService.OnUserLogout;
        
        CurrentTargetsService.RegisteredTargets.CollectionChanged -= OnDataChanged;
        CurrentTargetsService.UnregisteredTargets.CollectionChanged -= OnDataChanged;
        CurrentTargetsService.OnIsLoadingTargetsChanged -= OnDataChanged;
        #nullable enable
    }

    private void ReInitDictionary()
    {
        _targetSceneDataMap.Clear();
        foreach (var target in CurrentTargetsService.AllTargets)
        {
            _targetSceneDataMap.TryAdd(target, new AdditionalTargetInformation());
            
            TryGetSceneDataFromTarget(target, retryOnFail: true);
            TryGetLocationDataFromTarget(target);
        }
        
        StateHasChanged();
    }
    
    private void OnDataChanged(object? sender, EventArgs _)
    {
        ReInitDictionary();
        InvokeAsync(StateHasChanged);
    }
    
    
// ReSharper disable AsyncVoidMethod
    private async void TryGetLocationDataFromTarget(Target target)
// ReSharper restore AsyncVoidMethod
    {
        var addLocationDataIfApplicable = async (LocationData locationData) =>
        {
            if (!_targetSceneDataMap.TryGetValue(target, out var additionalTargetInformation))
            {
                return;
            }

            additionalTargetInformation.LocationData = locationData;
            additionalTargetInformation.LoadedLocationData = true;
            await InvokeAsync(StateHasChanged);
        };
        
        var targetKey = $"{target.Id.ToString()};{FormatCoordinates(target.Latitude, target.Longitude)};LocationData";
        if (SessionStorage.ContainKey(targetKey))
        {
            var locationData = SessionStorage.GetItem<LocationData>(targetKey);
            await addLocationDataIfApplicable(locationData);
            return;
        }
        
        try
        {
            var coordinates = new LatLong((float) target.Latitude, (float) target.Longitude);
            var locationData = await GeocodingService.GetNearestCity(coordinates);
            
            if (_targetSceneDataMap.TryGetValue(target, out var additionalTargetInformation))
            {
                additionalTargetInformation.LoadedLocationData = true;
            }
            
            SessionStorage.SetItem(targetKey, locationData);
            await addLocationDataIfApplicable(locationData);
        }
        catch (OperationCanceledException)
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception exception)
        {
            if (!Environment.IsProduction())
            {
                Logger.LogError(exception.Message);
            }
            
            Logger.LogError(exception.ToString());
            await InvokeAsync(StateHasChanged);
        }
    }

    
// ReSharper disable AsyncVoidMethod
    private async void TryGetSceneDataFromTarget(Target target, bool retryOnFail = false)
// ReSharper restore AsyncVoidMethod
    {
        var addSceneDataIfApplicable = async (SceneData sceneData) =>
        {
            if (!_targetSceneDataMap.TryGetValue(target, out var additionalTargetInformation))
            {
                return;
            }

            additionalTargetInformation.SceneData = sceneData;
            additionalTargetInformation.LoadedSceneData = true;
            await InvokeAsync(StateHasChanged);
        };
        
        
        var targetKey = $"{target.Id.ToString()};{FormatCoordinates(target.Latitude, target.Longitude)};SceneData";
        if (SessionStorage.ContainKey(targetKey))
        {
            var sceneData = SessionStorage.GetItem<SceneData>(targetKey);
            await addSceneDataIfApplicable(sceneData);
            return;
        }
        
        try
        {
            var sceneDataArr = await ApiTargetService.TryGetSceneData(
                CurrentUserService.AccessToken, 
                target.Path,
                target.Row, 
                2);
            
            var sceneData = sceneDataArr.FirstOrDefault();
            
            if (_targetSceneDataMap.TryGetValue(target, out var additionalTargetInformation))
            {
                additionalTargetInformation.LoadedSceneData = true;
            }
            
            if (sceneData is not null)
            {
                SessionStorage.SetItem(targetKey, sceneData);
                await addSceneDataIfApplicable(sceneData);
            }
        }
        catch (OperationCanceledException)
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception exception)
        {
            if (!_targetSceneDataMap.ContainsKey(target))
            {
                // if the entry isn't there anymore (deleted, for example), then theres no point in trying to get the data
                return;
            }
            
            if (retryOnFail)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));  // Have some delay between retry attempts
                TryGetSceneDataFromTarget(target, retryOnFail: false);
            }
            else
            {
                if (!Environment.IsProduction())
                {
                    Logger.LogError(exception.Message);
                }
                
                Logger.LogError(exception.ToString());
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task TryDeleteTarget(Target target)
    {
        // Direct call to api service cause I don't feel like having a two layer call
        try
        {
            FullPageLoadingOverlay.SetOverlayMessage("Deleting target...");
            FullPageLoadingOverlay.Show();

            var deletedTarget = CurrentUserService.IsAuthenticated 
                ? await ApiTargetService.TryDeleteTarget(CurrentUserService.AccessToken, target)
                : target;

            var wasDeleted = CurrentTargetsService.RegisteredTargets.Remove(deletedTarget);
            if (!Environment.IsProduction())
            {
                Logger.LogInformation($"Try delete \"{target.Id}\" from memory: {wasDeleted}");
                Logger.LogInformation($"\"CurrentTargetsService.Targets.Count\": {CurrentTargetsService.RegisteredTargets.Count}");
            }

            Snackbar.Add("Successfully deleted target", Severity.Success);
        }
        catch (Exception exception)
        {
            if (!Environment.IsProduction())
            {
                Logger.LogError(exception.ToString());
                Snackbar.Add(exception.Message, Severity.Error);
            }
            else
            {
                Snackbar.Add("An unexpected error occurred.", Severity.Error);
            }
        }
        finally
        {
            FullPageLoadingOverlay.ClearOverlayMessage();
            FullPageLoadingOverlay.Hide();
        }
    }

    private void NavigateToDetailedView(Target target)
    {
        NavigationManager.NavigateTo($"TargetDetails?target-id={target.Id}");
    }

    private static string FormatCoordinates(double latitude, double longitude) =>
        $"{latitude:F}N+{longitude:F}W";
}    