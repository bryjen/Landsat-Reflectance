using System;
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
    
    public ApiTargetService(
        JsonSerializerOptions jsonSerializerOptions, 
        ILogger<ApiTargetService> logger, 
        HttpClient httpClient)
    {
        _jsonSerializerOptions = jsonSerializerOptions;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<Result<SceneData[], string>> TryGetSceneData(
        string authToken, 
        int path, 
        int row, 
        int results, 
        CancellationToken? cancellationToken = null)
    {
        cancellationToken ??= CancellationToken.None;

        try
        {
            if (_httpClient.DefaultRequestHeaders.Authorization is null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            var response = await _httpClient.GetAsync($"scene?path={path}&row={row}&results={results}", cancellationToken.Value);

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse =
                JsonSerializer.Deserialize<ApiResponse<SceneData[]>>(responseBody, _jsonSerializerOptions);

            if (apiResponse is null)
            {
                return Result<SceneData[], string>.FromError("Response from the server is null");
            }

            if (apiResponse.ErrorMessage is not null)
            {
                return Result<SceneData[], string>.FromError(apiResponse.ErrorMessage);
            }

            return Result<SceneData[], string>.FromOk(apiResponse.Data);
        }
        catch (OperationCanceledException _)
        {
            return Result<SceneData[], string>.FromError("Cancelled operation");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception.ToString());
            return Result<SceneData[], string>.FromError("An unknown error occurred");
        }
    }
    
    public async Task<Result<Target[], string>> TryGetUserTargets(string authToken)
    {
        try
        {
            if (_httpClient.DefaultRequestHeaders.Authorization is null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
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
        catch (OperationCanceledException _)
        {
            return Result<Target[], string>.FromError("Cancelled Operation");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception.ToString());
            return Result<Target[], string>.FromError("An unknown error occurred");
        }
    }

    public async Task<Target?> TryDeleteTarget(string authToken, Target targetToDelete)
    {
        throw new NotImplementedException();
    }
}