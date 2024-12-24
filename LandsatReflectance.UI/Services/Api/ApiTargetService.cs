using System.Net.Http.Headers;
using System.Text.Json;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Utils;

namespace LandsatReflectance.UI.Services.Api;

public class ApiTargetService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly ILogger<ApiTargetService> _logger;
    private readonly HttpClient _httpClient;
    private readonly CurrentUserService _currentUserService;
    
    public ApiTargetService(
        JsonSerializerOptions jsonSerializerOptions, 
        ILogger<ApiTargetService> logger, 
        HttpClient httpClient,
        CurrentUserService currentUserService)
    {
        _jsonSerializerOptions = jsonSerializerOptions;
        _logger = logger;
        _httpClient = httpClient;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Target[], string>> TryGetUserTargets()
    {
        try
        {
            if (_httpClient.DefaultRequestHeaders.Authorization is null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _currentUserService.AuthToken);
            }
            
            var response = await _httpClient.GetAsync("user/targets");

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<Target[]>>(responseBody, _jsonSerializerOptions);

            if (apiResponse is null)
            {
                return Result<Target[], string>.FromError("Response from the server is null");
            }

            if (apiResponse.ErrorMessage is not null)
            {
                return Result<Target[], string>.FromError(apiResponse.ErrorMessage);
            }

            return Result<Target[], string>.FromOk(apiResponse.Data);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception.ToString());
            return Result<Target[], string>.FromError("An unknown error occurred");
        }
    }
}