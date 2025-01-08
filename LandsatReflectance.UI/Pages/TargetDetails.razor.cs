using LandsatReflectance.SceneBoundaries;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace LandsatReflectance.UI.Pages;

public partial class TargetDetails : ComponentBase
{
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
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

    private bool _isLoading = true;
    private bool _showQuickSummary;
    private bool _showImageLoadingDelayWarning = true;
    private bool _isLoadingLocation;
    
    private Target? _target;
    private string? _errorMsg;

    private int _currentSceneIndex = 0;
    private List<SceneData> _sceneDatas = new();
        private LocationData? _locationData = null;

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
            await TryGetSceneData();
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
        if (TargetId is null)
        {
            _errorMsg = "A \"target-id\" is not provided.";
            return;
        }
        
        if (!Guid.TryParse(TargetId, out Guid asGuid))
        {
            _errorMsg = $"There was an error parsing the string \"{TargetId}\" into a GUID.";
            return;
        }
        
        _target = CurrentTargetsService.RegisteredTargets.FirstOrDefault(target => target.Id == asGuid);
        if (_target is null)
        {
            _errorMsg = $"Could not find a target with the id \"{asGuid}\".";
        }
    }

    private async Task TryGetSceneData()
    {
        if (_target is null)
        {
            return;
        }

        if (!CurrentUserService.IsAuthenticated)
        {
            _errorMsg = "You need to be authenticated.";
            return;
        }

        var sceneDatas = await ApiTargetService.TryGetSceneData(CurrentUserService.AccessToken, _target.Path, _target.Row, 10);
        _sceneDatas = sceneDatas.OrderByDescending(sceneData => sceneData.PublishDate).ToList();
    }

    private async void TryGetLocation()
    {
        if (_target is not null)
        {
            try
            {
                _isLoadingLocation = true;
                StateHasChanged();
                
                var asLatLong = new LatLong((float)_target.Latitude, (float)_target.Longitude);
                _locationData = await GeocodingService.GetNearestCity(asLatLong);
                
                _isLoadingLocation = false;
                StateHasChanged();
            }
            catch (Exception)
            {
                // TODO: Display some message here
            }
        }
    }
}