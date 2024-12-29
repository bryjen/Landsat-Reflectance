using System.Collections.ObjectModel;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services.Api;
using LandsatReflectance.UI.Utils;
using MudBlazor;

namespace LandsatReflectance.UI.Services;

public class CurrentTargetsService
{
    public ObservableCollection<Target> Targets { get; set; } = new();
    
    public bool HasLoadedUserTargets { get; private set; } = false;

    public EventHandler OnIsLoadingTargetsChanged = (_, _) => { };
    private bool _isLoadingTargets = false;
    public bool IsLoadingTargets
    {
        get => _isLoadingTargets;
        set
        {
            if (value != _isLoadingTargets)
            {
                _isLoadingTargets = value;
                OnIsLoadingTargetsChanged.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private readonly ILogger<CurrentUserService> _logger;
    private readonly ApiTargetService _apiTargetService;
    

    public CurrentTargetsService(
        ILogger<CurrentUserService> logger,
        ApiTargetService apiTargetService)
    {
        _logger = logger;
        _apiTargetService = apiTargetService;
    }

    internal void SaveTargetsCreatedOffline(object? sender, AuthenticatedEventArgs authenticatedEventArgs)
    {
    }

    
    internal async void LoadUserTargets(object? sender, AuthenticatedEventArgs authenticatedEventArgs)
    {
        try
        {
            await LoadUserTargetsCore(authenticatedEventArgs.Token);
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
        }
    }

    internal async Task LoadUserTargetsCore(string authToken)
    {
        _logger.LogInformation("Loading User targets ...");

        IsLoadingTargets = true;
        
        var targetsResult = await _apiTargetService.TryGetUserTargets(authToken);
        targetsResult.MatchUnit(
            targets =>
            {
                _logger.LogInformation($"Loaded {targets.Length} targets");
                
                // Prevents double adding in case of a 'mistaken' double call
                if (!HasLoadedUserTargets)
                {
                    foreach (var target in targets)
                    {
                        Targets.Add(target);
                    }
                    HasLoadedUserTargets = true;
                }
            },
            errorMsg =>
            {
                _logger.LogError(errorMsg);
            });
        
        
        IsLoadingTargets = false;
    }


    internal void OnUserLogout(object? sender, EventArgs eventArgs)
    {
        Targets.Clear();
        HasLoadedUserTargets = false;
    }
}