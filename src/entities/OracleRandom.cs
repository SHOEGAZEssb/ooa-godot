namespace oracleofages;

// getRandomNumber and generateRandomBuffer from bank 0 / bank 2. Enemy RNG
// is deliberately shared: the original engine does not give each object its
// own random stream.
internal sealed class OracleRandom
{
    internal readonly record struct Result(byte Value, byte High, byte Low);

    private byte _rng1 = 0x37;
    private byte _rng2 = 0x0d;
    private readonly byte[] _placementBuffer = new byte[256];
    private byte _placementIndex;
    private bool _placementBufferReady;

    internal int Calls { get; private set; }
    internal Result LastResult { get; private set; }

    public Result Next()
    {
        int original = (_rng2 << 8) | _rng1;
        int multiplied = (original * 3) & 0xffff;
        byte high = (byte)(multiplied >> 8);
        byte low = (byte)multiplied;
        _rng2 = high;
        _rng1 = (byte)(high + _rng1);
        LastResult = new Result(_rng1, high, low);
        Calls++;
        return LastResult;
    }

    public byte NextPlacementValue()
    {
        if (!_placementBufferReady)
        {
            throw new System.InvalidOperationException(
                "Enemy placement requested before the room object list was parsed.");
        }

        _placementIndex = unchecked((byte)(_placementIndex + 1));
        return _placementBuffer[_placementIndex];
    }

    public void BeginRoomParse()
    {
        _placementIndex = 0;
        for (int index = 0; index < _placementBuffer.Length; index++)
            _placementBuffer[index] = (byte)index;

        Swap(_placementBuffer, 0xff, Next().Value);
        for (int current = 0xff; current > 0; current--)
        {
            int randomIndex = (Next().Value * current) >> 8;
            Swap(_placementBuffer, current, randomIndex);
        }
        _placementBufferReady = true;
    }

    private static void Swap(byte[] values, int first, int second)
    {
        (values[first], values[second]) = (values[second], values[first]);
    }
}
