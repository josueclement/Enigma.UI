using System.Reflection;
using PhosphorIconsAvalonia;
using SkiaSharp;
using Svg.Skia;

namespace Enigma.IconGenerator;

public static class SvgToIcoGenerator
{
    private static readonly int[] DefaultSizes = [16, 24, 32, 48, 64, 128, 256];

    /// <summary>
    /// Generates a multi-resolution .ico file from an SVG file.
    /// </summary>
    public static void FromSvgFile(string svgPath, string outputPath, int[]? sizes = null, string? hexColor = null)
    {
        var svgContent = File.ReadAllText(svgPath);

        if (hexColor is not null)
            svgContent = ApplyFillColor(svgContent, hexColor);

        var pngImages = RenderSvgAtSizes(svgContent, sizes ?? DefaultSizes);
        IcoWriter.Write(outputPath, pngImages, sizes ?? DefaultSizes);
    }

    /// <summary>
    /// Generates a multi-resolution .ico file from a PhosphorIconsAvalonia icon.
    /// </summary>
    public static void FromPhosphorIcon(
        Icon icon,
        IconType iconType = IconType.fill,
        string? outputPath = null,
        int[]? sizes = null,
        string? hexColor = null)
    {
        var iconName = ToResourceName(icon);
        var style = iconType.ToString();
        outputPath ??= $"{iconName}-{style}.ico";
        sizes ??= DefaultSizes;

        var svgContent = LoadPhosphorSvg(iconName, style);

        if (hexColor is not null)
            svgContent = ApplyFillColor(svgContent, hexColor);

        var pngImages = RenderSvgAtSizes(svgContent, sizes);
        IcoWriter.Write(outputPath, pngImages, sizes);

        var fileInfo = new FileInfo(outputPath);
        Console.WriteLine($"Generated: {fileInfo.FullName} ({fileInfo.Length:N0} bytes)");
        Console.WriteLine($"  Icon: {icon} ({iconType}), Sizes: {string.Join(", ", sizes)}px");
    }

    /// <summary>
    /// Generates a standalone PNG from an SVG file.
    /// </summary>
    public static void PngFromSvgFile(string svgPath, string outputPath, int size, string? hexColor = null)
    {
        var svgContent = File.ReadAllText(svgPath);

        if (hexColor is not null)
            svgContent = ApplyFillColor(svgContent, hexColor);

        var pngData = RenderSvgToPng(svgContent, size);
        File.WriteAllBytes(outputPath, pngData);
    }

    /// <summary>
    /// Generates a standalone PNG from a PhosphorIconsAvalonia icon.
    /// </summary>
    public static void PngFromPhosphorIcon(
        Icon icon,
        IconType iconType = IconType.fill,
        string? outputPath = null,
        int size = 512,
        string? hexColor = null)
    {
        var iconName = ToResourceName(icon);
        var style = iconType.ToString();
        outputPath ??= $"{iconName}-{style}-{size}.png";

        var svgContent = LoadPhosphorSvg(iconName, style);

        if (hexColor is not null)
            svgContent = ApplyFillColor(svgContent, hexColor);

        var pngData = RenderSvgToPng(svgContent, size);
        File.WriteAllBytes(outputPath, pngData);

        var fileInfo = new FileInfo(outputPath);
        Console.WriteLine($"Generated: {fileInfo.FullName} ({fileInfo.Length:N0} bytes)");
        Console.WriteLine($"  Icon: {icon} ({iconType}), Size: {size}px");
    }

    private static string ToResourceName(Icon icon) =>
        icon.ToString().Replace('_', '-');

    private static string LoadPhosphorSvg(string iconName, string style)
    {
        var assembly = Assembly.Load("PhosphorIconsAvalonia");
        var resourceName = $"PhosphorIconsAvalonia.Icons.{style}.{iconName}-{style}.svg";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ArgumentException(
                $"Icon '{iconName}' with style '{style}' not found. Resource: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ApplyFillColor(string svgContent, string hexColor)
    {
        if (!hexColor.StartsWith('#'))
            hexColor = "#" + hexColor;

        // Replace existing fill="currentColor" or fill="..." on the root <svg>, or add it
        if (System.Text.RegularExpressions.Regex.IsMatch(svgContent, @"<svg[^>]*\sfill=""[^""]*"""))
        {
            return System.Text.RegularExpressions.Regex.Replace(
                svgContent,
                @"(<svg[^>]*\s)fill=""[^""]*""",
                $"$1fill=\"{hexColor}\"");
        }

        return svgContent.Replace("<svg ", $"<svg fill=\"{hexColor}\" ");
    }

    private static List<byte[]> RenderSvgAtSizes(string svgContent, int[] sizes)
    {
        var pngImages = new List<byte[]>(sizes.Length);

        foreach (var size in sizes)
        {
            pngImages.Add(RenderSvgToPng(svgContent, size));
        }

        return pngImages;
    }

    private static byte[] RenderSvgToPng(string svgContent, int size)
    {
        using var svg = SKSvg.CreateFromSvg(svgContent);
        var picture = svg.Picture
            ?? throw new InvalidOperationException("Failed to parse SVG content.");

        using var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Transparent);

        // Scale the SVG to fit the target size
        var bounds = picture.CullRect;
        var scaleX = size / bounds.Width;
        var scaleY = size / bounds.Height;
        var scale = Math.Min(scaleX, scaleY);

        // Center the icon
        var offsetX = (size - bounds.Width * scale) / 2f;
        var offsetY = (size - bounds.Height * scale) / 2f;

        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
}
