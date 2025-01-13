using System;
using Avalonia.Media;

public static class BrushExtensions
{
    public static IBrush TryGetDarkerBrush(this IBrush brush, double factor)
    {
        if (brush is SolidColorBrush solidColorBrush)
        {
            var color = solidColorBrush.Color;
            return new SolidColorBrush(color.WithAdjustedBrightness(factor));
        }

        return brush;
    }

    public static Color WithAdjustedBrightness(this Color color, double factor)
    {
        byte Adjust(byte channel) => (byte)Math.Clamp(channel / factor, 0, 255);
        return new Color(color.A, Adjust(color.R), Adjust(color.G), Adjust(color.B));
    }
}