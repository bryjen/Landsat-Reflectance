﻿using LandsatReflectance.SceneBoundaries;
using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Exceptions;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;

namespace LandsatReflectance.UI.Layout;

public partial class MainLayout : LayoutComponentBase
{
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required ISnackbar Snackbar { get; set; }
    
    [Inject]
    public required UiService UiService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }
    
    [Inject]
    public required Wrs2AreasService Wrs2AreasService { get; set; }
    
    
    private FullPageLoadingOverlay m_fullPageLoadingOverlay = new();
    
    

    protected override void OnInitialized()
    {
        if (!CurrentUserService.IsAuthenticated)
        {
            try
            { 
                CurrentUserService.TryInitFromLocalStorage();
            }
            catch (AuthException authException)
            {
                if (!Environment.IsProduction())
                {
                    Snackbar.Add(authException.Message, Severity.Error);
                }
                else
                {
                    _ = AuthException.GenericLoginErrorMessage;
                    // TODO: Make popup for this thing
                }
            }
            catch (Exception exception)
            {
                _ = AuthException.GenericLoginErrorMessage;
                // TODO: Make popup for this thing
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool isFirstRender)
    {
        // await Wrs2AreasService.GetScenes(new LatLong(45.50371351218764f, -73.56731958677688f));
        
        /*
        if (Wrs2AreasService.IsInitialized())
        {
            var workFunc = async () => await Wrs2AreasService.InitWrs2Areas();
            var onWorkFinishedCallback = () => Task.CompletedTask;
            await m_fullPageLoadingOverlay.ExecuteWithOverlay("Loading landsat area data.\nPlease wait ...", workFunc, onWorkFinishedCallback);
        }
         */
    }

    public string DarkLightModeButtonIcon =>
        UiService.IsDarkMode switch
        {
            true => Icons.Material.Rounded.AutoMode,
            false => Icons.Material.Outlined.DarkMode,
        };


    public void DrawerToggle()
    {
        UiService.IsDrawerExpanded = !UiService.IsDrawerExpanded;
    }

    public void DarkModeToggle()
    {
        UiService.IsDarkMode = !UiService.IsDarkMode;
    }
}