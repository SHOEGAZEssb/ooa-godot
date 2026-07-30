using System;

namespace oracleofages;

// getRandomNumber and generateRandomBuffer from bank 0 / bank 2. Enemy RNG
// is deliberately shared: the original engine does not give each object its
// own random stream.
internal sealed class OracleRandom
{

    private byte _rng1 = 0x37;
    private byte _rng2 = 0x0d;
    private readonly byte[] _placementBuffer = new byte[256];
    private byte _placementIndex;
    private bool _placementBufferReady;

    internal int Calls { get; private set; }
    internal OracleRandomResult LastResult { get; private set; }

    public OracleRandomResult Next()
    {
        int original = (_rng2 << 8) | _rng1;
        int multiplied = (original * 3) & 0xffff;
        byte high = (byte)(multiplied >> 8);
        byte low = (byte)multiplied;
        _rng2 = high;
        _rng1 = (byte)(high + _rng1);
        LastResult = new OracleRandomResult(_rng1, high, low);
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
        byte[] permutation = GeneratePermutation();
        permutation.CopyTo(_placementBuffer, 0);
        _placementIndex = 0;
        _placementBufferReady = true;
    }

    internal byte[] GeneratePermutation()
    {
        var permutation = new byte[256];
        for (int index = 0; index < permutation.Length; index++)
            permutation[index] = (byte)index;

        Swap(permutation, 0xff, Next().Value);
        for (int current = 0xff; current > 0; current--)
        {
            int randomIndex = (Next().Value * current) >> 8;
            Swap(permutation, current, randomIndex);
        }
        return permutation;
    }

    internal OracleRandomState CaptureState() => new(
        _rng1,
        _rng2,
        (byte[])_placementBuffer.Clone(),
        _placementIndex,
        _placementBufferReady,
        Calls,
        LastResult);

    internal void RestoreState(OracleRandomState state)
    {
        ArgumentNullException.ThrowIfNull(state.PlacementBuffer);
        if (state.PlacementBuffer.Length != _placementBuffer.Length)
        {
            throw new ArgumentException(
                "The RNG snapshot does not contain the 256-byte placement buffer.",
                nameof(state));
        }
        if (state.Calls < 0)
            throw new ArgumentOutOfRangeException(nameof(state));

        _rng1 = state.Rng1;
        _rng2 = state.Rng2;
        state.PlacementBuffer.CopyTo(_placementBuffer, 0);
        _placementIndex = state.PlacementIndex;
        _placementBufferReady = state.PlacementBufferReady;
        Calls = state.Calls;
        LastResult = state.LastResult;
    }

    private static void Swap(byte[] values, int first, int second)
    {
        (values[first], values[second]) = (values[second], values[first]);
    }
}

internal readonly record struct OracleRandomState(
    byte Rng1,
    byte Rng2,
    byte[] PlacementBuffer,
    byte PlacementIndex,
    bool PlacementBufferReady,
    int Calls,
    OracleRandomResult LastResult);

internal readonly record struct OracleRandomResult(byte Value, byte High, byte Low);
