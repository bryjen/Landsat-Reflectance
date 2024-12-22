using LandsatReflectance.UI.Services;
using Microsoft.AspNetCore.Components;

namespace LandsatReflectance.UI.Layout;

public partial class NavMenu : ComponentBase
{
    [Parameter]
    public required bool IsDrawerExpanded { get; set; }
}