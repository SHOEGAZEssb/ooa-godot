using System;
using System.Buffers.Binary;
using System.Text;

namespace oracleofages;

/// <summary>
/// The original $550-byte file image copied between WRAM $c5b0-$caff and
/// cartridge SRAM. Keeping the original offsets lets gameplay systems share
/// the same flag and inventory semantics without inventing parallel state.
/// </summary>
public sealed class OracleSaveData
{
    public const int FileSize = 0x550;
    public const int GlobalFlagCount = 0x80;
    public const int RoomsPerFlagTable = 0x100;

    public const byte RoomFlagLayoutSwap = 0x01;
    public const byte RoomFlagPortalSpotDiscovered = 0x08;
    public const byte RoomFlagVisited = 0x10;
    public const byte RoomFlagItem = 0x20;
    public const byte RoomFlag40 = 0x40;
    public const byte RoomFlag80 = 0x80;

    public const int GlobalFlagIntroDone = 0x0a;
    public const int GlobalFlagWonFairyHidingGame = 0x0e;
    public const int GlobalFlagPregameIntroDone = 0x21;
    public const int GlobalFlagLinkSummoned = 0x3d;
    public const int GlobalFlagMakuTreeDisappeared = 0x0c;
    public const int GlobalFlagSavedNayru = 0x11;
    public const int GlobalFlagMakuTreeSaved = 0x12;
    public const int GlobalFlagSawTwinrovaBeforeEndgame = 0x13;
    public const int GlobalFlagFinishedGame = 0x14;
    public const int GlobalFlagSymmetryBridgeBuilt = 0x25;
    public const int GlobalFlagForestUnscrambled = 0x2b;
    public const int GlobalFlagPreBlackTowerCutsceneDone = 0x33;
    public const int GlobalFlagGotRingFromZelda = 0x38;
    public const int GlobalFlagFlameOfDespairLit = 0x3a;
    public const int GlobalFlagReturnedDog = 0x3b;
    public const int GlobalFlagRalphEnteredPortal = 0x40;
    public const int GlobalFlagEnterPastCutsceneDone = 0x41;
    public const int GlobalFlagRalphEnteredBlackTower = 0x45;

    public const int ChildNameAddress = 0xc609;
    public const int ChildStatusAddress = 0xc60f;
    public const int ChildStageAddress = 0xc6e0;
    public const int NextChildStageAddress = 0xc6e1;
    public const int ChildFlagsAddress = 0xc6e2;
    public const int ChildPersonalityAddress = 0xc6e4;

    private const int WramBase = 0xc5b0;
    private const int ChecksumOffset = 0x000;
    private const int VerificationOffset = 0x002;
    private const int UnappraisedRingsOffset = 0x010;
    private const int GlobalFlagsOffset = 0x120;
    private const int Group0RoomFlagsOffset = 0x150;
    private const int Group1RoomFlagsOffset = 0x250;
    private const int Group4RoomFlagsOffset = 0x350;
    private const int Group5RoomFlagsOffset = 0x450;
    private static readonly byte[] VerificationString = "Z21216-0"u8.ToArray();

    private readonly byte[] _data;

    public event Action? Changed;

    public int MinimapGroup => ReadWramByte(0xc63a);
    public int MinimapRoom => ReadWramByte(0xc63b);
    public int MakuTreeState => ReadWramByte(0xc6e8);
    public string ChildName => ReadName(ChildNameAddress, 6);
    public bool ChildNamed => (ReadWramByte(ChildFlagsAddress) & 0x01) != 0;
    public string LinkName
    {
        get => ReadName(0xc602, 5);
    }
    public int MaxHealthQuarters => ReadWramByte(0xc6ab);
    public int DeathCount => ReadWramByte(0xc61f) * 100 +
        FromBcd(ReadWramByte(0xc61e));
    public int TextSpeed => ReadWramByte(0xc629);
    public int RespawnGroup => ReadWramByte(0xc62b);
    public int RespawnRoom => ReadWramByte(0xc62c);
    public int RespawnStateModifier => ReadWramByte(0xc62d);
    public int RespawnFacing => ReadWramByte(0xc62e) & 0x03;
    public int RespawnY => ReadWramByte(0xc62f);
    public int RespawnX => ReadWramByte(0xc630);
    public bool IsLinkedGame => ReadWramByte(0xc612) != 0;
    public bool IsCompleted => ReadWramByte(0xc614) != 0;

    private OracleSaveData(byte[] data)
    {
        _data = data;
    }

