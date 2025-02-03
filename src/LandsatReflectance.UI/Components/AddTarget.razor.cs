using LandsatReflectance.SceneBoundaries;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using LandsatReflectance.UI.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;

namespace LandsatReflectance.UI.Components;

public partial class AddTarget : ComponentBase
{
    [Inject]
    public required ILogger<AddTarget> Logger { get; set; }
    
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required ISnackbar Snackbar { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
    [Inject]
    public required GeocodingService GeocodingService { get; set; }
    
    [Inject]
    public required ApiTargetService ApiTargetService { get; set; }
    
    [Inject]
    public required Wrs2AreasService Wrs2AreasService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }
    
    [Inject]
    public required CurrentTargetsService CurrentTargetsService { get; set; }
    
    
    [CascadingParameter(Name = "FullPageLoadingOverlay")]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; }
    
    [CascadingParameter(Name = "OnUnhandledError")]
    public required EventCallback<(Exception, Func<Task>?)> OnUnhandledError { get; set; }


    private ForwardGeocodingData? _selectedForwardGeocodingData;
    private ForwardGeocodingData? SelectedForwardGeocodingData
    {
        get => _selectedForwardGeocodingData;
        set
        {
            if (value != _selectedForwardGeocodingData)
            {
                _selectedForwardGeocodingData = value;

                if (value is not null)
                {
                    FetchSceneData(value).ConfigureAwait(false);
                }
            }
        }
    }

    private Dictionary<Wrs2Scene, SceneData?> _scenes = new();
    private Wrs2Scene? _selectedScene;


    private bool _showLandsat8 = true;
    private bool _showLandsat9 = true;
    private double _minCloudCover = 0;
    private double _maxCloudCover = 1;
    private int _notificationOffsetHours = 1;
    
    
    private async Task<IEnumerable<ForwardGeocodingData>> SearchForAddresses(string addressStr, CancellationToken cancellationToken)
    {
        return await GeocodingService.GetRelatedAddresses(addressStr, cancellationToken);
    }

    private async Task TryAddTarget()
    {
        try
        {
            if (SelectedForwardGeocodingData is null)
            {
                throw new ArgumentNullException(nameof(SelectedForwardGeocodingData));
            }

            if (_selectedScene is null)
            {
                throw new ArgumentNullException(nameof(_selectedScene));
            }
            
            
            FullPageLoadingOverlay.SetOverlayMessage("Adding Target ...");
            FullPageLoadingOverlay.Show();
            

            if (CurrentUserService.IsAuthenticated)
            {
                var requestBodyDict = new Dictionary<string, object>
                {
                    { "path", _selectedScene.Value.Path },
                    { "row", _selectedScene.Value.Row },
                    { "latitude", SelectedForwardGeocodingData.Latitude },
                    { "longitude", SelectedForwardGeocodingData.Longitude },
                    { "minCloudCoverFilter", _minCloudCover },
                    { "maxCloudCoverFilter", _maxCloudCover },
                    { "notificationOffset", TimeSpan.FromHours(_notificationOffsetHours).ToString(@"hh\:mm\:ss") }
                };

                var target = await ApiTargetService.TryAddTarget(CurrentUserService.AccessToken, requestBodyDict);
                CurrentTargetsService.RegisteredTargets.Add(target);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(Rand.GenerateFormDelayTime())); // artificial delay
                var target = new Target
                {
                    Path = _selectedScene.Value.Path,
                    Row = _selectedScene.Value.Row,
                    Latitude = SelectedForwardGeocodingData.Latitude,
                    Longitude = SelectedForwardGeocodingData.Longitude,
                    MinCloudCoverFilter = _minCloudCover,
                    MaxCloudCoverFilter = _maxCloudCover,
                    NotificationOffset = TimeSpan.FromHours(_notificationOffsetHours)
                };

                CurrentTargetsService.AddUnregisteredTarget(target);
            }
        }
        catch (Exception exception)
        {
            if (!Environment.IsProduction())
            {
                Logger.LogWarning($"An exception was caught while trying to add a target with message: \"{exception.Message}\".");
            }

            await OnUnhandledError.InvokeAsync((exception, () => Task.CompletedTask)); // no recovery function needed; action is in finally block
        }
        finally
        {
            SelectedForwardGeocodingData = null;
            _selectedScene = null;
            _scenes.Clear();
            
            FullPageLoadingOverlay.ClearOverlayMessage();
            FullPageLoadingOverlay.Hide();

            Snackbar.Add("Successfully added target!", Severity.Success);
        }
    }

    private void OnSceneSelected(Wrs2Scene wrs2Scene)
    {
        if (_selectedScene is not null && _selectedScene == wrs2Scene)
        {
            _selectedScene = null;
            return;
        }
        
        _selectedScene = wrs2Scene;
    }
    
    private async Task FetchSceneData(ForwardGeocodingData forwardGeocodingData)
    {
        _scenes.Clear();
        
        if (!Wrs2AreasService.IsInit())
        {
            await Wrs2AreasService.InitIfNull();
        }

        Console.WriteLine("here");
        
        var scenes = await Wrs2AreasService.GetScenes(new LatLong((float)forwardGeocodingData.Latitude, (float)forwardGeocodingData.Longitude));
        _scenes = scenes.ToDictionary(s => s, SceneData? (_) => null);
        scenes.ForEach(FetchSceneData);
        StateHasChanged();
    }

    private async void FetchSceneData(Wrs2Scene wrs2Scene)
    {
        try
        {
            // we intentionally pick an image with low cloud for better idea of the area displayed
            var sceneDatas = await ApiTargetService.TryGetSceneData(wrs2Scene.Path, wrs2Scene.Row, 10, 0, 0, 10);
            var first = sceneDatas.FirstOrDefault();

            if (first is null)
            {
                return;
            }

            if (_scenes.ContainsKey(wrs2Scene))
            {
                _scenes[wrs2Scene] = first;
                StateHasChanged();
            }
        }
        catch (Exception exception)
        {
            // ignored, for now
        }
    }
}