// using LandsatReflectance.Common.Models;

using System.IdentityModel.Tokens.Jwt;
using LandsatReflectance.UI.Utils;

namespace LandsatReflectance.UI.Services;

public class CurrentUserService
{
    public string AuthToken { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    public bool IsAuthenticated { get; private set; } = false;
    
    
    private ILogger<CurrentUserService> m_logger;
    
    
    public CurrentUserService(ILogger<CurrentUserService> logger)
    {
        m_logger = logger;
    }
    

    public Result<Unit, string> TryInitCurrentUser(string authToken)
    {
        AuthToken = authToken;

        var handler = new JwtSecurityTokenHandler();
        var asJwtToken = handler.ReadJwtToken(authToken);

        
        var givenNameClaim = asJwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.GivenName);
        if (givenNameClaim is null)
        {
            m_logger.LogInformation($"Could not find the claim \"{JwtRegisteredClaimNames.GivenName}\".");
            return Result<Unit, string>.FromError("Failed to authenticate user");
        }
        FirstName = givenNameClaim.Value;
        
        
        var familyNameClaim = asJwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.FamilyName);
        if (familyNameClaim is null)
        {
            m_logger.LogInformation($"Could not find the claim \"{JwtRegisteredClaimNames.FamilyName}\".");
            return Result<Unit, string>.FromError("Failed to authenticate user");
        }
        LastName = familyNameClaim.Value;
        
        
        var emailClaim = asJwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Email);
        if (emailClaim is null)
        {
            m_logger.LogInformation($"Could not find the claim \"{JwtRegisteredClaimNames.Email}\".");
            return Result<Unit, string>.FromError("Failed to authenticate user");
        }
        Email = emailClaim.Value;
        
        
        IsAuthenticated = true;

        return Result<Unit, string>.FromOk(Unit.Default);
    }

    public Result<Unit, string> TryLogoutUser()
    {
        if (IsAuthenticated)
        {
            return Result<Unit, string>.FromOk(Unit.Default);
        }
        
        return Result<Unit, string>.FromOk(Unit.Default);
    }
}