using Avalonia.Controls;
using Enigma.IconGenerator;
using PhosphorIconsAvalonia;

namespace Enigma.UI.Helpers;

public static class AppIconHelper
{
    public static WindowIcon CreateWindowIcon(
        Icon icon,
        IconType iconType = IconType.fill,
        string? hexColor = null)
    {
        using var stream = SvgToIcoGenerator.IcoFromPhosphorIcon(icon, iconType, hexColor: hexColor);
        return new WindowIcon(stream);
    }
}
