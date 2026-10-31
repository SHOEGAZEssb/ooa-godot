using System;

namespace Oracles;

/// <summary>
/// Original WRAM state outside the serialized $c5b0-$caff file image. These
/// values live for one gameplay session and are intentionally not saved.
/// </summary>
public sealed class OracleRuntimeState
{
    internal OracleGame Game { get; }
    private readonly int _objectsDisabledAddress;
    private readonly int _menuDisabledAddress;
    private readonly int _screenTransitionsDisabledAddress;
    private readonly int _linkCollisionsAndMenuDisabledAddress;
    internal ulong ObjectsDisabledWriteVersion { get; private set; }
    internal ulong MenuDisabledWriteVersion { get; private set; }
    internal ulong ScreenTransitionsDisabledWriteVersion { get; private set; }
    internal ulong LinkCollisionsAndMenuDisabledWriteVersion { get; private set; }
    // Clock ticks do not invalidate NPC story-state bindings.
    internal byte FrameCounter
    {
        get => _wram[FieldAddress("wFrameCounter") - WramStart];
        set => _wram[FieldAddress("wFrameCounter") - WramStart] = value;
    }
    internal static OracleRuntimeState CreateInitialized(OracleGame game)
    {
        var state = game switch
        {
            OracleGame.Ages => AgesGameContent.CreateContentRuntimeState(),
            OracleGame.Seasons => SeasonsGameContent.CreateContentRuntimeState(),
            _ => throw new ArgumentOutOfRangeException(nameof(game))
        };
        NativeObjectStreamDatabase.For(OracleAssetPack.ForGame(game)).InitializeLoadFlags(state);
        // clearMemoryOnScreenReload initializes the active Link page to w1Link.
        state.SetField("wLinkObjectIndex", 0xd0);
        return state;
    }
    internal OracleRuntimeState(OracleGame game)
    {
        _ = OracleAssetPack.ForGame(game);
        Game = game;
        _objectsDisabledAddress = FieldAddress("wDisabledObjects");
        _menuDisabledAddress = FieldAddress("wMenuDisabled");
        _screenTransitionsDisabledAddress = FieldAddress("wDisableScreenTransitions");
        _linkCollisionsAndMenuDisabledAddress=FieldAddress("wDisableLinkCollisionsAndMenu");
    }
    internal byte ReadField(string symbol, int offset = 0) => OracleWramLayout.Shared.Value.Resolve(Game, symbol, offset) is int address ? ReadWramByte(address) : (byte)0;
    internal int FieldAddress(string symbol, int offset = 0) => OracleWramLayout.Shared.Value.Require(Game, symbol, offset);
    internal void SetField(string symbol, byte value, int offset = 0)
    {
        if (OracleWramLayout.Shared.Value.Resolve(Game, symbol, offset) is int address) SetWramByte(address, value);
        else if (value != 0) throw new NotSupportedException($"include/wram.s:{symbol} is absent in {Game}.");
    }

    internal void ClearRoomReloadGlobals()
    {
        // bank1.clearMemoryOnScreenReload: the two builds have different spans.
        ClearSourceRange("wLinkInAir", "wcce9");
        if (ReadField("wLinkObjectIndex") != 0xd1) SetField("wLinkObjectIndex", 0xd0);
        SetField("wCameraFocusedObject", ReadField("wLinkObjectIndex"));
        SetField("wCameraFocusedObjectType", 0);
        SetField("wccaa", 0xff);
    }

    internal void ClearScrollEntryGlobals()
    {
        // bank1.func_49c9 preserves earlier Link/carry/Maple globals.
        ClearSourceRange("wDisabledObjects", "wcce1");
        SetField("wccaa", 0xff);
    }

    private void ClearSourceRange(string first, string end)
    {
        int limit = FieldAddress(end);
        for (int address = FieldAddress(first); address < limit; address++) SetWramByte(address, 0);
    }
    public const int WramStart = 0xc000;
    public const int WramEnd = 0xdfff;
    internal const int SeedTreeRefillLocationCount = 16;
    internal const int SeedTreeRefillRoomsPerLocation = 8;

    private readonly byte[] _wram = new byte[WramEnd - WramStart + 1];
    private readonly byte[] _seedTreeRefillRooms =
        new byte[SeedTreeRefillLocationCount * SeedTreeRefillRoomsPerLocation];

    public event Action? Changed;



    public byte ReadWramByte(int address)
    {
        ValidateAddress(address);
        return _wram[address - WramStart];
    }

    public void SetWramByte(int address, byte value)
    {
        ValidateAddress(address);
        // Cancellation must observe later writers even when the byte is unchanged.
        if (address == _objectsDisabledAddress) ObjectsDisabledWriteVersion++;
        if (address == _menuDisabledAddress) MenuDisabledWriteVersion++;
        if (address == _screenTransitionsDisabledAddress) ScreenTransitionsDisabledWriteVersion++;
        if(address==_linkCollisionsAndMenuDisabledAddress)LinkCollisionsAndMenuDisabledWriteVersion++;
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
        Game,
        (byte[])_wram.Clone(),
        (byte[])_seedTreeRefillRooms.Clone());

    internal void RestoreState(OracleRuntimeStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot.Wram);
        ArgumentNullException.ThrowIfNull(snapshot.SeedTreeRefillRooms);
        if (snapshot.Game != Game || snapshot.Wram.Length != _wram.Length ||
            snapshot.SeedTreeRefillRooms.Length != _seedTreeRefillRooms.Length)
        {
            throw new ArgumentException(
                "The runtime-state snapshot does not match this game's WRAM layout.",
                nameof(snapshot));
        }

        snapshot.Wram.CopyTo(_wram, 0);
        ObjectsDisabledWriteVersion++;
        MenuDisabledWriteVersion++;
        ScreenTransitionsDisabledWriteVersion++;
        LinkCollisionsAndMenuDisabledWriteVersion++;
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
    OracleGame Game,
    byte[] Wram,
    byte[] SeedTreeRefillRooms);
