using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace OracleOfAges.Importer;

/// <summary>Writes opaque, 8-bit grayscale atlases without a platform image API.</summary>
public static class GeneratedGrayscalePng
{
    public static void Write(string path, int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.LongLength != (long)width * height)
            throw new InvalidDataException($"PNG '{path}': invalid {width}x{height} grayscale buffer ({pixels.Length} bytes).");

        using var compressed = new MemoryStream();
        // Stored DEFLATE blocks avoid platform-dependent compression decisions.
        // Atlases are small; identical bytes across Windows/Linux take priority.
        using (var zlib = new ZLibStream(compressed, CompressionLevel.NoCompression, leaveOpen: true))
        {
            for (int y = 0; y < height; y++)
            {
                zlib.WriteByte(0); // PNG filter: None.
                zlib.Write(pixels.AsSpan(y * width, width));
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var output = File.Create(path);
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Span<byte> header = stackalloc byte[13];
        header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8; // Bit depth; color type 0 is opaque grayscale.
        WriteChunk(output, "IHDR", header);
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> integer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(integer, data.Length);
        output.Write(integer);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        uint crc = UpdateCrc(uint.MaxValue, typeBytes);
        crc = UpdateCrc(crc, data) ^ uint.MaxValue;
        BinaryPrimitives.WriteUInt32BigEndian(integer, crc);
        output.Write(integer);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }
        return crc;
    }
}
