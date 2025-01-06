using System.Diagnostics;
using GoogleMapsComponents;
using GoogleMapsComponents.Maps;
using LandsatReflectance.SceneBoundaries;
using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor;
using MouseEvent = GoogleMapsComponents.Maps.MouseEvent;

namespace LandsatReflectance.UI.Pages;

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
    
    
    [CascadingParameter]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; } 
    
    
    private GoogleMap m_googleMap = null!;
    private MapOptions m_mapOptions = null!;

    private const string ParentDivHeight = "height: calc(100vh - (var(--mud-appbar-height) - var(--mud-appbar-height) / 4))";

    
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
    }

    
    private async Task OnAfterMapRender()
    {
        if (Environment.IsDevelopment())
        {
            Snackbar.Add("Map loaded", Severity.Info);
        }
        
        await m_googleMap.InteropObject.AddListener<MouseEvent>("click", mouseEvents => { _ = OnClick(mouseEvents); });
    }

    
    private async Task OnClick(MouseEvent e)
    {
        Snackbar.Add($"Left clicked: {e.LatLng.Lat:F}, {e.LatLng.Lng:F}", Severity.Info);
        var scenes = await Wrs2AreasService.GetScenes(new LatLong((float) e.LatLng.Lat, (float) e.LatLng.Lng));
        
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
            Snackbar.Add($"Left clicked: {e.LatLng.Lat:F}, {e.LatLng.Lng:F}", Severity.Info);
            
            Logger.LogInformation($"[Map] {e.LatLng.Lat:F}, {e.LatLng.Lng:F}, Found {scenes.Count} areas.");
            Logger.LogInformation(string.Join("\n", scenes.Select((tuple, i) => $"{i}. {tuple.Path}, {tuple.Row}")));
        }
    }
}