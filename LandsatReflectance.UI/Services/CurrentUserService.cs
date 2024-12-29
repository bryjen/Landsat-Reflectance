// using LandsatReflectance.Common.Models;

using System.IdentityModel.Tokens.Jwt;
using Blazored.LocalStorage;
using LandsatReflectance.UI.Utils;

namespace LandsatReflectance.UI.Services;


public class AuthenticatedEventArgs : EventArgs
{
    public required string Token { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
}

public class CurrentUserService
{
    public string Token { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsAuthenticated { get; private set; } = false;
    
    
    public EventHandler<AuthenticatedEventArgs> OnUserAuthenticated = (_, _) => { };
    
    public EventHandler OnUserLogout = (_, _) => { };
    
    
    private readonly ILogger<CurrentUserService> _logger;
    private readonly ISyncLocalStorageService _localStorage;
    
    private const string AuthTokenLocalStorageKey = "authToken";
    
    
    public CurrentUserService(
        ILogger<CurrentUserService> logger, 
        ISyncLocalStorageService localStorage)
    {
        _logger = logger;
        _localStorage = localStorage;
    }

    
    public Result<Unit, string> TryInitFromLocalStorage()
    {
        var authToken = _localStorage.GetItemAsString(AuthTokenLocalStorageKey);

        if (authToken is not null)
        {
            return TryInitFromAuthToken(authToken);
        }
        
        // A null auth token isn't an error
        return Result<Unit, string>.FromOk(Unit.Default);
    }

    public Result<Unit, string> TryInitFromAuthToken(string authToken)
    {
        Token = authToken;

        var handler = new JwtSecurityTokenHandler();
        var asJwtToken = handler.ReadJwtToken(authToken);

        
        var givenNameClaim = asJwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.GivenName);
        if (givenNameClaim is null)
        {
            _logger.LogInformation($"Could not find the claim \"{JwtRegisteredClaimNames.GivenName}\".");
            return Result<Unit, string>.FromError("Failed to authenticate user");
        }
        FirstName = givenNameClaim.Value;
        
        
        var familyNameClaim = asJwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.FamilyName);
        if (familyNameClaim is null)
        {
            _logger.LogInformation($"Could not find the claim \"{JwtRegisteredClaimNames.FamilyName}\".");
            return Result<Unit, string>.FromError("Failed to authenticate user");
        }
        LastName = familyNameClaim.Value;
        
        
        var emailClaim = asJwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Email);
        if (emailClaim is null)
        {
            _logger.LogInformation($"Could not find the claim \"{JwtRegisteredClaimNames.Email}\".");
            return Result<Unit, string>.FromError("Failed to authenticate user");
        }
        Email = emailClaim.Value;
        
        IsAuthenticated = true;

        PersistAuthToken();
        NotifyLoggedIn();
        
        return Result<Unit, string>.FromOk(Unit.Default);
    }

    public void LogoutUser()
    {
        if (IsAuthenticated)
        {
            Token = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
            Email = string.Empty;
            IsAuthenticated = false;
            
            OnUserLogout.Invoke(this, EventArgs.Empty);

            if (_localStorage.ContainKey(AuthTokenLocalStorageKey))
            {
                _localStorage.RemoveItem(AuthTokenLocalStorageKey);
            }
        }
    }

    private void NotifyLoggedIn()
    {
        OnUserAuthenticated.Invoke(this, new AuthenticatedEventArgs
        {
            Token = this.Token,
            FirstName = this.FirstName,
            LastName = this.LastName,
            Email = this.Email,
        });
    }
    
    
    private void PersistAuthToken()
    {
        if (IsAuthenticated)
        {
            _localStorage.SetItemAsString(AuthTokenLocalStorageKey, Token);
        }
    }
}