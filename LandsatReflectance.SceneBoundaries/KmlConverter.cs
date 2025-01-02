using System.Xml;

namespace LandsatReflectance.SceneBoundaries;

/// <summary>
/// Converts the input <code>.kml</code> file into a compressed set of region-partitioned binary files.
/// </summary>
public class KmlConverter
{
    public static string? Convert(string kmlFilePath)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(kmlFilePath);

        var xmlNamespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
        xmlNamespaceManager.AddNamespace("kml", "http://www.opengis.net/kml/2.2");

        _ = xmlDoc.SelectNodes("//kml:Placemark", xmlNamespaceManager);
        
        
        
        return null;
    }
}