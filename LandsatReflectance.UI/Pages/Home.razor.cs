using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using LandsatReflectance.UI.Utils;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LandsatReflectance.UI.Pages;

public partial class Home : ComponentBase
{
    [Inject]
    public required ISnackbar Snackbar { get; set; }
    
    [Inject]
    public required ApiTargetService ApiTargetService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }


    private Target[] _targets = [];


    protected override async Task OnParametersSetAsync()
    {
        await GetTargets();
    }


    public async Task GetTargets()
    {
        var targetsResult = await ApiTargetService.TryGetUserTargets();
        targetsResult.MatchUnit(
            targets =>
            {
                _targets = targets;
                StateHasChanged();
            },
            errorMsg =>
            {
                Snackbar.Add(errorMsg, Severity.Error);
            });
    }
}    