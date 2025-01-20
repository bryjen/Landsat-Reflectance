using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;

namespace LandsatReflectance.UI.Components.Dialog;

public partial class SaveUnregisteredTargetsDialog : ComponentBase
{
    [Inject] 
    public required ILogger<SaveUnregisteredTargetsDialog> Logger { get; set; }
    
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; } 
    
    [Inject]
    public required ISnackbar Snackbar { get; set; } 
    
    [Inject] 
    public required ApiTargetService ApiTargetService { get; set; }
    
    [Inject] 
    public required CurrentUserService CurrentUserService { get; set; }
    
    [Inject] 
    public required CurrentTargetsService CurrentTargetsService { get; set; }
    
    
    [CascadingParameter] 
    private MudDialogInstance MudDialog { get; set; } = null!;


    private bool _overlayVisible;
    

    private async Task SaveUnregisteredTargets()
    {
        var tasks = new List<Task<Target?>>();

        _overlayVisible = true;
        StateHasChanged();

        var unregisteredTargetsLen = CurrentTargetsService.UnregisteredTargets.Count;
        foreach (var target in CurrentTargetsService.UnregisteredTargets)
        {
            var addTargetTask = Task.Run(async () =>
            {
                try
                {
                    var requestBodyDict = new Dictionary<string, object>
                    {
                        { "path", target.Path },
                        { "row", target.Row },
                        { "latitude", target.Latitude },
                        { "longitude", target.Longitude },
                        { "minCloudCoverFilter", target.MinCloudCoverFilter },
                        { "maxCloudCoverFilter", target.MaxCloudCoverFilter },
                        { "notificationOffset", target.NotificationOffset }
                    };

                    return await ApiTargetService.TryAddTarget(CurrentUserService.AccessToken, requestBodyDict);
                }
                catch (Exception exception)
                {
                    if (!Environment.IsProduction())
                    {
                        Logger.LogError($"Failed to save target with message: \"{exception.Message}\".");
                    }
                    
                    return null;
                }
            });

            tasks.Add(addTargetTask);
        }

        var nullableTargets = await Task.WhenAll(tasks);
        List<Target> savedTargets = nullableTargets
            .Where(target => target is not null)
            .ToList()!;

        foreach (var target in savedTargets)
        {
            CurrentTargetsService.RegisteredTargets.Add(target);
        }
        
        CurrentTargetsService.ClearAllUnregisteredTargets();


        _overlayVisible = false;
        StateHasChanged();
        
        MudDialog.Close();
        Snackbar.Add($"Successfully saved {savedTargets.Count} target(s).",
            savedTargets.Count == unregisteredTargetsLen ? Severity.Success : Severity.Warning);
    }
}