using System.Collections.ObjectModel;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services.Api;
using LandsatReflectance.UI.Utils;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
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
    private readonly IWebAssemblyHostEnvironment _environment;
    private readonly ApiTargetService _apiTargetService;
    

    public CurrentTargetsService(
        ILogger<CurrentUserService> logger,
        IWebAssemblyHostEnvironment webAssemblyHostEnvironment,
        ApiTargetService apiTargetService)
    {
        _logger = logger;
        _environment = webAssemblyHostEnvironment;
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
        try
        {
            if (!_environment.IsProduction())
            {
                _logger.LogInformation("Loading User targets ...");
            }

            IsLoadingTargets = true;

            var targets = await _apiTargetService.TryGetUserTargets(authToken);
            
            if (!_environment.IsProduction())
            {
                _logger.LogInformation($"Loaded {targets.Length} targets");
            }

            if (!HasLoadedUserTargets)  // Prevents double adding in case of a 'mistaken' double call
            {
                foreach (var target in targets)
                {
                    Targets.Add(target);
                }

                HasLoadedUserTargets = true;
            }
        }
        finally
        {
            IsLoadingTargets = false;
        }
    }


    internal void OnUserLogout(object? sender, EventArgs eventArgs)
    {
        Targets.Clear();
        HasLoadedUserTargets = false;
    }
}