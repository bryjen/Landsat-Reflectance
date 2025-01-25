using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using Serilog;

namespace LandsatReflectance.SceneBoundaries;

/// <summary>
/// Converts the input <code>.kml</code> file into a compressed set of region-partitioned binary files.
/// </summary>
public class KmlConverter
{
    public static string? Convert(int depth, string outputDirectory, string kmlFilePath)
    {
        var scenes = ReadFromKmlFile(kmlFilePath);
        var regionWithScenesList = Split(depth, scenes);
        
        var workingDirectory = Path.Join(outputDirectory, "temp");
        if (!Directory.Exists(workingDirectory))
        {
            Directory.CreateDirectory(workingDirectory);
        }
        return SceneManager.TryConvertToFile(workingDirectory, regionWithScenesList);
    }

    private static List<Wrs2Scene> ReadFromKmlFile(string kmlFilePath)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(kmlFilePath);

        var xmlNamespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
        xmlNamespaceManager.AddNamespace("kml", "http://www.opengis.net/kml/2.2");

        const string regexPattern = "<strong>([\\w\\s]+)</strong>: +([-\\w\\s\\d.]+)<br>";
        var nodes = xmlDoc.SelectNodes("//kml:Placemark/kml:description", xmlNamespaceManager);

        if (nodes is null)
        {
            throw new Exception();
        }

        var scenes = new List<Wrs2Scene>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var xmlNode = nodes[i];
            if (xmlNode is null)
            {
                continue;
            }

            var dict = new Dictionary<string, object>();
            foreach (Match match in Regex.Matches(xmlNode.InnerText, regexPattern))
            {
                if (match.Groups.Count == 3)
                {
                    dict.TryAdd(match.Groups[1].ToString(), match.Groups[2]);
                }
            }

