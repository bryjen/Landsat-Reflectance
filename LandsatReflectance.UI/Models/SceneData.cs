namespace LandsatReflectance.UI.Models;

public class SceneData
{
    public string? BrowseName { get; set; } = string.Empty;
    public string? BrowsePath { get; set; } = string.Empty;
    public string? OverlayPath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; } = string.Empty;
    
    public string EntityId { get; set; } = string.Empty;
    public string DisplayId { get; set; } = string.Empty;
    public DateTimeOffset PublishDate { get; set; } = default;
}