namespace oracleofages;

// getRandomNumber and generateRandomBuffer from bank 0 / bank 2. Enemy RNG
// is deliberately shared: the original engine does not give each object its
// own random stream.
internal sealed class OracleRandom
{
    internal readonly record struct Result(byte Value, byte High, byte Low);

    private byte _rng1 = 0x37;
    private byte _rng2 = 0x0d;
    private readonly byte[] _placementBuffer;
    private byte _placementIndex;

    public OracleRandom()
    {
        _placementBuffer = GenerateInitialBuffer();
    }

    public Result Next()
    {
        int original = (_rng2 << 8) | _rng1;
        int multiplied = (original * 3) & 0xffff;
        byte high = (byte)(multiplied >> 8);
        byte low = (byte)multiplied;
        _rng2 = high;
        _rng1 = (byte)(high + _rng1);
        return new Result(_rng1, high, low);
    }

    public byte NextPlacementValue()
    {
        _placementIndex++;
        return _placementBuffer[_placementIndex];
    }

    private byte[] GenerateInitialBuffer()
    {
        var buffer = new byte[256];
        for (int index = 0; index < buffer.Length; index++)
            buffer[index] = (byte)index;

        Swap(buffer, 0xff, Next().Value);
        for (int current = 0xff; current > 0; current--)
        {
            int randomIndex = (Next().Value * current) >> 8;
            Swap(buffer, current, randomIndex);
        }
        return buffer;
    }

    private static void Swap(byte[] values, int first, int second)
    {
        (values[first], values[second]) = (values[second], values[first]);
    }
}
