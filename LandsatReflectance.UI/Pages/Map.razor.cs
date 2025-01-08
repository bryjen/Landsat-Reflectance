using System.Diagnostics;
using System.Text.Json;
using GoogleMapsComponents;
using GoogleMapsComponents.Maps;
using LandsatReflectance.SceneBoundaries;
using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using LandsatReflectance.UI.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor;
using MouseEvent = GoogleMapsComponents.Maps.MouseEvent;
using Polygon = GoogleMapsComponents.Maps.Polygon;

namespace LandsatReflectance.UI.Pages;

class TargetCreationInfo : IAsyncDisposable
{
    public LatLong Coordinates { get; set; }
    // public List<Wrs2Scene> Scenes { get; set; } = new();
    public (int Path, int Row)? SelectedPathRow { get; set; }

    internal Marker? AssociatedMarker { get; init; } = null;
    // internal List<Polygon> AssociatedMapPolygons { get; init; } = new();

    internal Dictionary<Wrs2Scene, Polygon> ScenePolygonMap { get; init; } = new();

    
    public async ValueTask DisposeAsync()
    {
        if (AssociatedMarker is not null)
        {
            await AssociatedMarker.SetMap(null);
        }
        
        foreach (var polygon in ScenePolygonMap.Values)
        {
            await polygon.SetMap(null);
        }
    }
}

