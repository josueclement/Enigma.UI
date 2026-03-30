using Enigma.IconGenerator;
using PhosphorIconsAvalonia;

// Generate a multi-resolution .ico file from Phosphor "key" icon (fill style, white)
SvgToIcoGenerator.FromPhosphorIcon(
    icon: Icon.key,
    iconType: IconType.fill,
    outputPath: "key-fill.ico",
    hexColor: "FFFFFF");

// Also generate a 512px PNG file
SvgToIcoGenerator.PngFromPhosphorIcon(
    icon: Icon.key,
    iconType: IconType.fill,
    outputPath: "key-fill-512.png",
    size: 512,
    hexColor: "FFFFFF");

// In-memory ICO generation (no file written)
using var icoStream = SvgToIcoGenerator.IcoFromPhosphorIcon(
    icon: Icon.key,
    iconType: IconType.fill,
    hexColor: "FFFFFF");
Console.WriteLine($"\nIn-memory ICO: {icoStream.Length:N0} bytes");
Console.WriteLine("  Usage in Avalonia: mainWindow.Icon = new WindowIcon(icoStream);");
