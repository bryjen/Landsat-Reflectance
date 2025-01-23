using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LandsatReflectance.UI.Components.Dialog;

public enum CloudCoverFilter
{
    CustomRange,
    TargetRange,
    None
}

public class DetailedViewSettingsModel
{
    public int Images { get; set; } = 10;
    public int Skip { get; set; } = 0;
    public double MinCloudCover { get; set; } = 0;
    public double MaxCloudCover { get; set; } = 1;

    public bool ShowLandsat8 { get; set; } = true;
    public bool ShowLandsat9 { get; set; } = true;
    
    /// <summary>
    /// The cloud cover range attached to the target. Null if no target is present.
    /// </summary>
    public (double Min, double Max)? TargetCloudCoverFilter { get; set; }

    public CloudCoverFilter CloudCoverFilter { get; set; } = CloudCoverFilter.None;
}

public partial class DetailedViewSettingsDialog : ComponentBase
{
    [Parameter]
    public required DetailedViewSettingsModel Model { get; set; }
    
    [Parameter]
    public required EventCallback<DetailedViewSettingsModel> OnDialogSubmit { get; set; } = EventCallback<DetailedViewSettingsModel>.Empty;
    
    
    [CascadingParameter] 
    private MudDialogInstance MudDialog { get; set; } = null!;


    protected override void OnParametersSet()
    {
        switch (Model.CloudCoverFilter)
        {
            case CloudCoverFilter.CustomRange:
                CustomRangeSelected = true;
                break;
            case CloudCoverFilter.TargetRange:
                TargetRangeSelected = true;
                break;
        }
    }


    private bool _customRangeSelected;
    private bool CustomRangeSelected
    {
        get => _customRangeSelected;
        set
        {
            if (value != _customRangeSelected)
            {
                TargetRangeSelected = false;
                _customRangeSelected = value;
                Model.CloudCoverFilter = Model.CloudCoverFilter is CloudCoverFilter.CustomRange ? CloudCoverFilter.None : CloudCoverFilter.CustomRange;
            }
        } 
    }
    
    private bool _targetRangeSelected;
    private bool TargetRangeSelected
    {
        get => _targetRangeSelected;
        set
        {
            if (value != _targetRangeSelected)
            {
                CustomRangeSelected = false;
                _targetRangeSelected = value;
                Model.CloudCoverFilter = Model.CloudCoverFilter is CloudCoverFilter.TargetRange ? CloudCoverFilter.None : CloudCoverFilter.TargetRange;
            }
        } 
    }


    private async Task Submit()
    { 
        MudDialog.Close();
        await OnDialogSubmit.InvokeAsync(Model);
    }
}