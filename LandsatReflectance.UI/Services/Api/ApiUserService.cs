using System.Net.Mime;
using System.Text;
using System.Text.Json;
using LandsatReflectance.UI.Exceptions;
using LandsatReflectance.UI.Models;
// using LandsatReflectance.Common.Models;
// using LandsatReflectance.Common.Models.Request;
// using LandsatReflectance.Common.Models.ResponseModels;
using LandsatReflectance.UI.Pages.LoginRegistration;
using LandsatReflectance.UI.Utils;
using OneOf.Types;

namespace LandsatReflectance.UI.Services.Api;

public class ApiUserService
{
    private readonly JsonSerializerOptions m_jsonSerializerOptions;
    private readonly ILogger<ApiUserService> m_logger;
    private readonly HttpClient m_httpClient;
    
    public ApiUserService(
        JsonSerializerOptions jsonSerializerOptions, 
        ILogger<ApiUserService> logger, 
        HttpClient httpClient)
    {
        m_jsonSerializerOptions = jsonSerializerOptions;
        m_logger = logger;
        m_httpClient = httpClient;
    }

    public async Task<LoginData> LoginAsync(string email, string password)
    {
        var credentialsDict = new Dictionary<string, string>
        {
            { "email", email },
            { "password", password },
        };

        try
        {
            using var requestBody = new StringContent(JsonSerializer.Serialize(credentialsDict, m_jsonSerializerOptions), Encoding.UTF8,"application/json");
            var response = await m_httpClient.PostAsync("user", requestBody);

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<LoginData>>(responseBody, m_jsonSerializerOptions);

            if (apiResponse is null)
            {
                throw new AuthException("Response from the server is null");
            }

            if (apiResponse.ErrorMessage is not null)
            {
                throw new AuthException(apiResponse.ErrorMessage);
            }

            return apiResponse.Data;
        }
        catch (AuthException)
        {
            throw;
        }
        catch (Exception exception)
        {
            m_logger.LogError(exception.ToString());
            throw;
        }
    }

    // TODO: Change API/UI to return 'LoginData'
    public async Task<string> RegisterAsync(string email, string firstName, string lastName, string password, bool isEmailEnabled)
    {
        var userInfoDict = new Dictionary<string, object>
        {
            { "email", email },
            { "firstName", firstName },
            { "lastName", lastName },
            { "password", password },
            { "isEmailEnabled", isEmailEnabled },
        };
        
        try
        {
            using var requestBody = new StringContent(JsonSerializer.Serialize(userInfoDict, m_jsonSerializerOptions), Encoding.UTF8, "application/json");
            var response = await m_httpClient.PostAsync("user/create", requestBody);

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(responseBody, m_jsonSerializerOptions);

            if (apiResponse is null)
            {
                throw new AuthException("Response from the server is null");
            }

            if (apiResponse.ErrorMessage is not null)
            {
                throw new AuthException(apiResponse.ErrorMessage);
            }

            return apiResponse.Data;
        }
        catch (AuthException)
        {
            throw;
        }
        catch (Exception exception)
        {
            m_logger.LogError(exception.ToString());
            throw;
        }
    }

    public async Task<string> RefreshAccessToken(string refreshToken)
    {
        var requestBodyAsDict = new Dictionary<string, string>
        {
            { "refreshToken", refreshToken }
        };

        try
        {
            using var requestBody = new StringContent(JsonSerializer.Serialize(requestBodyAsDict, m_jsonSerializerOptions), Encoding.UTF8,"application/json");
            var response = await m_httpClient.PostAsync("user/refresh-token-login", requestBody);
            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(responseBody, m_jsonSerializerOptions);

            if (apiResponse is null)
            {
                throw new AuthException("Response from the server is null");
            }

            if (apiResponse.ErrorMessage is not null)
            {
                throw new AuthException(apiResponse.ErrorMessage);
            }

            return apiResponse.Data;
        }
        catch (AuthException)
        {
            throw;
        }
        catch (Exception exception)
        {
            m_logger.LogError(exception.ToString());
            throw;
        }
    }
}