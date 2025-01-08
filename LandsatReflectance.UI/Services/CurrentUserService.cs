// using LandsatReflectance.Common.Models;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Authentication;
using Blazored.LocalStorage;
using LandsatReflectance.UI.Exceptions;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Services.Api;
using LandsatReflectance.UI.Utils;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NetTopologySuite.Operation.Valid;

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
    public string AccessToken { get; private set; } = string.Empty;
    public string RefreshToken { get; private set; } = string.Empty;
    
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsAuthenticated { get; private set; } = false;
    
    
    public EventHandler<AuthenticatedEventArgs> OnUserAuthenticated = (_, _) => { };
    public EventHandler OnUserLogout = (_, _) => { };
    
    private readonly IWebAssemblyHostEnvironment _environment;
    private readonly ILogger<CurrentUserService> _logger;
    private readonly ISyncLocalStorageService _localStorage;
    private readonly ApiUserService _apiUserService;
    
    private const string AuthTokenLocalStorageKey = "authToken";
    private const string RefreshTokenLocalStorageKey = "refreshToken";
    
    
    public CurrentUserService(
        IWebAssemblyHostEnvironment environment,
        ILogger<CurrentUserService> logger, 
        ISyncLocalStorageService localStorage,
        ApiUserService apiUserService)
    {
        _environment = environment;
        _logger = logger;
        _localStorage = localStorage;
        _apiUserService = apiUserService;
        
        AccessToken = _localStorage.GetItemAsString(AuthTokenLocalStorageKey) ?? string.Empty;
        RefreshToken = _localStorage.GetItemAsString(RefreshTokenLocalStorageKey) ?? string.Empty;
    }

    public void TryInit(LoginData loginData)
    {
        RefreshToken = loginData.RefreshToken;
        TryInitFromAuthToken(loginData.AccessToken);
        
        PersistTokens();
    }
    
    public async Task TryInitFromLocalValues()
    {
        var isEmpty = string.IsNullOrWhiteSpace;
        
        if (!isEmpty(AccessToken) && !isEmpty(RefreshToken))
        {
            if (IsTokenExpired(AccessToken))
            {
                if (!_environment.IsProduction())
                {
                    _logger.LogInformation("Access token is expired, refreshing ...");
                }
                
                try
                {
                    AccessToken = await _apiUserService.RefreshAccessToken(RefreshToken);
                    TryInitFromAuthToken(AccessToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception.Message);
                }
            }
            else
            {
                TryInitFromAuthToken(AccessToken);
            }
        }
    }

    public void TryInitFromAuthToken(string accessToken)
    {
        AccessToken = accessToken;

        var handler = new JwtSecurityTokenHandler();
        var asJwtToken = handler.ReadJwtToken(accessToken);

        var givenNameClaim = asJwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.GivenName);
        if (givenNameClaim is null)
        {
            throw new AuthException($"Could not find the claim \"{JwtRegisteredClaimNames.GivenName}\".");
        }
        FirstName = givenNameClaim.Value;
        
        
        var familyNameClaim = asJwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.FamilyName);
        if (familyNameClaim is null)
        {
            throw new AuthException($"Could not find the claim \"{JwtRegisteredClaimNames.FamilyName}\".");
        }
        LastName = familyNameClaim.Value;
        
        
        var emailClaim = asJwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Email);
        if (emailClaim is null)
        {
            throw new AuthException($"Could not find the claim \"{JwtRegisteredClaimNames.Email}\".");
        }
        Email = emailClaim.Value;
        
        IsAuthenticated = true;

        PersistTokens();
        NotifyLoggedIn();
    }

    public void LogoutUser()
    {
        if (IsAuthenticated)
        {
            AccessToken = string.Empty;
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
            Token = this.AccessToken,
            FirstName = this.FirstName,
            LastName = this.LastName,
            Email = this.Email,
        });
    }
    
    private void PersistTokens()
    {
        if (IsAuthenticated)
        {
            _localStorage.SetItemAsString(AuthTokenLocalStorageKey, AccessToken);
            _localStorage.SetItemAsString(RefreshTokenLocalStorageKey, RefreshToken);
        }
    }
    
    
    private static bool IsTokenExpired(string accessToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(accessToken);

        var unixExpDate = jwtToken.Payload.Expiration;
        if (unixExpDate is null)
        {
            return true;
        }

        var expirationDateTimeUtc = DateTimeOffset.FromUnixTimeSeconds(unixExpDate.Value).UtcDateTime;
        return DateTime.UtcNow >= expirationDateTimeUtc;
    }
}