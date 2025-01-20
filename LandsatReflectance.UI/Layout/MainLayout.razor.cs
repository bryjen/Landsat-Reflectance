using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using MudBlazor;

using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Exceptions;
using LandsatReflectance.UI.Components.Dialog;


namespace LandsatReflectance.UI.Layout;


public partial class MainLayout : LayoutComponentBase
{
    [Inject]
    public required ILogger<MainLayout> Logger { get; set; }
    
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required IDialogService DialogService { get; set; }
    
    [Inject]
    public required ISnackbar Snackbar { get; set; }
    
    [Inject]
    public required UiService UiService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }
    
    [Inject]
    public required CurrentTargetsService CurrentTargetsService { get; set; }
    
    [Inject]
    public required Wrs2AreasService Wrs2AreasService { get; set; }

    // Flag to prevent the dialog from being displayed multiple times on top of each other.
    // Unknown what causes this.
    private bool _wasSaveUnregisteredTargetsDialogDisplayed;
    
    private FullPageLoadingOverlay _fullPageLoadingOverlay = new();
    
    
    protected override async Task OnInitializedAsync()
    {
        if (!CurrentUserService.IsAuthenticated)
        {
            try
            { 
                await CurrentUserService.TryInitFromLocalValues();
                StateHasChanged();
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
    
    protected override void OnParametersSet()
    {
        CurrentUserService.OnUserAuthenticated += PromptToSaveUnregisteredTargets;
    }
    
    protected void Dispose()
    {
        #nullable disable
        CurrentUserService.OnUserAuthenticated -= PromptToSaveUnregisteredTargets;
        #nullable enable
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
    
    
    private async void PromptToSaveUnregisteredTargets(object? sender, EventArgs args)
    {
        if (CurrentTargetsService.UnregisteredTargets.Count == 0)
        {
            return;
        }
        
        if (!_wasSaveUnregisteredTargetsDialogDisplayed)
        {
            _wasSaveUnregisteredTargetsDialogDisplayed = true;
            
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            await DialogService.ShowAsync<SaveUnregisteredTargetsDialog>();
            StateHasChanged();
        }
    }
}