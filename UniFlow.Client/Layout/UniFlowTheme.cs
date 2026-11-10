using MudBlazor;

namespace UniFlow.Client.Layout;

public static class UniFlowTheme
{
    private const string Burgundy = "#7B1E3B";

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Burgundy,
            AppbarBackground = Burgundy,
            Background = "#F5F5F7"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        },
        Typography = new Typography
        {
            // Inter is served from wwwroot/fonts: nothing is downloaded from external servers
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Segoe UI", "Arial", "sans-serif"]
            },
            H5 = new H5Typography
            {
                FontWeight = "600"
            },
            H6 = new H6Typography
            {
                FontWeight = "600"
            },
            // Flat style: normal letters instead of UPPERCASE on buttons
            Button = new ButtonTypography
            {
                FontWeight = "600",
                TextTransform = "none",
                LetterSpacing = "normal"
            }
        }
    };
}