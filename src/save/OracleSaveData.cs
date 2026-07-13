using System;
using System.Buffers.Binary;

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
    public const int GlobalFlagMakuTreeDisappeared = 0x0c;
    public const int GlobalFlagSavedNayru = 0x11;
    public const int GlobalFlagMakuTreeSaved = 0x12;

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
        save.WriteWramByte(0xc6b1, 0x10);
        save.WriteWramByte(0xc6aa, 0x0c);
        save.WriteWramByte(0xc6ab, 0x0c);
        save.WriteWramByte(0xc63e, 0xff);
        save.WriteWramByte(0xc6e9, 0x21);
        save.WriteWramByte(0xc6ec, 0xb6);
        save.WriteWramByte(0xc6ed, 0x48);
        save.WriteWramByte(0xc6ee, 0x48);
        save.WriteWramByte(0xc6ef, 0x02);

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
