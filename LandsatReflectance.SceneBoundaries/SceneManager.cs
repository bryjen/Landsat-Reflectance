using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MessagePack;
using MessagePack.Resolvers;
using Serilog;

namespace LandsatReflectance.SceneBoundaries;

[MessagePackObject]
public struct RegionInfo
{
    [Key(0)]
    public int Id { get; set; }
    
    [Key(1)]
    public LatLong UpperLeft { get; init; }
    
    [Key(2)]
    public LatLong UpperRight { get; init; }
    
    [Key(3)]
    public LatLong LowerLeft { get; init; }
    
    [Key(4)]
    public LatLong LowerRight { get; init; }
}

public class SceneManager
{
    private List<RegionInfo> RegionInfos { get; set; } = new();
    private Dictionary<int, Lazy<List<Wrs2Scene>>> RegionIdToScenesMap { get; set; } = new();

    // Customizable logger callback methods
    public Action<string> LogInfo { get; set; } = _ => { };
    public Action<string> LogWarning { get; set; } = _ => { };
    public Action<string> LogError { get; set; } = _ => { };

    private static readonly IPointInScene Ps = new NetTopologySuitePointInScene();
    
    
    public IEnumerable<Wrs2Scene> GetScenes(LatLong latLong)
    {
        int? regionId = null;
        foreach (var regionInfo in RegionInfos)
        {
            var asWrs2Scene = new Wrs2Scene
            {
                UpperLeft = regionInfo.UpperLeft,
                UpperRight = regionInfo.UpperRight,
                LowerLeft = regionInfo.LowerLeft,
                LowerRight = regionInfo.LowerRight,
            };
            
            if (Ps.IsPointInScene(latLong, asWrs2Scene))
            {
                regionId = regionInfo.Id;
            }
        }

        // TODO: Create log error message
        
        if (regionId is null)
        {
            LogError("");
            return [];
        }

        if (!RegionIdToScenesMap.TryGetValue(regionId.Value, out var scenes))
        {
            LogError("");
            return [];
        }

        var scenesToReturn = new List<Wrs2Scene>();
        foreach (var scene in scenes.Value)
        {
            if (Ps.IsPointInScene(latLong, scene))
            {
                scenesToReturn.Add(scene);
            }
        }

        return scenesToReturn;
    }
    
    
#region Conversions

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string? TryConvertToFile(string workingDirectory, List<(Wrs2Scene, List<Wrs2Scene>)> regionToScenesList)
    {
        var regionInfos = ConvertToRegionInfoList(regionToScenesList);
        var regionInfosAsBytes = MessagePackSerializer.Serialize(regionInfos);
        var metadataFilePath = Path.Join(workingDirectory, "regions.bin");  // make them have the same file extension soon
        File.WriteAllBytes(metadataFilePath, regionInfosAsBytes);

        var datFilePaths = new List<string>();
        for (int i = 0; i < regionToScenesList.Count; i++)
        {
            var wrs2Scenes = regionToScenesList[i].Item2;
            var asBytes = MessagePackSerializer.Serialize(wrs2Scenes);
            
            var filePath = Path.Join(workingDirectory, $"{i}.dat");
            File.WriteAllBytes(filePath, asBytes);
            datFilePaths.Add(filePath);
        }

        var zipFilePath = Path.Join(workingDirectory, "regions.zip");
        if (File.Exists(zipFilePath))
        {
            Log.Warning("The file \"regions.zip\" already exists, replacing...");
            File.Delete(zipFilePath);
        }
        
        using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
        foreach (var datFilePath in datFilePaths)
        {
            zipArchive.CreateEntryFromFile(datFilePath, Path.GetFileName(datFilePath));
        }
        zipArchive.CreateEntryFromFile(metadataFilePath, Path.GetFileName(metadataFilePath));
        
        return zipFilePath;
    }

    private static List<RegionInfo> ConvertToRegionInfoList(List<(Wrs2Scene, List<Wrs2Scene>)> regionToScenesList)
    {
        return regionToScenesList
            .Select(tuple => tuple.Item1)
            .Select((regionAsScene, i) => new RegionInfo
            {
                Id = i,
                UpperLeft = regionAsScene.UpperLeft,
                UpperRight = regionAsScene.UpperRight,
                LowerLeft = regionAsScene.LowerLeft,
                LowerRight = regionAsScene.LowerRight,
            }).ToList();
    }


    public static SceneManager TryLoadFromBytes(byte[] regionsFileBytes)
    {
        var sceneManager = new SceneManager();

        using var memoryStream = new MemoryStream(regionsFileBytes);
        using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        foreach (var zipArchiveEntry in zipArchive.Entries)
        {
            switch (Path.GetExtension(zipArchiveEntry.Name))
            {
                case ".dat":
                {
                    using var stream = zipArchiveEntry.Open();
                    using var memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    var bytes = memStream.ToArray();

                    var withoutFileExtension = Path.GetFileNameWithoutExtension(zipArchiveEntry.Name);
                    if (!int.TryParse(withoutFileExtension, out var regionId))
                    {
                        continue;
                    }

                    var lazyInit = new Lazy<List<Wrs2Scene>>(() => MessagePackSerializer.Deserialize<List<Wrs2Scene>>(bytes));
                    sceneManager.RegionIdToScenesMap.TryAdd(regionId, lazyInit);
                    
                    break;
                }
                case ".bin":
                {
                    using var stream = zipArchiveEntry.Open();
                    using var memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    var bytes = memStream.ToArray();

                    var regionInfos = MessagePackSerializer.Deserialize<List<RegionInfo>>(bytes);
                    sceneManager.RegionInfos = regionInfos;
                    
                    break;
                }
                default:
                    continue;
            }
            
            
        }

        return sceneManager;
    }
#endregion
}