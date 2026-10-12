using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using OracleOfAges.Importer;

internal static class GeneratedGrayscalePngTests
{
    public static void Run(string root)
    {
        string path = Path.Combine(root, "png ' [0]", "shades.png");
        GeneratedGrayscalePng.Write(path, 2, 2, new byte[] { 0, 85, 170, 255 });
        byte[] png = File.ReadAllBytes(path);
        Require(png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "PNG signature changed");
        int offset = 8;
        var chunks = new List<string>();
        using var compressed = new MemoryStream();
        while (offset < png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
            string type = Encoding.ASCII.GetString(png, offset + 4, 4);
            chunks.Add(type);
            ReadOnlySpan<byte> data = png.AsSpan(offset + 8, length);
            if (type == "IHDR")
            {
                // Independently specified PNG header: 2x2, 8-bit grayscale,
                // standard compression/filter methods, no interlace.
                Require(data.SequenceEqual(Convert.FromHexString("00000002000000020800000000")),
                    "PNG dimensions, bit depth or color type changed");
                Require(BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length)) == 0x57dd52f8,
                    "PNG IHDR CRC does not match the standard 2x2 grayscale fixture");
            }
            if (type == "IDAT")
                compressed.Write(data);
            offset += length + 12;
        }
        Require(offset == png.Length && chunks.SequenceEqual(new[] { "IHDR", "IDAT", "IEND" }),
            "PNG chunk boundaries or order changed");
        compressed.Position = 0;
        using var decoder = new ZLibStream(compressed, CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        decoder.CopyTo(decoded);
        Require(decoded.ToArray().SequenceEqual(new byte[] { 0, 0, 85, 0, 170, 255 }),
            "PNG scanline filters or grayscale values changed");
        GeneratedGrayscalePng.Write(path, 2, 2, new byte[] { 0, 85, 170, 255 });
        Require(File.ReadAllBytes(path).SequenceEqual(png), "PNG output is not deterministic");

        foreach (var dimensions in new[] { (0, 2), (2, -1), (2, 3), (int.MaxValue, int.MaxValue) })
        {
            bool rejected = false;
            try { GeneratedGrayscalePng.Write(path, dimensions.Item1, dimensions.Item2, new byte[4]); }
            catch (InvalidDataException exception) { rejected = exception.Message.Contains(path, StringComparison.Ordinal); }
            Require(rejected, "Malformed PNG dimensions/buffer lost their output-path diagnostic");
            Require(File.ReadAllBytes(path).SequenceEqual(png), "Rejected PNG request overwrote an existing asset");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
