using System;

namespace oracleofages;

/// <summary>
/// Original WRAM state outside the serialized $c5b0-$caff file image. These
/// values live for one gameplay session and are intentionally not saved.
/// </summary>
public sealed class OracleRuntimeState
{
    public const int WramStart = 0xc000;
    public const int WramEnd = 0xdfff;
    public const int SeedTreeRefilledBitsetAddress = 0xcc4d;
    public const int MamamuDogLocationAddress = 0xcde2;
    // Ages WRAM addresses (the shared labels are $cdd2-$cdd4 in Seasons).
    public const int ToggleBlocksStateAddress = 0xcc31;
    public const int SwitchStateAddress = 0xcc32;
    public const int SpinnerStateAddress = 0xcc33;

    private readonly byte[] _wram = new byte[WramEnd - WramStart + 1];

    public event Action? Changed;

    public byte ReadWramByte(int address)
    {
        ValidateAddress(address);
        return _wram[address - WramStart];
    }

    public void SetWramByte(int address, byte value)
    {
        ValidateAddress(address);
        int offset = address - WramStart;
        if (_wram[offset] == value)
            return;
        _wram[offset] = value;
        Changed?.Invoke();
    }

    private static void ValidateAddress(int address)
    {
        if (address is < WramStart or > WramEnd)
            throw new ArgumentOutOfRangeException(nameof(address));
    }
}
