using System.Collections.Concurrent;
using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;

namespace LandsatReflectance.UI.Pages;

public partial class Home : ComponentBase
{
    [Inject]
    public required ILogger<Home> Logger { get; set; }
    
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
    [Inject]
    public required ISnackbar Snackbar { get; set; }
    
    [Inject]
    public required ApiTargetService ApiTargetService { get; set; }
    
    [Inject]
    public required CurrentTargetsService CurrentTargetsService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }
    
    
    [CascadingParameter]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; }


    private readonly ConcurrentDictionary<Target, SceneData?> _targetSceneDataMap = new();

    
    
    protected override void OnParametersSet()
    {
        CurrentUserService.OnUserAuthenticated += CurrentTargetsService.SaveTargetsCreatedOffline;
        CurrentUserService.OnUserAuthenticated += CurrentTargetsService.LoadUserTargets;
        
        CurrentUserService.OnUserLogout += CurrentTargetsService.OnUserLogout;
        
        CurrentTargetsService.Targets.CollectionChanged += OnDataChanged;
        CurrentTargetsService.OnIsLoadingTargetsChanged += OnDataChanged;
    }

    protected override async Task OnAfterRenderAsync(bool isFirstRender)
    {
        // Logic to load targets based on cookie auth
        if (isFirstRender && CurrentUserService.IsAuthenticated && !CurrentTargetsService.HasLoadedUserTargets)
        {
            await CurrentTargetsService.LoadUserTargetsCore(CurrentUserService.Token);
        }
        else if (isFirstRender && CurrentUserService.IsAuthenticated && CurrentTargetsService.HasLoadedUserTargets)
        {
            ReInitDictionary();
        }
    }

    protected void Dispose()
    {
        #nullable disable
        CurrentUserService.OnUserAuthenticated -= CurrentTargetsService.SaveTargetsCreatedOffline;
        CurrentUserService.OnUserAuthenticated -= CurrentTargetsService.LoadUserTargets;
        
        CurrentUserService.OnUserLogout -= CurrentTargetsService.OnUserLogout;
        
        CurrentTargetsService.Targets.CollectionChanged -= OnDataChanged;
        CurrentTargetsService.OnIsLoadingTargetsChanged -= OnDataChanged;
        #nullable enable
    }

    private void ReInitDictionary()
    {
        _targetSceneDataMap.Clear();
        foreach (var target in CurrentTargetsService.Targets)
        {
            if (!_targetSceneDataMap.ContainsKey(target))
            {
                _targetSceneDataMap.TryAdd(target, null);
                StateHasChanged();
                
                TryGetSceneDataFromTarget(target, retryOnFail: true);
            }
        }
    }
    
    private void OnDataChanged(object? sender, EventArgs _)
    {
        ReInitDictionary();
        InvokeAsync(StateHasChanged);
    }

    private async void TryGetSceneDataFromTarget(Target target, bool retryOnFail = false)
    {
        try
        {
            var sceneDataArr = await ApiTargetService.TryGetSceneData(
                CurrentUserService.Token, 
                target.Path,
                target.Row, 
                2);
            
            var sceneData = sceneDataArr.FirstOrDefault();
            if (!_targetSceneDataMap.TryAdd(target, sceneData))
            {
                _targetSceneDataMap[target] = sceneData;
            }
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception exception)
        {
            if (retryOnFail)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));  // Have some delay between retry attempts
                TryGetSceneDataFromTarget(target, retryOnFail: false);
            }
            else
            {
                if (!Environment.IsProduction())
                {
                    Logger.LogError(exception.Message);
                }
                
                Logger.LogError(exception.ToString());
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task TryDeleteTarget(Target target)
    {
        // Direct call to api service cause I don't feel like having a two layer call
        try
        {
            FullPageLoadingOverlay.SetOverlayMessage("Deleting target...");
            FullPageLoadingOverlay.Show();

            var deletedTarget = CurrentUserService.IsAuthenticated 
                ? await ApiTargetService.TryDeleteTarget(CurrentUserService.Token, target)
                : target;

            var wasDeleted = CurrentTargetsService.Targets.Remove(deletedTarget);
            if (!Environment.IsProduction())
            {
                Logger.LogInformation($"Try delete \"{target.Id}\" from memory: {wasDeleted}");
            }

            Snackbar.Add("Successfully deleted target", Severity.Success);
        }
        catch (Exception exception)
        {
            if (!Environment.IsProduction())
            {
                Logger.LogError(exception.ToString());
                Snackbar.Add(exception.Message, Severity.Error);
            }
            else
            {
                Snackbar.Add("An unexpected error occurred.", Severity.Error);
            }
        }
        finally
        {
            FullPageLoadingOverlay.ClearOverlayMessage();
            FullPageLoadingOverlay.Hide();
        }
    }
}    