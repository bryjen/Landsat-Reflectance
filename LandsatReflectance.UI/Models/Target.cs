namespace LandsatReflectance.UI.Models;

public class Target
{
    public Guid UserId { get; set; }
    public Guid Id { get; set; }
    public int Path { get; set; }
    public int Row { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double MinCloudCoverFilter { get; set; }
    public double MaxCloudCoverFilter { get; set; }
    public TimeSpan NotificationOffset { get; set; }
}