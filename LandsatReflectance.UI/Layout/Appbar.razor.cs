using LandsatReflectance.UI.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LandsatReflectance.UI.Layout;

public partial class Appbar : ComponentBase
{
    [Parameter]
    public required bool IsDarkMode { get; set; } = true;
    
    [Parameter]
    public required EventCallback DrawerToggle { get; set; }
    
    [Parameter]
    public required EventCallback DarkModeToggle { get; set; }
    
    
    private string DarkLightModeButtonIcon =>
        IsDarkMode switch
        {
            true => Icons.Material.Rounded.AutoMode,
            false => Icons.Material.Outlined.DarkMode,
        };
}