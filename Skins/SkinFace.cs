using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace UnrealFrontiersLauncher;

public static class SkinFace
{
    private const int Size = 8;
    private static readonly Rect FaceSource = new(8, 8, Size, Size);
    private static readonly Rect HatSource = new(40, 8, Size, Size);
    private static readonly Rect Destination = new(0, 0, Size, Size);

    /// <summary>
    /// The face of a skin PNG as an 8x8 picture: the face square (8,8) with the hat layer (40,8) over it.
    /// Show it scaled with BitmapInterpolationMode.None so every skin pixel stays a sharp square.
    /// </summary>
    /// <returns>Null when the picture is not a skin (too small to hold the face).</returns>
    public static Bitmap? Compose(Stream png)
    {
        using var skin = new Bitmap(png);
        if (skin.PixelSize.Width < 48 || skin.PixelSize.Height < 16) return null;

        var face = new RenderTargetBitmap(new PixelSize(Size, Size));
        using (var context = face.CreateDrawingContext())
        {
            context.DrawImage(skin, FaceSource, Destination);
            context.DrawImage(skin, HatSource, Destination);
        }
        return face;
    }
}
