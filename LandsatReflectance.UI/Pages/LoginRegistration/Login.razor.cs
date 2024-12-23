using LandsatReflectance.UI.Components;
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
    public required UserService UserService { get; set; }
    
    [Inject]
    public required CurrentUserService CurrentUserService { get; set; }
    
    
    [CascadingParameter]
    public required FullPageLoadingOverlay FullPageLoadingOverlay { get; set; }
    
    private string m_email = string.Empty;
    private string m_password = string.Empty;
    private bool m_isProcessing = false;
    
    
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
        
        
        var authTokenResult = await UserService.LoginAsync(m_email, m_password);
        var loginResult = authTokenResult.Bind(CurrentUserService.TryInitCurrentUser);

        await loginResult.Match<Task<Unit>, Unit, string>(
            async _ =>  // on successful login
            {
                m_isProcessing = false;
                StateHasChanged();

                await FullPageLoadingOverlay.ExecuteWithOverlay(
                    async () => await Task.Delay(TimeSpan.FromSeconds(Rand.GeneratePageSwitchDelayTime())),
                    () =>
                    {
                        NavigationManager.NavigateTo("/");
                        Snackbar.Add("Successfully logged in.", Severity.Info);
                        return Task.CompletedTask;
                    });
                return Unit.Default;
            }, 
            errorMsg =>  // on unsuccessful login
            {
                m_isProcessing = false;
                StateHasChanged();
                
                Snackbar.Add(errorMsg, Severity.Error);
                return Task.FromResult(Unit.Default);
            });
    }
}