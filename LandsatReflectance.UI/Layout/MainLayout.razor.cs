using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using MudBlazor;

using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Exceptions;
using LandsatReflectance.UI.Components.Dialog;
using Microsoft.AspNetCore.Components.Web;


namespace LandsatReflectance.UI.Layout;


public partial class MainLayout : LayoutComponentBase
{
    [Inject]
    public required ILogger<MainLayout> Logger { get; set; }
    
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
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

    private ErrorBoundary _errorBoundary = new();
    
    private MudDialog _mudDialog = new();
    private IDialogReference? _dialogReference;

    private (Exception Exception, Func<Task>? TryRecoverCallback)? _exceptionData;
    
    
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


    private DialogOptions DefaultUnexpectedErrorDialogOptions => new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        BackdropClick = false,
    };

    private DialogOptions GetDialogOptions(Exception? exception)
    {
        if (Environment.IsProduction())
        {
            return DefaultUnexpectedErrorDialogOptions;
        }

        if (exception is not null || _exceptionData is not null)
        {
            return new DialogOptions
            {
                MaxWidth = MaxWidth.Medium,
                FullWidth = true,
                BackdropClick = false
            };
        }

        return DefaultUnexpectedErrorDialogOptions;
    }

    private void DefaultRecovery()
    {
        // Default behavior when no recovery callback is provided is to force load the home page.
        NavigationManager.NavigateTo("/", forceLoad: true);
    }

    private async Task OnTriggerableErrorDialogDismissed()
    {
        if (_dialogReference is not null)
        {
            _dialogReference.Close();
        }

        if (_exceptionData is not null)
        {
            if (_exceptionData.Value.TryRecoverCallback is not null)
            {
                await InvokeAsync(_exceptionData.Value.TryRecoverCallback);
            }
            else
            {
                DefaultRecovery();
            }
            
            _exceptionData = null;
            StateHasChanged();
        }
    }
    
    private async Task ShowUnexpectedErrorDialog((Exception Exception, Func<Task>? TryRecoverCallback) errorData)
    {
        if (!Environment.IsProduction())
        {
            Logger.LogInformation($"Exception: \"{errorData.Exception.Message}\"");
        }
        
        _exceptionData = errorData;
        StateHasChanged();
        
        _dialogReference = await _mudDialog.ShowAsync();
    }
}