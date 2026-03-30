using Enigma.IconGenerator;
using PhosphorIconsAvalonia;

// Generate a multi-resolution .ico from Phosphor "key" icon (fill style, white)
SvgToIcoGenerator.FromPhosphorIcon(
    icon: Icon.key,
    iconType: IconType.fill,
    outputPath: "key-fill.ico",
    hexColor: "FFFFFF");

// Also generate a 512px PNG
SvgToIcoGenerator.PngFromPhosphorIcon(
    icon: Icon.key,
    iconType: IconType.fill,
    outputPath: "key-fill-512.png",
    size: 512,
    hexColor: "FFFFFF");
