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

    public const int ChildFlagsAddress = WramAddress.wc6e2;

    internal const int RespawnRememberedCompanionIdAddress = 0xc631;
    internal const int RespawnRememberedCompanionGroupAddress = 0xc632;
    internal const int RespawnRememberedCompanionRoomAddress = 0xc633;
    internal const int RespawnLinkObjectIndexAddress = 0xc634;
    internal const int RespawnRememberedCompanionYAddress = 0xc636;
    internal const int RespawnRememberedCompanionXAddress = 0xc637;

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
    private int _mutationDepth;
    private bool _mutationDirty;

    public event Action? Changed;

    public int MinimapGroup => ReadWramByte(WramAddress.wMinimapGroup);
    public int MinimapRoom => ReadWramByte(WramAddress.wMinimapRoom);
    public int MinimapDungeonPosition => ReadWramByte(WramAddress.wMinimapDungeonMapPosition);
    public int MinimapDungeonFloor => ReadWramByte(WramAddress.wMinimapDungeonFloor);
    public byte DungeonVisitedFloors(int dungeon) => ReadWramByte(0xc662 + dungeon);
    public int TimePortalGroup => ReadWramByte(WramAddress.wPortalGroup);
    public int TimePortalRoom => ReadWramByte(WramAddress.wPortalRoom);
    public int TimePortalPosition => ReadWramByte(WramAddress.wPortalPos);
    public int MakuTreeState => ReadWramByte(WramAddress.wMakuTreeState);
    public int MakuMapTextPresent => ReadWramByte(WramAddress.wMakuMapTextPresent);
    public int MakuMapTextPast => ReadWramByte(WramAddress.wMakuMapTextPast);
    public int MakuTreeSeedSatchelXPosition => ReadWramByte(WramAddress.wMakuTreeSeedSatchelXPosition);
    public string ChildName => ReadName(WramAddress.wKidName, 6);
    public bool ChildNamed => (ReadWramByte(ChildFlagsAddress) & 0x01) != 0;
    public string LinkName
    {
        get => ReadName(0xc602, 5);
    }
    public int MaxHealthQuarters => ReadWramByte(WramAddress.wLinkMaxHealth);
    public int DeathCount => ReadWramByte(0xc61f) * 100 +
        FromBcd(ReadWramByte(WramAddress.wDeathCounter));
    public int TextSpeed => ReadWramByte(WramAddress.wTextSpeed);
    public int RespawnGroup => ReadWramByte(WramAddress.wDeathRespawnBuffer);
    public int RespawnRoom => ReadWramByte(0xc62c);
    public int RespawnStateModifier => ReadWramByte(0xc62d);
    public int RespawnFacing => ReadWramByte(0xc62e) & 0x03;
    public int RespawnY => ReadWramByte(0xc62f);
    public int RespawnX => ReadWramByte(0xc630);
    public bool IsLinkedGame => ReadWramByte(WramAddress.wFileIsLinkedGame) != 0;
    public bool IsCompleted => ReadWramByte(WramAddress.wFileIsCompleted) != 0;
    public int MapleKillCounter => ReadWramByte(WramAddress.wMapleKillCounter);
    public int MapleState => ReadWramByte(WramAddress.wMapleState);
    public int GashaMaturity =>
        ReadWramByte(WramAddress.wGashaMaturity) | (ReadWramByte(0xc660) << 8);
    internal bool HasHarvestedFirstGashaNut => (ReadWramByte(WramAddress.wGashaSpotFlags) & 0x01) != 0;
    internal bool HasHarvestedGashaHeartPiece => (ReadWramByte(WramAddress.wGashaSpotFlags) & 0x02) != 0;

    private OracleSaveData(byte[] data)
    {
        _data = data;
        new VineSproutDatabase().InitializeMissingPositions(this);
    }

    public static OracleSaveData CreateStandardGame()
    {
        var save = new OracleSaveData(new byte[FileSize]);

        // fileManagement.initializeFile fills these separately from the
        // standard-game overrides in initialFileVariables_standardGame.
        Array.Fill(save._data, (byte)0xff, UnappraisedRingsOffset, 0x40);
        VerificationString.CopyTo(save._data, VerificationOffset);
        save.WriteWramByte(WramAddress.wc608, 0x01);
        save.WriteWramByte(WramAddress.wTextSpeed, 0x04);
        // initialFileVariables: Ages begins at 0:8a, facing up at $38/$48.
        save.WriteWramByte(WramAddress.wDeathRespawnBuffer, 0x00);
        save.WriteWramByte(0xc62c, 0x8a);
        save.WriteWramByte(0xc62e, 0x00);
        save.WriteWramByte(0xc62f, 0x38);
        save.WriteWramByte(0xc630, 0x48);
        save.WriteWramByte(WramAddress.wMaxBombs, 0x10);
        save.WriteWramByte(WramAddress.wLinkHealth, 0x0c);
        save.WriteWramByte(WramAddress.wLinkMaxHealth, 0x0c);
        save.WriteWramByte(WramAddress.wPortalGroup, 0xff);
        save.WriteWramByte(WramAddress.wJabuWaterLevel, 0x21);
        save.WriteWramByte(WramAddress.wPirateShipRoom, 0xb6);
        save.WriteWramByte(WramAddress.wPirateShipY, 0x48);
        save.WriteWramByte(WramAddress.wPirateShipX, 0x48);
        save.WriteWramByte(WramAddress.wPirateShipAngle, 0x02);
        for (int address = 0xc6c6; address <= 0xc6cb; address++)
            save.WriteWramByte(address, 0xff);

        int punchFlagAddress = 0xc69a + TreasureId.Punch / 8;
        save.WriteWramByte(
            punchFlagAddress,
            (byte)(1 << (TreasureId.Punch & 7)));
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
        bool changed = WriteWramByte(WramAddress.wMinimapGroup, (byte)group);
        changed |= WriteWramByte(WramAddress.wMinimapRoom, (byte)room);
        if (changed)
            PublishChange();
    }

    public void SetTimePortalLocation(int group, int room) =>
        SetTimePortalLocation(group, room, TimePortalPosition);

    public void SetTimePortalLocation(int group, int room, int position)
    {
        if (group is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(group));
        if (room is < 0 or >= RoomsPerFlagTable)
            throw new ArgumentOutOfRangeException(nameof(room));
        if (position is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(position));

        bool changed = WriteWramByte(WramAddress.wPortalGroup, (byte)group);
        changed |= WriteWramByte(WramAddress.wPortalRoom, (byte)room);
        changed |= WriteWramByte(WramAddress.wPortalPos, (byte)position);
        if (changed)
            PublishChange();
    }

    public void ClearTimePortalLocation()
    {
        if (WriteWramByte(WramAddress.wPortalGroup, 0xff))
            PublishChange();
    }

    public void SetMakuTreeState(int state)
    {
        if (state is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(state));
        if (WriteWramByte(WramAddress.wMakuTreeState, (byte)state))
            PublishChange();
    }

    public void SetMakuMapTextPast(int textLow)
    {
        if (textLow is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(textLow));
        if (WriteWramByte(WramAddress.wMakuMapTextPast, (byte)textLow))
            PublishChange();
    }

    public void SetMakuMapTextPresent(int textLow)
    {
        if (textLow is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(textLow));
        if (WriteWramByte(WramAddress.wMakuMapTextPresent, (byte)textLow))
            PublishChange();
    }

    public void SetMakuTreeSeedSatchelXPosition(int x)
    {
        if (x is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(x));
        if (WriteWramByte(WramAddress.wMakuTreeSeedSatchelXPosition, (byte)x))
            PublishChange();
    }

    public void SetLinkedGame(bool linked)
    {
        if (WriteWramByte(WramAddress.wFileIsLinkedGame, linked ? (byte)0x01 : (byte)0x00))
            PublishChange();
    }

    public void SetLinkName(string name)
    {
        Span<byte> encoded = stackalloc byte[6];
        EncodeName(name, encoded, nameof(name));
        if (WriteWramBytes(WramAddress.wLinkName, encoded))
            PublishChange();
    }

    public void NameChild(string name)
    {
        Span<byte> encoded = stackalloc byte[6];
        string normalized = EncodeName(name, encoded, nameof(name));
        bool changed = WriteWramBytes(WramAddress.wKidName, encoded);

        int lowNibbleSum = 0;
        for (int index = 0; index < normalized.Length; index++)
            lowNibbleSum += encoded[index] & 0x0f;
        changed |= WriteWramByte(
            WramAddress.wChildStatus, (byte)(lowNibbleSum % 3 + 1));
        changed |= WriteWramByte(
            ChildFlagsAddress, (byte)(ReadWramByte(ChildFlagsAddress) | 0x01));
        changed |= WriteWramByte(WramAddress.wNextChildStage, 0x01);
        if (changed)
            PublishChange();
    }

    public void SetTextSpeed(int speed)
    {
        if (speed is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(speed));
        if (WriteWramByte(WramAddress.wTextSpeed, (byte)speed))
            PublishChange();
    }

    /// <summary>
    /// Mirrors addToGashaMaturity's little-endian add and $ffff saturation.
    /// </summary>
    public void AddGashaMaturity(int amount)
    {
        if (amount is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(amount));
        int next = Math.Min(0xffff, GashaMaturity + amount);
        bool changed = WriteWramByte(WramAddress.wGashaMaturity, (byte)next);
        changed |= WriteWramByte(0xc660, (byte)(next >> 8));
        if (changed)
            PublishChange();
    }

    internal void SubtractGashaMaturity(int amount)
    {
        if (amount is < 0 or > 0xffff)
            throw new ArgumentOutOfRangeException(nameof(amount));
        int next = Math.Max(0, GashaMaturity - amount);
        bool changed = WriteWramByte(WramAddress.wGashaMaturity, (byte)next);
        changed |= WriteWramByte(0xc660, (byte)(next >> 8));
        if (changed)
            PublishChange();
    }

    internal bool IsGashaSpotPlanted(int subId)
    {
        ValidateGashaSpot(subId);
        return (ReadWramByte(0xc64d + subId / 8) & (1 << (subId & 7))) != 0;
    }

    internal void SetGashaSpotPlanted(int subId, bool planted)
    {
        ValidateGashaSpot(subId);
        int address = 0xc64d + subId / 8;
        int mask = 1 << (subId & 7);
        byte value = ReadWramByte(address);
        value = planted ? (byte)(value | mask) : (byte)(value & ~mask);
        if (WriteWramByte(address, value))
            PublishChange();
    }

    internal int GetGashaSpotKillCounter(int subId)
    {
        ValidateGashaSpot(subId);
        return ReadWramByte(0xc64f + subId);
    }

    internal void SetGashaSpotKillCounter(int subId, int count)
    {
        ValidateGashaSpot(subId);
        if (count is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (WriteWramByte(0xc64f + subId, (byte)count))
            PublishChange();
    }

    internal void SetGashaHarvestFlag(int bit)
    {
        if (bit is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(bit));
        byte value = (byte)(ReadWramByte(WramAddress.wGashaSpotFlags) | (1 << bit));
        if (WriteWramByte(WramAddress.wGashaSpotFlags, value))
            PublishChange();
    }

    internal void SetMapleKillCounter(int value)
    {
        if (value is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (WriteWramByte(WramAddress.wMapleKillCounter, (byte)value))
            PublishChange();
    }

    internal void SetMapleState(int value)
    {
        if (value is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (WriteWramByte(WramAddress.wMapleState, (byte)value))
            PublishChange();
    }

    private static void ValidateGashaSpot(int subId)
    {
        if (subId is < 0 or >= 16)
            throw new ArgumentOutOfRangeException(nameof(subId));
    }

    public void SetDeathRespawnPoint(
        int group, int room, int stateModifier, int facing, int y, int x)
    {
        SetDeathRespawnPointCore(
            group, room, stateModifier, facing, y, x,
            rememberedCompanion: null);
    }

    internal void SetDeathRespawnPoint(
        int group,
        int room,
        int stateModifier,
        int facing,
        int y,
        int x,
        RememberedCompanion rememberedCompanion,
        int linkObjectIndex)
    {
        if (linkObjectIndex is not (0xd0 or 0xd1))
            throw new ArgumentOutOfRangeException(nameof(linkObjectIndex));
        SetDeathRespawnPointCore(
            group, room, stateModifier, facing, y, x,
            (rememberedCompanion, linkObjectIndex));
    }

    private void SetDeathRespawnPointCore(
        int group,
        int room,
        int stateModifier,
        int facing,
        int y,
        int x,
        (RememberedCompanion Companion, int LinkObjectIndex)? rememberedCompanion)
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

        bool changed = WriteWramByte(WramAddress.wDeathRespawnBuffer, (byte)group);
        changed |= WriteWramByte(0xc62c, (byte)room);
        changed |= WriteWramByte(0xc62d, (byte)stateModifier);
        changed |= WriteWramByte(0xc62e, (byte)facing);
        changed |= WriteWramByte(0xc62f, (byte)y);
        changed |= WriteWramByte(0xc630, (byte)x);
        if (rememberedCompanion is { } state)
        {
            changed |= WriteWramByte(
                RespawnRememberedCompanionIdAddress,
                checked((byte)state.Companion.Id));
            changed |= WriteWramByte(
                RespawnRememberedCompanionGroupAddress,
                checked((byte)state.Companion.Group));
            changed |= WriteWramByte(
                RespawnRememberedCompanionRoomAddress,
                checked((byte)state.Companion.Room));
            changed |= WriteWramByte(
                RespawnLinkObjectIndexAddress,
                checked((byte)state.LinkObjectIndex));
            changed |= WriteWramByte(
                RespawnRememberedCompanionYAddress,
                checked((byte)state.Companion.Y));
            changed |= WriteWramByte(
                RespawnRememberedCompanionXAddress,
                checked((byte)state.Companion.X));
        }
        if (changed)
            PublishChange();
    }

    public void IncrementDeathCount()
    {
        int next = Math.Min(999, DeathCount + 1);
        bool changed = WriteWramByte(WramAddress.wDeathCounter, ToBcd(next % 100));
        changed |= WriteWramByte(0xc61f, (byte)(next / 100));
        if (changed)
            PublishChange();
    }

    internal void ResetHealthIfDepleted()
    {
        byte health = ReadWramByte(WramAddress.wLinkHealth);
        if (health != 0 && (health & 0x80) == 0)
            return;
        if (WriteWramByte(WramAddress.wLinkHealth, ReadWramByte(WramAddress.wLinkMaxHealth)))
            PublishChange();
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

    internal void RestoreFrom(OracleSaveData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source._data.CopyTo(_data, 0);
        PublishChange();
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

    internal IDisposable BeginMutation()
    {
        _mutationDepth++;
        return new MutationScope(this);
    }

    internal void CommitInventoryChange() => PublishChange();

    private void PublishChange()
    {
        if (_mutationDepth != 0)
        {
            _mutationDirty = true;
            return;
        }
        Changed?.Invoke();
    }

    private void EndMutation()
    {
        if (_mutationDepth <= 0)
            throw new InvalidOperationException("Save mutation scopes must be disposed exactly once.");

        _mutationDepth--;
        if (_mutationDepth != 0 || !_mutationDirty)
            return;

        _mutationDirty = false;
        Changed?.Invoke();
    }

    private void SetMask(int offset, byte mask, bool value)
    {
        byte previous = _data[offset];
        byte next = value ? (byte)(previous | mask) : (byte)(previous & ~mask);
        if (next == previous)
            return;
        _data[offset] = next;
        PublishChange();
    }

    private static ushort CalculateChecksum(ReadOnlySpan<byte> data)
    {
        uint checksum = 0;
        for (int offset = 2; offset < FileSize; offset += 2)
            checksum += BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
        return (ushort)checksum;
    }

    private static int FromBcd(byte value) => (value >> 4) * 10 + (value & 0x0f);

    private static byte ToBcd(int value) =>
        (byte)((value / 10 << 4) | value % 10);

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

    private sealed class MutationScope : IDisposable
    {
        private OracleSaveData? _owner;

        public MutationScope(OracleSaveData owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            OracleSaveData? owner = _owner;
            if (owner is null)
                return;

            _owner = null;
            owner.EndMutation();
        }
    }
}