public partial class Map : ComponentBase
{
    [Inject]
    public required ILogger<Map> Logger { get; set; } 
    
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; } 
    
    [Inject]
    public required IJSRuntime JsRuntime { get; set; } 
    
    [Inject]
    public required ISnackbar Snackbar { get; set; } 
    
    [Inject]
    public required Wrs2AreasService Wrs2AreasService { get; set; } 
    
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; } 
    
    [Inject]
    public required CurrentTargetsService CurrentTargetsService { get; set; } 
    
    [Inject]
    public required ApiTargetService ApiTargetService { get; set; } 
    
    
    [CascadingParameter]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; } 
    
    
    private GoogleMap m_googleMap = null!;
    private MapOptions m_mapOptions = null!;

    private TargetCreationInfo? _targetCreationInfo = null;

    private const string ParentDivHeight = "height: calc(100vh - (var(--mud-appbar-height) - var(--mud-appbar-height) / 4))";
    private const string RegionColor = "#000000";
    private const string SelectedRegionColor = "#FF0000";

    
    protected override void OnInitialized()
    {
        m_mapOptions = new MapOptions
        {
            Zoom = 2,
            Center = new LatLngLiteral
            {
                Lat = 0,
                Lng = 0 
            },
            MapTypeId = MapTypeId.Roadmap,
            DisableDefaultUI = true,
            ZoomControl = true,
        };
    }

    protected override void OnParametersSet()
    {
        CurrentUserService.OnUserAuthenticated += CurrentTargetsService.SaveTargetsCreatedOffline;
        CurrentUserService.OnUserAuthenticated += CurrentTargetsService.LoadUserTargets;
        
        CurrentUserService.OnUserLogout += CurrentTargetsService.OnUserLogout;
    }

    protected override async Task OnAfterRenderAsync(bool isFirstRender)
    {
        if (isFirstRender && !Wrs2AreasService.IsInit())
        {
            FullPageLoadingOverlay.SetOverlayMessage("Initializing map data ...");
            FullPageLoadingOverlay.Show();
            
            _ = await Wrs2AreasService.InitIfNull();
            
            FullPageLoadingOverlay.Hide();
            FullPageLoadingOverlay.ClearOverlayMessage();
        }
        
        
        // Logic to load targets based on cookie auth
        if (isFirstRender && CurrentUserService.IsAuthenticated && !CurrentTargetsService.HasLoadedUserTargets)
        {
            await CurrentTargetsService.LoadUserTargetsCore(CurrentUserService.AccessToken);
            await RenderTargetsAsMarkersOnMap();
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

    
    private async Task OnAfterMapRender()
    {
        await m_googleMap.InteropObject.AddListener<MouseEvent>("click", mouseEvents => { _ = OnMapClick(mouseEvents); });
        await RenderTargetsAsMarkersOnMap();
    }

    
    private async Task OnMapClick(MouseEvent e)
    {
        var coordinates = new LatLong((float)e.LatLng.Lat, (float)e.LatLng.Lng);
        var scenes = await Wrs2AreasService.GetScenes(coordinates);

        var scenePolygonMap = new Dictionary<Wrs2Scene, Polygon>();
        foreach (var scene in scenes)
        {
            var polygon = await DrawSceneOnMap(scene);
            scenePolygonMap.TryAdd(scene, polygon);
        }
        
        var markerOptions = new MarkerOptions
        {
            Position = e.LatLng,
            Map = m_googleMap.InteropObject,
            // Title = "something",
            // Label = $"Marker @ {e.LatLng.Lat:F}, {e.LatLng.Lng:F}",
            Draggable = false,
            Icon = new Icon
            {
                Url = "http://maps.google.com/mapfiles/ms/icons/blue-dot.png"
            }
        };

        var newMarker = await Marker.CreateAsync(m_googleMap.JsRuntime, markerOptions);
        await newMarker.AddListener<MouseEvent>("click", async mouseEvent =>
        {
            await mouseEvent.Stop();
        });
        
        if (!Environment.IsProduction())
        {
            Logger.LogInformation($"[Map] {e.LatLng.Lat:F}, {e.LatLng.Lng:F}, Found {scenes.Count} areas.");
            Logger.LogInformation(string.Join("\n", scenes.Select((tuple, i) => $"{i}. {tuple.Path}, {tuple.Row}")));
        }


        // UI update stuff
        await ClearTargetCreationInfo();
        
        _targetCreationInfo = new()
        {
            Coordinates = coordinates,
            // Scenes = scenes,
            
            AssociatedMarker = newMarker,
            // AssociatedMapPolygons = mapPolygons,
            
            ScenePolygonMap = scenePolygonMap
            
        };
        StateHasChanged();  // re-render to display selection pop-up
    }

    private async Task<Polygon> DrawSceneOnMap(Wrs2Scene wrs2Scene)
    {
        var polygon = await Polygon.CreateAsync(m_googleMap.JsRuntime, CreatePolygonOptions(RegionColor));
        var latLongList = new List<LatLong>
        {
            wrs2Scene.LowerLeft,
            wrs2Scene.LowerRight,
            wrs2Scene.UpperRight,
            wrs2Scene.UpperLeft
        };
        var asLatLngLiterals = latLongList.Select(ToMapLatlngLiteral).ToList();
        await polygon.SetPath(asLatLngLiterals);
        
        return polygon;
    }

    private async Task OnSceneSelectedInMenu(int path, int row)
    {
        if (_targetCreationInfo is null)
        {
            return;
        }

        if (_targetCreationInfo.SelectedPathRow.HasValue
            && _targetCreationInfo.SelectedPathRow.Value.Path == path
            && _targetCreationInfo.SelectedPathRow.Value.Row == row)
        {
            _targetCreationInfo.SelectedPathRow = null;  // deselect behavior
        }
        else
        {
            _targetCreationInfo.SelectedPathRow = (path, row);
        }

        await ChangeSelectedPolygonColor();
    }

    private async Task ChangeSelectedPolygonColor()
    {
        if (_targetCreationInfo is not null)
        {
            if (_targetCreationInfo.SelectedPathRow is not null)
            {
                var (path, row) = _targetCreationInfo.SelectedPathRow.Value;
                foreach (var (scene, polygon) in _targetCreationInfo.ScenePolygonMap)
                {
                    if (scene.Path == path && scene.Row == row)
                    {
                        await polygon.SetOptions(CreatePolygonOptions(SelectedRegionColor));
                    }
                    else
                    {
                        await polygon.SetOptions(CreatePolygonOptions(RegionColor));
                    }
                }
            }
            else
            {
                // on de-selection, this executes
                foreach (var (_, polygon) in _targetCreationInfo.ScenePolygonMap)
                {
                    await polygon.SetOptions(CreatePolygonOptions(RegionColor));
                }
            }
        }
    }

    private async Task ClearTargetCreationInfo()
    {
        if (_targetCreationInfo is not null)
        {
            await _targetCreationInfo.DisposeAsync();
        }

        _targetCreationInfo = null;
    }

    private async Task AddTarget(LatLong latLong, int path, int row)
    {
        try
        {
            FullPageLoadingOverlay.SetOverlayMessage("Adding target ...");
            FullPageLoadingOverlay.Show();
            
            if (CurrentUserService.IsAuthenticated)
            {
                var requestBodyDict = new Dictionary<string, object>
                {
                    { "path", path },
                    { "row", row },
                    { "latitude", latLong.Latitude },
                    { "longitude", latLong.Longitude },
                    { "minCloudCoverFilter", 0d },
                    { "maxCloudCoverFilter", 1d },
                    { "notificationOffset", "01:00:00" }
                };

                if (!Environment.IsProduction())
                {
                    Logger.LogInformation(JsonSerializer.Serialize(requestBodyDict, new JsonSerializerOptions { WriteIndented = true }));
                }
                
                var target = await ApiTargetService.TryAddTarget(CurrentUserService.AccessToken, requestBodyDict);
                CurrentTargetsService.RegisteredTargets.Add(target);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(Rand.GenerateFormDelayTime()));  // artificial delay
                var target = new Target
                {
                    Path = path,
                    Row = row,
                    Latitude = latLong.Latitude,
                    Longitude = latLong.Longitude,
                    MinCloudCoverFilter = 0,
                    MaxCloudCoverFilter = 1,
                    NotificationOffset = TimeSpan.FromHours(1)
                };
                
                CurrentTargetsService.UnregisteredTargets.Add(target);
            }

            
            FullPageLoadingOverlay.Hide();
            FullPageLoadingOverlay.ClearOverlayMessage();
            
            await ClearTargetCreationInfo();
            await RenderTargetsAsMarkersOnMap();
        }
        catch (Exception)
        {
            // ignored, for now
        }
    }

    private async Task RenderTargetsAsMarkersOnMap()
    {
        foreach (var target in CurrentTargetsService.AllTargets)
        {
            var markerOptions = new MarkerOptions
            {
                Position = new LatLngLiteral(target.Latitude, target.Longitude),
                Map = m_googleMap.InteropObject,
                Draggable = false,
                Icon = new Icon
                {
                    Url = "http://maps.google.com/mapfiles/ms/icons/blue-dot.png"
                }
            };

            var _ = await Marker.CreateAsync(m_googleMap.JsRuntime, markerOptions);
            /*
            await newMarker.AddListener<MouseEvent>("click", async mouseEvent =>
            {
                await mouseEvent.Stop();
            });
             */
        }
        
        await RefreshGoogleMaps();
    }


    private async Task RefreshGoogleMaps()
    {
        // Changing a property should 'refresh' and re-render the map
        // The below shouldn't change nothin
        await m_googleMap.InteropObject.SetZoom(await m_googleMap.InteropObject.GetZoom());
    }
    
    private PolygonOptions CreatePolygonOptions(string colorString) => new()
    {
        Map = m_googleMap.InteropObject,
        StrokeColor = colorString,
        StrokeOpacity = 0.25f,
        FillColor = colorString,
        FillOpacity = 0.25f,
    };
    
    // Google map api uses its own type. Converts our type to that.
    private static LatLngLiteral ToMapLatlngLiteral(LatLong latLong) => 
        new(latLong.Latitude, latLong.Longitude);
}