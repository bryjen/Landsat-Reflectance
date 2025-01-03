using MessagePack;

namespace LandsatReflectance.SceneBoundaries;

[MessagePackObject]
public readonly record struct LatLong
{
    [Key(0)]
    public float Latitude { get; }
    
    [Key(1)]
    public float Longitude { get; }

    public LatLong(float latitude, float longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public override string ToString()
    {
        return $"{Latitude:F5}\u00b0N, {Longitude:F5}\u00b0W";
    }
}

[MessagePackObject]
public readonly struct Wrs2Scene
{
    [Key(0)]
    public int Path { get; init; }
    
    [Key(1)]
    public int Row { get; init; }
    
    [Key(2)]
    public LatLong UpperLeft { get; init; }
    
    [Key(3)]
    public LatLong UpperRight { get; init; }
    
    [Key(4)]
    public LatLong LowerLeft { get; init; }
    
    [Key(5)]
    public LatLong LowerRight { get; init; }

    public override string ToString()
    {
        return $"[{Path}, {Row}]: (UL: {UpperLeft}) (UR: {UpperRight}) (LL: {LowerLeft}) (LR: {LowerRight})";
    }
}