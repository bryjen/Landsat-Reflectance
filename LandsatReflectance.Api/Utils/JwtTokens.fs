module FsLandsatApi.Utils.JwtTokens

open System
open System.Text
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt

open FsToolkit.ErrorHandling
open Microsoft.IdentityModel.Tokens

open LandsatReflectance.Api.Models.User


let issuer = "landsat-api.onrender.com"
let audience = "flateartherslandsat.ca"


type RefreshTokenParseResults =
    { Email: string
      RefreshGuid: Guid }

let rec tryParseRefreshToken (secret: string) (refreshTokenRaw: string) =
    let validationParameters = TokenValidationParameters()
    validationParameters.ValidateIssuer <- true
    validationParameters.ValidIssuer <- issuer
    
    validationParameters.ValidateAudience <- true
    validationParameters.ValidAudience <- audience
    
    let signingKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    validationParameters.ValidateIssuerSigningKey <- true
    validationParameters.IssuerSigningKey <- signingKey
    
    let tokenHandler = JwtSecurityTokenHandler()
    let claimsPrincipal, _ = tokenHandler.ValidateToken(refreshTokenRaw, validationParameters)
    
    result {
        let alternateJwtClaimTypeName = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
        let! (refreshGuidStr: string) = tryGetClaimValue [ JwtRegisteredClaimNames.Jti ] claimsPrincipal
        let! email = tryGetClaimValue [ JwtRegisteredClaimNames.Email; alternateJwtClaimTypeName ] claimsPrincipal
        
        let! refreshGuid = 
            match Guid.TryParse(refreshGuidStr) with
            | true, guid -> Ok guid
            | false, _ -> Error "Unable to parse the refresh token information."
            
        return { Email = email
                 RefreshGuid = refreshGuid }
    }
    
and private tryGetClaimValue (validClaimTypesList: string list) (claimsPrincipal: ClaimsPrincipal) =
    claimsPrincipal.Claims
    |> Seq.tryFind (fun (claim: Claim) -> List.contains claim.Type validClaimTypesList)
    |> function
        | Some claim ->
            Ok claim.Value
        | None ->
            let asString = String.Join(", ", (List.map (fun str -> $"\"{str}\"") validClaimTypesList))
            Error $"Could not find any \"{asString}\""


let createRefreshToken (secret: string) (refreshGuid: Guid) (user: User) =
    let signingKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    let credentials = SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
    
    let claims = [
        Claim(JwtRegisteredClaimNames.Jti, refreshGuid.ToString())
        Claim(JwtRegisteredClaimNames.Email, user.Email)
    ]
    
    let token = JwtSecurityToken(
        issuer = issuer,
        audience = audience,
        claims = claims,
        notBefore = DateTime.UtcNow,
        expires = DateTime.UtcNow.AddDays(1),
        signingCredentials = credentials)
    
    JwtSecurityTokenHandler().WriteToken(token)
    

let createAccessToken (secret: string) (user: User) =
    let signingKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    let credentials = SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
    
    let mutable claims = [
        Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        Claim(JwtRegisteredClaimNames.GivenName, user.FirstName)
        Claim(JwtRegisteredClaimNames.FamilyName, user.LastName)
        Claim(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}")
        Claim(JwtRegisteredClaimNames.Email, user.Email)
    ]
    
    if user.IsAdmin then
        claims <- Claim("role", "Admin") :: claims
    
    let token = JwtSecurityToken(
        issuer = issuer,
        audience = audience,
        claims = claims,
        notBefore = DateTime.UtcNow,
        expires = DateTime.UtcNow.AddHours(1),
        signingCredentials = credentials)
    
    JwtSecurityTokenHandler().WriteToken(token)
