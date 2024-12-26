namespace LandsatReflectance.SceneBoundaries;

public struct LatLong
{
    public float Latitude { get; init; }
    public float Longitude { get; init; }
}

public struct Wrs2Scene
{
    public int Path { get; init; }
    public int Row { get; init; }
    public LatLong UpperLeft { get; init; }
    public LatLong UpperRight { get; init; }
    public LatLong LowerLeft { get; init; }
    public LatLong LowerRight { get; init; }
}