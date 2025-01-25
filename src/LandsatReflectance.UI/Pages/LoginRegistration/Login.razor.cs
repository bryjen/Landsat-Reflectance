using LandsatReflectance.UI.Components;
using LandsatReflectance.UI.Exceptions;
using LandsatReflectance.UI.Services;
using LandsatReflectance.UI.Services.Api;
using LandsatReflectance.UI.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;

namespace LandsatReflectance.UI.Pages.LoginRegistration;

public partial class Login : ComponentBase
{
    [Inject]
    public required IWebAssemblyHostEnvironment Environment { get; set; }
    
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
    [Inject]
    public required ISnackbar Snackbar { get; set; }
    
    [Inject]
    public required IDialogService DialogService { get; set; }
    
    [Inject]
    public required ApiUserService ApiUserService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }
    
    
    [CascadingParameter(Name = "FullPageLoadingOverlay")]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; }
    
    private string m_email = string.Empty;
    private string m_password = string.Empty;
    private bool m_isProcessing;
    
    
#region Password Text Field
    private InputType m_passwordFieldInputType = InputType.Password;

    private string PasswordFieldInputIcon =>
        m_passwordFieldInputType is InputType.Password
            ? Icons.Material.Filled.Visibility
            : Icons.Material.Filled.VisibilityOff;

    private void OnPasswordVisibilityButtonClicked()
    {
        m_passwordFieldInputType = m_passwordFieldInputType is InputType.Password 
            ? InputType.Text 
            : InputType.Password;
    }
#endregion



#region RegistrationPanel
    private string m_registrationEmail = string.Empty;

    private async Task GoToRegistrationPage()
    {
        var workFunc = async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(Rand.GeneratePageSwitchDelayTime()));
        };
        
        var onWorkFinishedCallback = () =>
        {
            string emailQueryString = string.IsNullOrWhiteSpace(m_registrationEmail)
                ? string.Empty
                : "?email=" + Uri.EscapeDataString(m_registrationEmail);
            
            Snackbar.Clear();
            NavigationManager.NavigateTo($"/Register{emailQueryString}");
            return Task.CompletedTask;
        };

        await FullPageLoadingOverlay.ExecuteWithOverlay(workFunc, onWorkFinishedCallback);
    }
#endregion


    private async Task TryLogin()
    {
        m_isProcessing = true;
        StateHasChanged();

        try
        {
            var authToken = await ApiUserService.LoginAsync(m_email, m_password);
            CurrentUserService.TryInit(authToken);

            // logic for switching to home page
            m_isProcessing = false;
            StateHasChanged();

            await FullPageLoadingOverlay.ExecuteWithOverlay(
                async () => { await Task.Delay(TimeSpan.FromSeconds(Rand.GeneratePageSwitchDelayTime())); },
                () =>
                {
                    NavigationManager.NavigateTo("/");
                    Snackbar.Add("Successfully logged in.", Severity.Info);
                    return Task.CompletedTask;
                });
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
        finally
        {
            m_isProcessing = false;
            StateHasChanged();
        }
    }
}