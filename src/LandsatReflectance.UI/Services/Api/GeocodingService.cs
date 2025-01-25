using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using LandsatReflectance.SceneBoundaries;

namespace LandsatReflectance.UI.Services.Api;



/// <summary>
/// Query is Lat/Long, result is location information (addresses).
/// </summary>
public class ReverseGeocodingData
{
    public string? City { get; set; }
    public string? Country { get; set; }

    
    public override string ToString()
    {
        return (City, Country) switch
        {
            ({ } city, { } country) => $"{city}, {country}",
            (null, { } country) => country,
            ({ } city, null) => city,
            _ => string.Empty,
        };
    }
}

/// <summary>
/// Query is an address, result is location information (more specific addresses & coordinates).
/// </summary>
public class ForwardGeocodingData
{
    public string FormattedLocation { get; set; } = string.Empty;
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }


    public override string ToString()
    {
        return $"[{Latitude:F2}°N, {Longitude:F2}°W] {FormattedLocation}";
    }
    
    public static string ToString(ForwardGeocodingData forwardGeocodingData)
    {
        return forwardGeocodingData.ToString();
    }
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

    
    public async Task<ReverseGeocodingData> GetNearestCity(LatLong latLong)
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


        string? city = null;
        if (componentsJsonElement.TryGetProperty("city", out var cityJsonElement))
        {
            city = cityJsonElement.GetString();
        }
        
        string? country = null;
        if (componentsJsonElement.TryGetProperty("country", out var countryJsonElement))
        {
            country = countryJsonElement.GetString();
        }

        return new ReverseGeocodingData
        {
            City = city,
            Country = country
        };
    }

    public async Task<IEnumerable<ForwardGeocodingData>> GetRelatedAddresses(string addressStr, CancellationToken? cancellationToken = null)
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;  // clear auth header, not needed
        cancellationToken ??= CancellationToken.None;

        var response = await _httpClient.GetAsync($"json?q={Uri.EscapeDataString(addressStr)}&key={ApiKey}", cancellationToken.Value);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken.Value);

        var asJsonElement = JsonDocument.Parse(responseBody).RootElement;
        
        if (!asJsonElement.TryGetProperty("results", out var resultsJsonElement))
        {
            return [];
        }

        var resultsAsList = resultsJsonElement.EnumerateArray().ToList();
        if (resultsAsList.Count == 0)
        {
            return [];
        }

        var datas = new List<ForwardGeocodingData>();
        foreach (var locationDataJsonElement in resultsAsList)
        {
            if (!locationDataJsonElement.TryGetProperty("formatted", out var formattedLocationJsonElement))
                continue;
            
            
            if (!locationDataJsonElement.TryGetProperty("geometry", out var geometryJsonElement))
                continue;
            
            if (!geometryJsonElement.TryGetProperty("lat", out var latJsonElement) || latJsonElement.ValueKind is not JsonValueKind.Number)
                continue;
            
            if (!geometryJsonElement.TryGetProperty("lng", out var lngJsonElement) || lngJsonElement.ValueKind is not JsonValueKind.Number)
                continue;

            var data = new ForwardGeocodingData
            {
                FormattedLocation = formattedLocationJsonElement.GetString() ?? string.Empty,
                Latitude = latJsonElement.GetDouble(),
                Longitude = lngJsonElement.GetDouble(),
            };
            datas.Add(data);
        }

        return datas;
    }
}