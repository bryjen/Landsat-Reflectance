using System.Net.Mime;
using System.Text;
using System.Text.Json;
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

    public async Task<Result<string, string>> LoginAsync(string email, string password)
    {
        var credentialsDict = new Dictionary<string, string>
        {
            { "email", email },
            { "password", password },
        };

        try
        {
            using var requestBody = new StringContent(JsonSerializer.Serialize(credentialsDict, m_jsonSerializerOptions), Encoding.UTF8, "application/json");
            var response = await m_httpClient.PostAsync("user", requestBody);

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(responseBody, m_jsonSerializerOptions);

            if (apiResponse is null)
            {
                return Result<string, string>.FromError("Response from the server is null");
            }

            if (apiResponse.ErrorMessage is not null)
            {
                return Result<string, string>.FromError(apiResponse.ErrorMessage);
            }

            var authToken = apiResponse.Data;
            return Result<string, string>.FromOk(authToken);
        }
        catch (Exception exception)
        {
            m_logger.LogError(exception.ToString());
            return Result<string, string>.FromError("An unknown error occurred");
        }
    }

    public async Task<Result<string, string>> RegisterAsync(string email, string firstName, string lastName, string password, bool isEmailEnabled)
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
                return Result<string, string>.FromError("Response from the server is null");
            }

            if (apiResponse.ErrorMessage is not null)
            {
                return Result<string, string>.FromError(apiResponse.ErrorMessage);
            }

            var authToken = apiResponse.Data;
            return Result<string, string>.FromOk(authToken);
        }
        catch (Exception exception)
        {
            m_logger.LogError(exception.ToString());
            return Result<string, string>.FromError("An unknown error occurred");
        }
    }
}