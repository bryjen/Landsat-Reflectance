using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using LandsatReflectance.SceneBoundaries;

namespace LandsatReflectance.UI.Services.Api;

// Uses OpenCage's Geocoding API.

public class GeocodingService
{
    // no way to hide this brah
    private const string ApiKey = "b0d06c4f1de444eabd8086106b7eb833";

    private readonly ILogger<GeocodingService> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly HttpClient _httpClient;
    
    public GeocodingService(
        ILogger<GeocodingService> logger,
        JsonSerializerOptions jsonSerializerOptions, 
        HttpClient httpClient)
    {
        _logger = logger;
        _jsonSerializerOptions = jsonSerializerOptions;

        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("https://api.opencagedata.com/geocode/v1/");
    }

    public async Task<(string City, string Country)> GetNearestCity(LatLong latLong)
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;  // clear auth header, not needed

        var coordinatesEncoded = Uri.EscapeDataString($"{latLong.Latitude},{latLong.Longitude}");
        var response = await _httpClient.GetAsync($"json?q={coordinatesEncoded}&key={ApiKey}");
        var responseBody = await response.Content.ReadAsStringAsync();

        var asJsonElement = JsonDocument.Parse(responseBody).RootElement;

        if (!asJsonElement.TryGetProperty("results", out var resultsJsonElement))
        {
            throw new NotImplementedException();
        }

        var resultsAsList = resultsJsonElement.EnumerateArray().ToList();
        if (resultsAsList.Count == 0)
        {
            throw new NotImplementedException();
        }

        var firstResultJsonElement = resultsAsList.First();
        if (!firstResultJsonElement.TryGetProperty("components", out var componentsJsonElement))
        {
            throw new NotImplementedException();
        }
        
        
        if (!componentsJsonElement.TryGetProperty("city", out var cityJsonElement))
        {
            throw new NotImplementedException();
        }
        
        if (!componentsJsonElement.TryGetProperty("country", out var countryJsonElement))
        {
            throw new NotImplementedException();
        }

        var city = cityJsonElement.GetString() ?? string.Empty;
        var country = countryJsonElement.GetString() ?? string.Empty;
        return (city, country);
    }
}