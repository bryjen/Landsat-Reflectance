﻿using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using LandsatReflectance.SceneBoundaries;

namespace LandsatReflectance.UI.Services.Api;



public class LocationData
{
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}


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

    public async Task<LocationData> GetNearestCity(LatLong latLong)
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;  // clear auth header, not needed

        var coordinatesEncoded = Uri.EscapeDataString($"{latLong.Latitude},{latLong.Longitude}");
        var response = await _httpClient.GetAsync($"json?q={coordinatesEncoded}&key={ApiKey}");
        var responseBody = await response.Content.ReadAsStringAsync();

        var asJsonElement = JsonDocument.Parse(responseBody).RootElement;

        if (!asJsonElement.TryGetProperty("results", out var resultsJsonElement))
        {
            throw new JsonException("Could not find the property \"/results\".");
        }

        var resultsAsList = resultsJsonElement.EnumerateArray().ToList();
        if (resultsAsList.Count == 0)
        {
            throw new JsonException("The property \"/results\" contains no values.");
        }

        var firstResultJsonElement = resultsAsList.First();
        if (!firstResultJsonElement.TryGetProperty("components", out var componentsJsonElement))
        {
            throw new JsonException("Could not find the property \"/results[0]/components\".");
        }
        
        
        if (!componentsJsonElement.TryGetProperty("city", out var cityJsonElement))
        {
            throw new JsonException("Could not find the property \"/results[0]/components/city\".");
        }
        
        if (!componentsJsonElement.TryGetProperty("country", out var countryJsonElement))
        {
            throw new JsonException("Could not find the property \"/results[0]/components/country\".");
        }

        return new LocationData
        {
            City = cityJsonElement.GetString() ?? string.Empty,
            Country = countryJsonElement.GetString() ?? string.Empty
        };
    }
}