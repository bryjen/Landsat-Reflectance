using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Serilog;

namespace LandsatReflectance.SceneBoundaries;

class Program
{
    [RequiresAssemblyFiles("Calls System.Reflection.Assembly.Location")]
    static void Main(string[] _)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
        
        var assemblyDir = Directory.GetParent(Assembly.GetExecutingAssembly().Location)?.FullName ?? string.Empty;
        var kmlFilePath = Path.Combine(assemblyDir, "Data", "WRS-2_bound_world_0.kml").Replace("\\", "/");

        var outputDirectory = Path.Join(assemblyDir, "out");
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        
        var dir = KmlConverter.Convert(depth: 2, outputDirectory, kmlFilePath);
        if (dir is not null)
        {
            Log.Information(dir);
        }
    }
}