    public static OracleSaveData CreateStandardGame()
    {
        var save = new OracleSaveData(new byte[FileSize]);

        // fileManagement.initializeFile fills these separately from the
        // standard-game overrides in initialFileVariables_standardGame.
        Array.Fill(save._data, (byte)0xff, UnappraisedRingsOffset, 0x40);
        VerificationString.CopyTo(save._data, VerificationOffset);
        save.WriteWramByte(0xc608, 0x01);
        save.WriteWramByte(0xc629, 0x04);
        // initialFileVariables: Ages begins at 0:8a, facing up at $38/$48.
        save.WriteWramByte(0xc62b, 0x00);
        save.WriteWramByte(0xc62c, 0x8a);
        save.WriteWramByte(0xc62e, 0x00);
        save.WriteWramByte(0xc62f, 0x38);
        save.WriteWramByte(0xc630, 0x48);
        save.WriteWramByte(0xc6b1, 0x10);
        save.WriteWramByte(0xc6aa, 0x0c);
        save.WriteWramByte(0xc6ab, 0x0c);
        save.WriteWramByte(0xc63e, 0xff);
        save.WriteWramByte(0xc6e9, 0x21);
        save.WriteWramByte(0xc6ec, 0xb6);
        save.WriteWramByte(0xc6ed, 0x48);
        save.WriteWramByte(0xc6ee, 0x48);
        save.WriteWramByte(0xc6ef, 0x02);
        for (int address = 0xc6c6; address <= 0xc6cb; address++)
            save.WriteWramByte(address, 0xff);

        int punchFlagAddress = 0xc69a + InventoryState.TreasurePunch / 8;
        save.WriteWramByte(
            punchFlagAddress,
            (byte)(1 << (InventoryState.TreasurePunch & 7)));
        return save;
    }

    public bool HasGlobalFlag(int flag)
    {
        ValidateGlobalFlag(flag);
        return (_data[GlobalFlagsOffset + flag / 8] & (1 << (flag & 7))) != 0;
    }

    public bool HasTreasure(int treasure)
    {
        if (treasure is < 0 or >= 0x80)
            throw new ArgumentOutOfRangeException(nameof(treasure));
        return (ReadWramByte(0xc69a + treasure / 8) &
            (1 << (treasure & 7))) != 0;
    }

    public void SetGlobalFlag(int flag, bool value = true)
    {
        ValidateGlobalFlag(flag);
        SetMask(GlobalFlagsOffset + flag / 8, (byte)(1 << (flag & 7)), value);
    }

    public byte GetRoomFlags(int group, int room)
    {
        ValidateRoom(group, room);
        return _data[GetRoomFlagTableOffset(group) + room];
    }

    public bool HasRoomFlag(int group, int room, byte mask) =>
        (GetRoomFlags(group, room) & mask) != 0;

    public void SetRoomFlag(int group, int room, byte mask, bool value = true)
    {
        ValidateRoom(group, room);
        SetMask(GetRoomFlagTableOffset(group) + room, mask, value);
    }

    public void SetMinimapLocation(int group, int room)
    {
        ValidateRoom(group, room);
        bool changed = WriteWramByte(0xc63a, (byte)group);
        changed |= WriteWramByte(0xc63b, (byte)room);
        if (changed)
            Changed?.Invoke();
    }

    public void SetMakuTreeState(int state)
    {
        if (state is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(state));
        if (WriteWramByte(0xc6e8, (byte)state))
            Changed?.Invoke();
    }

    public void SetLinkedGame(bool linked)
    {
        if (WriteWramByte(0xc612, linked ? (byte)0x01 : (byte)0x00))
            Changed?.Invoke();
    }

    public void SetLinkName(string name)
    {
        Span<byte> encoded = stackalloc byte[6];
        EncodeName(name, encoded, nameof(name));
        if (WriteWramBytes(0xc602, encoded))
            Changed?.Invoke();
    }

    public void NameChild(string name)
    {
        Span<byte> encoded = stackalloc byte[6];
        string normalized = EncodeName(name, encoded, nameof(name));
        bool changed = WriteWramBytes(ChildNameAddress, encoded);

        int lowNibbleSum = 0;
        for (int index = 0; index < normalized.Length; index++)
            lowNibbleSum += encoded[index] & 0x0f;
        changed |= WriteWramByte(
            ChildStatusAddress, (byte)(lowNibbleSum % 3 + 1));
        changed |= WriteWramByte(
            ChildFlagsAddress, (byte)(ReadWramByte(ChildFlagsAddress) | 0x01));
        changed |= WriteWramByte(NextChildStageAddress, 0x01);
        if (changed)
            Changed?.Invoke();
    }

    public void SetTextSpeed(int speed)
    {
        if (speed is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(speed));
        if (WriteWramByte(0xc629, (byte)speed))
            Changed?.Invoke();
    }

