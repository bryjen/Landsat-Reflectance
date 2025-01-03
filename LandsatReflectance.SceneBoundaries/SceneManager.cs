using System.Text.Json;
using MessagePack;
using MessagePack.Resolvers;

namespace LandsatReflectance.SceneBoundaries;

public struct RegionInfo
{
    public int Id { get; set; }
    public LatLong UpperLeft { get; init; }
    public LatLong UpperRight { get; init; }
    public LatLong LowerLeft { get; init; }
    public LatLong LowerRight { get; init; }
}

public class SceneManager
{

#region Conversions
    public static string? TryConvertToFile(string workingDirectory, List<(Wrs2Scene, List<Wrs2Scene>)> regionToScenesList)
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var regionInfos = ConvertToRegionInfoList(regionToScenesList);
        var regionInfosJson = JsonSerializer.Serialize(regionInfos, jsonSerializerOptions);
        File.WriteAllText(Path.Join(workingDirectory, "regions.json"), regionInfosJson);

        for (int i = 0; i < regionToScenesList.Count; i++)
        {
            var wrs2Scenes = regionToScenesList[i].Item2;
            var asBytes = MessagePackSerializer.Serialize(wrs2Scenes);
            File.WriteAllBytes(Path.Join(workingDirectory, $"{i}.dat"), asBytes);
        }

        return workingDirectory;
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

    public static SceneManager TryLoadFromFile()
    {
        throw new NotImplementedException();
    }
#endregion
}