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
    public const int SentBackByStrangeForceAddress = 0xcdde;
    internal const int SeedTreeRefillLocationCount = 16;
    internal const int SeedTreeRefillRoomsPerLocation = 8;

    private readonly byte[] _wram = new byte[WramEnd - WramStart + 1];
    private readonly byte[] _seedTreeRefillRooms =
        new byte[SeedTreeRefillLocationCount * SeedTreeRefillRoomsPerLocation];

    public event Action? Changed;

    public OracleRuntimeState()
    {
        // initializeSeedTreeRefillData starts Ages with bits $04-$0f set and
        // clears the sixteen eight-room refill histories.
        _wram[SeedTreeRefilledBitsetAddress - WramStart] = 0xf0;
        _wram[SeedTreeRefilledBitsetAddress + 1 - WramStart] = 0xff;
    }

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

    internal byte ReadSeedTreeRefillRoom(int index, int slot)
    {
        ValidateSeedTreeRefillPosition(index, slot);
        return _seedTreeRefillRooms[
            index * SeedTreeRefillRoomsPerLocation + slot];
    }

    internal void SetSeedTreeRefillRoom(int index, int slot, byte room)
    {
        ValidateSeedTreeRefillPosition(index, slot);
        _seedTreeRefillRooms[
            index * SeedTreeRefillRoomsPerLocation + slot] = room;
    }

    internal void ClearSeedTreeRefillRooms(int index)
    {
        ValidateSeedTreeRefillPosition(index, 0);
        Array.Clear(
            _seedTreeRefillRooms,
            index * SeedTreeRefillRoomsPerLocation,
            SeedTreeRefillRoomsPerLocation);
    }

    internal OracleRuntimeStateSnapshot CaptureState() => new(
        (byte[])_wram.Clone(),
        (byte[])_seedTreeRefillRooms.Clone());

    internal void RestoreState(OracleRuntimeStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot.Wram);
        ArgumentNullException.ThrowIfNull(snapshot.SeedTreeRefillRooms);
        if (snapshot.Wram.Length != _wram.Length ||
            snapshot.SeedTreeRefillRooms.Length != _seedTreeRefillRooms.Length)
        {
            throw new ArgumentException(
                "The runtime-state snapshot does not match the Ages WRAM layout.",
                nameof(snapshot));
        }

        snapshot.Wram.CopyTo(_wram, 0);
        snapshot.SeedTreeRefillRooms.CopyTo(_seedTreeRefillRooms, 0);
        Changed?.Invoke();
    }

    private static void ValidateAddress(int address)
    {
        if (address is < WramStart or > WramEnd)
            throw new ArgumentOutOfRangeException(nameof(address));
    }

    private static void ValidateSeedTreeRefillPosition(int index, int slot)
    {
        if (index is < 0 or >= SeedTreeRefillLocationCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (slot is < 0 or >= SeedTreeRefillRoomsPerLocation)
            throw new ArgumentOutOfRangeException(nameof(slot));
    }
}

internal readonly record struct OracleRuntimeStateSnapshot(
    byte[] Wram,
    byte[] SeedTreeRefillRooms);
