namespace LandsatReflectance.UI.Models;

public class SceneData
{
    public string? BrowseName { get; set; } = string.Empty;
    public string? BrowsePath { get; set; } = string.Empty;
    public string? OverlayPath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; } = string.Empty;
    public Metadata Metadata { get; set; } = new();
}

public class Metadata
{
    public string EntityId { get; set; } = string.Empty;
    public string DisplayId { get; set; } = string.Empty;
    public DateTimeOffset PublishDate { get; set; }
    public string? L1ProductId { get; set; }
    public string? L2ProductId { get; set; }
    public float? L1CloudCover { get; set; }
    public int? CloudCoverInt { get; set; }
    public int? Satellite { get; set; }
}