    public void SetDeathRespawnPoint(
        int group, int room, int stateModifier, int facing, int y, int x)
    {
        ValidateRoom(group, room);
        if (stateModifier is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(stateModifier));
        if (facing is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(facing));
        if (y is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(y));
        if (x is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(x));

        bool changed = WriteWramByte(0xc62b, (byte)group);
        changed |= WriteWramByte(0xc62c, (byte)room);
        changed |= WriteWramByte(0xc62d, (byte)stateModifier);
        changed |= WriteWramByte(0xc62e, (byte)facing);
        changed |= WriteWramByte(0xc62f, (byte)y);
        changed |= WriteWramByte(0xc630, (byte)x);
        if (changed)
            Changed?.Invoke();
    }

    public byte[] Serialize()
    {
        byte[] output = (byte[])_data.Clone();
        VerificationString.CopyTo(output, VerificationOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(
            output.AsSpan(ChecksumOffset, sizeof(ushort)), CalculateChecksum(output));
        return output;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> source, out OracleSaveData? save)
    {
        save = null;
        if (source.Length != FileSize ||
            !source.Slice(VerificationOffset, VerificationString.Length).SequenceEqual(VerificationString))
        {
            return false;
        }

        ushort stored = BinaryPrimitives.ReadUInt16LittleEndian(
            source.Slice(ChecksumOffset, sizeof(ushort)));
        if (stored != CalculateChecksum(source))
            return false;

        save = new OracleSaveData(source.ToArray());
        return true;
    }

    internal byte ReadWramByte(int address) => _data[OffsetForWram(address)];

    internal void ReadWramBytes(int address, Span<byte> destination) =>
        _data.AsSpan(OffsetForWram(address), destination.Length).CopyTo(destination);

    internal bool WriteWramByte(int address, byte value)
    {
        int offset = OffsetForWram(address);
        if (_data[offset] == value)
            return false;
        _data[offset] = value;
        return true;
    }

    internal bool WriteWramBytes(int address, ReadOnlySpan<byte> source)
    {
        Span<byte> destination = _data.AsSpan(OffsetForWram(address), source.Length);
        if (destination.SequenceEqual(source))
            return false;
        source.CopyTo(destination);
        return true;
    }

    internal void CommitInventoryChange() => Changed?.Invoke();

    private void SetMask(int offset, byte mask, bool value)
    {
        byte previous = _data[offset];
        byte next = value ? (byte)(previous | mask) : (byte)(previous & ~mask);
        if (next == previous)
            return;
        _data[offset] = next;
        Changed?.Invoke();
    }

    private static ushort CalculateChecksum(ReadOnlySpan<byte> data)
    {
        uint checksum = 0;
        for (int offset = 2; offset < FileSize; offset += 2)
            checksum += BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
        return (ushort)checksum;
    }

    private static int FromBcd(byte value) => (value >> 4) * 10 + (value & 0x0f);

    private string ReadName(int address, int byteCount)
    {
        Span<byte> name = stackalloc byte[byteCount];
        ReadWramBytes(address, name);
        int length = name.IndexOf((byte)0);
        if (length < 0)
            length = name.Length;
        return Encoding.ASCII.GetString(name[..length]).TrimEnd(' ');
    }

    private static string EncodeName(
        string name,
        Span<byte> destination,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(name);
        string normalized = name.TrimEnd(' ');
        if (normalized.Length is < 1 or > 5)
            throw new ArgumentOutOfRangeException(parameterName,
                "Original names contain one to five characters.");

        destination.Clear();
        for (int index = 0; index < normalized.Length; index++)
        {
            char character = normalized[index];
            if (character != ' ' &&
                (character < 'A' || character > 'Z') &&
                (character < 'a' || character > 'z'))
                throw new ArgumentException(
                    "Names support the original US uppercase and lowercase letters.",
                    parameterName);
            destination[index] = (byte)character;
        }
        return normalized;
    }

    private static int GetRoomFlagTableOffset(int group) => group switch
    {
        0 or 2 => Group0RoomFlagsOffset,
        1 or 3 => Group1RoomFlagsOffset,
        4 or 6 => Group4RoomFlagsOffset,
        5 or 7 => Group5RoomFlagsOffset,
        _ => throw new ArgumentOutOfRangeException(nameof(group),
            "Original room-flag groups are numbered $00-$07.")
    };

    private static int OffsetForWram(int address)
    {
        int offset = address - WramBase;
        if (offset < 0 || offset >= FileSize)
            throw new ArgumentOutOfRangeException(nameof(address),
                $"WRAM address ${address:x4} is outside save data $c5b0-$caff.");
        return offset;
    }

    private static void ValidateGlobalFlag(int flag)
    {
        if (flag is < 0 or >= GlobalFlagCount)
            throw new ArgumentOutOfRangeException(nameof(flag));
    }

    private static void ValidateRoom(int group, int room)
    {
        _ = GetRoomFlagTableOffset(group);
        if (room is < 0 or >= RoomsPerFlagTable)
            throw new ArgumentOutOfRangeException(nameof(room));
    }
}
