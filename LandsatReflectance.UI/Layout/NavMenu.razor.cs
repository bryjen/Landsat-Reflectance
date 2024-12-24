using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Utils;
using Microsoft.AspNetCore.Components;

namespace LandsatReflectance.UI.Layout;

public partial class NavMenu : ComponentBase
{
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
    
    [Parameter]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; }
    
    [Parameter]
    public required bool IsDrawerExpanded { get; set; }
    

    private async Task NavigateToLogin()
    {
        await FullPageLoadingOverlay.ExecuteWithOverlay(
            async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(Rand.GeneratePageSwitchDelayTime()));
            }, 
            () =>
            {
                NavigationManager.NavigateTo("/login/");
                return Task.CompletedTask;
            });
    }
    
    private async Task NavigateToRegistration()
    {
        await FullPageLoadingOverlay.ExecuteWithOverlay(
            async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(Rand.GeneratePageSwitchDelayTime()));
            }, 
            () =>
            {
                NavigationManager.NavigateTo("/register/");
                return Task.CompletedTask;
            });
    }
    
    
}