using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace UnrealFrontiersLauncher;

/// <summary>One server of a network, shown under the network's row in the list.</summary>
public sealed class GroupServerRow
{
    public GroupServerRow(GroupServer server, Bitmap? icon)
    {
        Id = server.Id;
        Name = server.Name;
        Kind = server.Kind;
        HasKind = server.Kind.Length > 0;
        Online = server.MaxPlayers > 0 ? $"{server.Online}/{server.MaxPlayers}" : server.Online.ToString();
        StatusText = server.Up ? "Онлайн" : "Недоступен";
        StatusBrush = server.Up ? new SolidColorBrush(Color.Parse("#4CC24C")) : new SolidColorBrush(Color.Parse("#8A8A8A"));

        var accent = Readable(Color.TryParse(server.Color ?? "", out var color) ? color : Color.Parse("#00FFA3"));
        AccentBrush = new SolidColorBrush(accent);
        TileBackground = new SolidColorBrush(Color.FromArgb(0x2E, accent.R, accent.G, accent.B));

        Icon = icon;
        Initial = server.Name.Length > 0 ? char.ToUpperInvariant(server.Name[0]).ToString() : "?";

        Tip = server.Description.Length > 0 ? server.Description : null;
    }

    /// <summary>A dark server colour is lifted toward white, so its text stays legible on the dark card.</summary>
    private static Color Readable(Color color)
    {
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
        if (luminance >= 0.45) return color;
        static byte Lift(byte channel) => (byte)(channel + (255 - channel) * 0.45);
        return Color.FromRgb(Lift(color.R), Lift(color.G), Lift(color.B));
    }

    public string Id { get; }
    public string Name { get; }
    public string Kind { get; }
    public bool HasKind { get; }
    public string Online { get; }
    public string StatusText { get; }
    public IBrush StatusBrush { get; }
    public IBrush AccentBrush { get; }
    public IBrush TileBackground { get; }

    /// <summary>The server's own icon; without one the tile shows its first letter in its colour.</summary>
    public Bitmap? Icon { get; }
    public bool HasIcon => Icon != null;
    public string Initial { get; }

    public string? Tip { get; }
}
