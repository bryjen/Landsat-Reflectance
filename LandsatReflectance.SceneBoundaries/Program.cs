using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace LandsatReflectance.SceneBoundaries;

class Program
{
    [RequiresAssemblyFiles("Calls System.Reflection.Assembly.Location")]
    static void Main(string[] args)
    {
        var assemblyDir = Directory.GetParent(Assembly.GetExecutingAssembly().Location)?.FullName ?? string.Empty;
        var kmlFilePath = Path.Combine(assemblyDir, "Data", "WRS-2_bound_world_0.kml").Replace("\\", "/");
        KmlConverter.Convert(kmlFilePath);
    }
}