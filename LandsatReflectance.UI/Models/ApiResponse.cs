namespace LandsatReflectance.UI.Models;

public class ApiResponse<T>
{
    public Guid RequestGuid { get; set; }
    public string? ErrorMessage { get; set; }
    public T Data { get; set; } = default!;
}