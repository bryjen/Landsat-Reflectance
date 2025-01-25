using LandsatReflectance.UI.Services.Api;
using Microsoft.AspNetCore.Components;

namespace LandsatReflectance.UI.Components;

public partial class AddTarget : ComponentBase
{
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    
    [Inject]
    public required GeocodingService GeocodingService { get; set; }


    private ForwardGeocodingData? _selectedForwardGeocodingData;
    
    
    private async Task<IEnumerable<ForwardGeocodingData>> SearchForAddresses(string addressStr, CancellationToken cancellationToken)
    {
        return await GeocodingService.GetRelatedAddresses(addressStr, cancellationToken);
    }
}