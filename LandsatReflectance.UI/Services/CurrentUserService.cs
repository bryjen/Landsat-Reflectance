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
        
        
        
        IsAuthenticated = true;

        return Result<Unit, string>.FromOk(Unit.Default);
    }
}