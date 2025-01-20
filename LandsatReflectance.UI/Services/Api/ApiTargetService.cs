using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LandsatReflectance.UI.Models;
using LandsatReflectance.UI.Utils;
using Microsoft.Extensions.Localization;

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

    public async Task<SceneData[]> TryGetSceneData(
        int path, 
        int row, 
        int results, 
        CancellationToken? cancellationToken = null)
    {
        cancellationToken ??= CancellationToken.None;

        var response = await _httpClient.GetAsync($"scene?path={path}&row={row}&results={results}", cancellationToken.Value);

        var responseBody = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<SceneData[]>>(responseBody, _jsonSerializerOptions);

        if (apiResponse is null)
        {
            throw new InvalidDataException("Response from the server is null");
        }

        if (apiResponse.ErrorMessage is not null)
        {
            throw new InvalidOperationException("Response from the server is null");
        }

        return apiResponse.Data;
    }

    public async Task<Target> TryAddTarget(string authToken, Dictionary<string, object> targetInfo)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
        
        var requestBodyStr = JsonSerializer.Serialize(targetInfo, _jsonSerializerOptions);
        var stringContent = new StringContent(requestBodyStr, Encoding.UTF8, "application/json");
        var responseMessage = await _httpClient.PostAsync("user/targets", stringContent);

        var responseBody = await responseMessage.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<Target>>(responseBody, _jsonSerializerOptions);
        
        if (apiResponse is null)
        {
            throw new InvalidDataException("Response from the server is null");
        }

        if (apiResponse.ErrorMessage is not null)
        {
            throw new InvalidOperationException("Response from the server is null");
        }

        return apiResponse.Data;
    }
    
    public async Task<Target[]> TryGetUserTargets(string authToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        var response = await _httpClient.GetAsync("user/targets");

        var responseBody = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<Target[]>>(responseBody, _jsonSerializerOptions);

        if (apiResponse is null)
        {
            throw new InvalidDataException("Response from the server is null");
        }

        if (apiResponse.ErrorMessage is not null)
        {
            throw new InvalidOperationException("Response from the server is null");
        }

        return apiResponse.Data;
    }

    public async Task<Target> TryDeleteTarget(string authToken, Target targetToDelete)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        var requestUri = $"user/targets?target-id={targetToDelete.Id}";
        var response = await _httpClient.DeleteAsync(requestUri);

        var responseBody = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(responseBody, _jsonSerializerOptions);

        if (apiResponse is null)
        {
            throw new InvalidDataException("Response from the server is null");
        }

        if (apiResponse.ErrorMessage is not null)
        {
            throw new InvalidOperationException("Response from the server is null");
        }

        return targetToDelete;
    }
}