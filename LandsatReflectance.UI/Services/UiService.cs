﻿using MudBlazor;

namespace LandsatReflectance.UI.Services;

/// Keeps track of variables across different web pages

public class UiService
{
    private MudTheme? m_mudTheme;
    public MudTheme MudTheme
    {
        get
        {
            m_mudTheme ??= new()
            {
                PaletteLight = _lightPalette, 
                PaletteDark = _darkPalette, 
                LayoutProperties = new LayoutProperties(),
                Typography = m_typography
            };
            return m_mudTheme;
        }
        set => m_mudTheme = value;
    }

    public bool IsDarkMode { get; set; } = true;

    public bool IsDrawerExpanded { get; set; } = true;


    
    

    private readonly Typography m_typography = new()
    {
        Default = new Default
        {
            FontFamily = [ "JetBrains Mono", "monospace" ]
        }
    };
    
    private readonly PaletteLight _lightPalette = new()
    {
        Black = "#110e2d",
        AppbarText = "#424242",
        AppbarBackground = "rgba(255,255,255,0.8)",
        DrawerBackground = "#ffffff",
        GrayLight = "#e8e8e8",
        GrayLighter = "#f9f9f9",
    };

    private readonly PaletteDark _darkPalette = new()
    {
        Primary = "#7e6fff",
        Surface = "#1e1e2d",
        Background = "#151b23",
        BackgroundGray = "#151521",
        AppbarText = "#92929f",
        AppbarBackground = "#151b23",
        DrawerBackground = "#151b23",
        ActionDefault = "#74718e",
        ActionDisabled = "#9999994d",
        ActionDisabledBackground = "#605f6d4d",
        TextPrimary = "#b2b0bf",
        TextSecondary = "#92929f",
        TextDisabled = "#ffffff33",
        DrawerIcon = "#92929f",
        DrawerText = "#92929f",
        GrayLight = "#2a2833",
        GrayLighter = "#1e1e2d",
        Info = "#4a86ff",
        Success = "#3dcb6c",
        Warning = "#ffb545",
        Error = "#ff3f5f",
        LinesDefault = "#33323e",
        TableLines = "#33323e",
        Divider = "#292838",
        OverlayLight = "#1e1e2d80",
    };
}