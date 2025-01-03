using NetTopologySuite.Geometries;

namespace LandsatReflectance.SceneBoundaries;

public interface IPointInScene
{
    public bool IsPointInScene(LatLong point, Wrs2Scene scene);
}


public class NetTopologySuitePointInScene : IPointInScene
{
    private static Coordinate LatLongToCoordinate(LatLong latLong) => new(latLong.Longitude, latLong.Latitude);
    
    public bool IsPointInScene(LatLong point, Wrs2Scene scene)
    {
        var polygon = new Polygon(new LinearRing([
            LatLongToCoordinate(scene.LowerLeft),
            LatLongToCoordinate(scene.LowerRight),
            LatLongToCoordinate(scene.UpperRight),
            LatLongToCoordinate(scene.UpperLeft),
            LatLongToCoordinate(scene.LowerLeft),
        ]));

        return polygon.Contains(new Point(LatLongToCoordinate(point)));
    }
}