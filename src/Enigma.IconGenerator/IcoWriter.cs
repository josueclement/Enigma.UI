namespace Enigma.IconGenerator;

/// <summary>
/// Writes multi-resolution ICO files from PNG image data.
/// </summary>
public static class IcoWriter
{
    public static void Write(Stream output, IReadOnlyList<byte[]> pngImages, IReadOnlyList<int> sizes)
    {
        if (pngImages.Count != sizes.Count)
            throw new ArgumentException("pngImages and sizes must have the same length.");

        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);

        // ICO header (6 bytes)
        writer.Write((ushort)0);                    // Reserved
        writer.Write((ushort)1);                    // Type: 1 = ICO
        writer.Write((ushort)pngImages.Count);      // Number of images

        // Calculate data offset: header (6) + directory entries (16 each)
        var dataOffset = 6 + 16 * pngImages.Count;

        // Write directory entries (16 bytes each)
        for (var i = 0; i < pngImages.Count; i++)
        {
            var size = sizes[i];
            var pngData = pngImages[i];

            writer.Write((byte)(size >= 256 ? 0 : size));  // Width (0 = 256)
            writer.Write((byte)(size >= 256 ? 0 : size));  // Height (0 = 256)
            writer.Write((byte)0);                          // Color palette count
            writer.Write((byte)0);                          // Reserved
            writer.Write((ushort)1);                        // Color planes
            writer.Write((ushort)32);                       // Bits per pixel
            writer.Write((uint)pngData.Length);             // Image data size
            writer.Write((uint)dataOffset);                 // Offset to image data

            dataOffset += pngData.Length;
        }

        // Write PNG data
        foreach (var pngData in pngImages)
        {
            writer.Write(pngData);
        }
    }

    public static void Write(string outputPath, IReadOnlyList<byte[]> pngImages, IReadOnlyList<int> sizes)
    {
        using var stream = File.Create(outputPath);
        Write(stream, pngImages, sizes);
    }
}