            var asScene = FromDictionary(i, nodes.Count, dict);
            if (asScene is not null)
            {
                scenes.Add(asScene.Value);
            }
        }
        
        Log.Information("Finished reading .klm file");
        Log.Information($"Successfully parsed {scenes.Count}/{nodes.Count} nodes ({((float) scenes.Count / nodes.Count):P})");

        return scenes;
    }

    // The first 'Wrs2Scene' represents a partitioned region, and not a scene.
    // This is done for simplicity and better compatibility with other helper functions.
    private static List<(Wrs2Scene, List<Wrs2Scene>)> Split(int depth, List<Wrs2Scene> scenes)
    {
        var regions = GetRegionBoundaries(depth);
        var regionWithScenesList = regions
            .Select(tuple => (Bounds: InterpolateIntoScene(tuple.Item1, tuple.Item2), Scenes: new List<Wrs2Scene>()))
            .ToList();

        IPointInScene ps = new NetTopologySuitePointInScene();

        for (var i = 0; i < scenes.Count; i++)
        {
            var scene = scenes[i];
            var completionPercentage = $"{((float) i / scenes.Count):P}";
            
            var validRegions = new List<Wrs2Scene>();  // for logging
            foreach (var (regionAsScene, sceneList) in regionWithScenesList)
            {
                if (ps.IsPointInScene(scene.LowerLeft, regionAsScene) ||
                    ps.IsPointInScene(scene.LowerRight, regionAsScene) ||
                    ps.IsPointInScene(scene.UpperLeft, regionAsScene) ||
                    ps.IsPointInScene(scene.UpperRight, regionAsScene))
                {
                    sceneList.Add(scene);
                    validRegions.Add(scene);
                }
            }

            if (validRegions.Count != 0)
            {
                Log.Information($"[{completionPercentage}] Scene ({scene.Path}, {scene.Row}) was placed into {validRegions.Count} regions.");
            }
            else
            {
                Log.Error($"[{completionPercentage}] Scene ({scene.Path}, {scene.Row}) was NOT placed into any valid regions.");
            }
        }

        return regionWithScenesList;
    }
    
    
    private static Wrs2Scene? FromDictionary(int count, int total, Dictionary<string, object> dict)
    {
        var completionPercentage = $"{((float) count / total):P}";
        
        try
        {
            var latUl = float.Parse(dict["LAT UL"].ToString() ?? string.Empty);
            var lonUl = float.Parse(dict["LON UL"].ToString() ?? string.Empty);
            var ul = new LatLong(latUl, lonUl);
            
            var latUr = float.Parse(dict["LAT UR"].ToString() ?? string.Empty);
            var lonUr = float.Parse(dict["LON UR"].ToString() ?? string.Empty);
            var ur = new LatLong(latUr, lonUr);
            
            var latLl = float.Parse(dict["LAT LL"].ToString() ?? string.Empty);
            var lonLl = float.Parse(dict["LON LL"].ToString() ?? string.Empty);
            var ll = new LatLong(latLl, lonLl);
            
            var latLr = float.Parse(dict["LAT LR"].ToString() ?? string.Empty);
            var lonLr = float.Parse(dict["LON LR"].ToString() ?? string.Empty);
            var lr = new LatLong(latLr, lonLr);
            
            var wrs2Scene = new Wrs2Scene
            {
                Path = (int)float.Parse(dict["PATH"].ToString() ?? string.Empty),
                Row = (int)float.Parse(dict["ROW"].ToString() ?? string.Empty),
                UpperLeft = ul,
                UpperRight = ur,
                LowerLeft = ll,
                LowerRight = lr,
            };
            
            Log.Information($"[{completionPercentage}] Parsed: {wrs2Scene}");

            return wrs2Scene;
        }
        catch (Exception exception)
        {
            Log.Warning($"[{completionPercentage}] Failed to parse into \"Wrs2Scene\": \"{exception.Message}\"");
            return null;
        }
    }

    private static (LatLong, LatLong)[] GetRegionBoundaries(int depth)
    {
        var intervals = (int)Math.Pow(2, depth);
        var latStep = (2d * 90) / intervals;
        var longStep = (2d * 180) / intervals;

        var latIntervals = new (double, double)[intervals];
        var longIntervals = new (double, double)[intervals];
        for (var i = 0; i < latIntervals.Length; i ++)
        {
            var latIntervalStart = (-90d + latStep * i);
            var longIntervalStart = (-180d + longStep * i);

            latIntervals[i] = (latIntervalStart, latIntervalStart + latStep);
            longIntervals[i] = (longIntervalStart, longIntervalStart + longStep);
        }
        
        // logging to debug intervals
        Log.Information("Lat Intervals:");
        foreach (var (intervalStart, intervalEnd) in latIntervals)
        {
            Log.Information($"\t[{intervalStart:F}, {intervalEnd:F}]");
        }
        
        Log.Information("Long Intervals:");
        foreach (var (intervalStart, intervalEnd) in longIntervals)
        {
            Log.Information($"\t[{intervalStart:F}, {intervalEnd:F}]");
        }
        
        Log.Information($"{latIntervals.Length}\u00d7{longIntervals.Length} -> {latIntervals.Length * longIntervals.Length} total regions.");

        // calculating them regions
        var regions = new (LatLong, LatLong)[intervals * intervals];  // first lat long represents the min lat/long, ...
        var count = 0;
        for (var i = 0; i < latIntervals.Length; i++)
        {
            for (var j = 0; j < longIntervals.Length; j++)
            {
                var minLatLong = new LatLong((float)latIntervals[i].Item1, (float)longIntervals[j].Item1);
                var maxLatLong = new LatLong((float)latIntervals[i].Item2, (float)longIntervals[j].Item2);
                regions[count++] = (minLatLong, maxLatLong);
            }
        }

        foreach (var (min, max) in regions)
        {
            Log.Information($"\t{min};\t{max}");
        }

        return regions;
    }

    private static Wrs2Scene InterpolateIntoScene(LatLong lowerLeft, LatLong upperRight)
    {
        return new Wrs2Scene
        {
            LowerLeft = lowerLeft,
            UpperRight = upperRight,
            UpperLeft = new LatLong(lowerLeft.Latitude, upperRight.Longitude),
            LowerRight = new LatLong(upperRight.Latitude, lowerLeft.Longitude),
        };
    }
}