using System.Collections.Concurrent;
using System.Collections.Specialized;
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
    public required ILogger<Home> Logger { get; set; }
    
    [Inject]
    public required ISnackbar Snackbar { get; set; }
    
    [Inject]
    public required ApiTargetService ApiTargetService { get; set; }
    
    [Inject]
    public required CurrentTargetsService CurrentTargetsService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }


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

    public void Dispose()
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
            var sceneDataResults = await ApiTargetService.TryGetSceneData(
                CurrentUserService.Token, 
                target.Path,
                target.Row, 
                2);
            sceneDataResults.MatchUnit(
                sceneDatas =>
                {
                    var sceneData = sceneDatas.FirstOrDefault();
                    if (!_targetSceneDataMap.TryAdd(target, sceneData))
                    {
                        _targetSceneDataMap[target] = sceneData;
                    }
                    InvokeAsync(StateHasChanged);
                },
                errorMsg =>
                {
                    Logger.LogError(errorMsg);
                    InvokeAsync(StateHasChanged);
                });
        }
        catch (OperationCanceledException _)
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
                Logger.LogError(exception.ToString());
                await InvokeAsync(StateHasChanged);
            }
        }
    }
}    