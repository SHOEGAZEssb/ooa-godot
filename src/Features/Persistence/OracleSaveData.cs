using System.Buffers.Binary;
using System.Text;
using static Oracles.OracleSaveLayout;

namespace Oracles;

/// <summary>
/// The original $550-byte file image copied between WRAM $c5b0-$caff and
/// cartridge SRAM. Keeping the original offsets lets gameplay systems share
/// the same flag and inventory semantics without inventing parallel state.
/// </summary>
public sealed partial class OracleSaveData
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



    private readonly byte[] _data;
    internal OracleGame Game { get; }
    private int FlagOffset => Game == OracleGame.Seasons ? SeasonsSaveLayout.GlobalFlagsOffset : AgesSaveLayout.GlobalFlagsOffset;
    private ReadOnlySpan<byte> FileSignature => Game == OracleGame.Seasons ? SeasonsSaveLayout.VerificationString : AgesSaveLayout.VerificationString;
    private int _mutationDepth;
    private bool _mutationDirty;

    public event Action? Changed;

    public int MinimapGroup => ReadField("wMinimapGroup");
    public int MakuMapTextPresent => ReadField("wMakuMapTextPresent");

    public void SetMakuMapTextPresent(int textLow)
    {
        if (textLow is < 0 or > 0xff) throw new ArgumentOutOfRangeException(nameof(textLow));
        if (WriteField("wMakuMapTextPresent", (byte)textLow)) PublishChange();
    }
    public int MinimapRoom => ReadField("wMinimapRoom");
    public int MinimapDungeonPosition => ReadField("wMinimapDungeonMapPosition");
    public int MinimapDungeonFloor => ReadField("wMinimapDungeonFloor");
    public byte DungeonVisitedFloors(int dungeon) => ReadField("wDungeonVisitedFloors", dungeon);
    public string ChildName => ReadName("wKidName", 6);
    public bool ChildNamed => (ReadField("wc6e2") & 0x01) != 0;
    public string LinkName
    {
        get => ReadName("wLinkName", 5);
    }
    public int MaxHealthQuarters => ReadField("wLinkMaxHealth");
    public int DeathCount => ReadField("wDeathCounter", 1) * 100 +
        FromBcd(ReadField("wDeathCounter"));
    public int TextSpeed => ReadField("wTextSpeed");
    public int RespawnGroup => ReadField("wDeathRespawnBuffer");
    public int RespawnRoom => ReadField("wDeathRespawnBuffer.room");
    public int RespawnStateModifier => ReadField("wDeathRespawnBuffer.stateModifier");
    public int RespawnFacing => ReadField("wDeathRespawnBuffer.facingDir") & 0x03;
    public int RespawnY => ReadField("wDeathRespawnBuffer.y");
    public int RespawnX => ReadField("wDeathRespawnBuffer.x");
    internal byte LastAnimalMountY => ReadField("wLastAnimalMountPointY");
    internal byte LastAnimalMountX => ReadField("wLastAnimalMountPointX");
    public bool IsLinkedGame => ReadField("wFileIsLinkedGame") != 0;
    public bool IsCompleted => ReadField("wFileIsCompleted") != 0;
    internal bool HasFinishedGame => HasGlobalFlag(Game == OracleGame.Ages ? AgesGlobalFlag.FinishedGame : SeasonsGlobalFlag.FinishedGame);
    internal void MarkRingSecretGenerated() => SetGlobalFlag(Game == OracleGame.Ages ? AgesGlobalFlag.RingSecretGenerated : SeasonsGlobalFlag.RingSecretGenerated);
    public int MapleKillCounter => ReadField("wMapleKillCounter");
    public int MapleState => ReadField("wMapleState");
    public int GashaMaturity =>
        ReadField("wGashaMaturity") | (ReadField("wGashaMaturity", 1) << 8);
    internal bool HasHarvestedFirstGashaNut => (ReadField("wGashaSpotFlags") & 0x01) != 0;
    internal bool HasHarvestedGashaHeartPiece => (ReadField("wGashaSpotFlags") & 0x02) != 0;

    private OracleSaveData(byte[] data, OracleGame game)
    {
        _data = data;
        Game = game;
    }

    internal static OracleSaveData CreateStandardGame(OracleGame game) => game switch
    {
        OracleGame.Ages => CreateAgesGame([]),
        OracleGame.Seasons => CreateSeasonsGame(),
        _ => throw new ArgumentOutOfRangeException(nameof(game))
    };

    private static OracleSaveData CreateSeasonsGame()
    {
        var save = new OracleSaveData(new byte[FileSize], OracleGame.Seasons);
        save.ApplyInitialFileVariables("common");
        save.ApplyInitialFileVariables("standardGame");
        Array.Fill(save._data, (byte)0xff, UnappraisedRingsOffset, 0x40);
        for (int index = 0; index < 5; index++) save.WriteField("wRingBoxContents", 0xff, index);
        save.WriteField("wActiveRing", 0xff);
        SeasonsSaveLayout.VerificationString.CopyTo(save._data.AsSpan(VerificationOffset));
        return save;
    }

    private void ApplyInitialFileVariables(string variant)
    {
        int index = 0;
        string root = OracleAssetPack.ForGame(Game);
        foreach (var row in GeneratedTable.Load(root + "metadata/new_file_initialization.tsv",
            new("New file initialization", GeneratedTableKeySemantics.Ordered,
                ["variant", "index", "address", "value", "source"], headerRequired: true)).Rows)
        {
            string sourceVariant = row.RequiredString(0);
            if (sourceVariant is not ("common" or "standardGame" or "heroGame" or "linkedGame"))
                throw row.Invalid(0, "a fileManagement.s initialization variant");
            if (sourceVariant != variant) continue;
            int address = row.HexWord(2);
            if (row.Decimal(1) != index++ || address is < 0xc600 or > 0xc6ff)
                throw row.Invalid(2, "ordered fileManagement.s initialization writes to $c6xx");
            WriteWramByte(address, (byte)row.HexByte(3));
        }
        if (index != (variant switch { "common" => Game == OracleGame.Seasons ? 13 : 19, "standardGame" => 5, "heroGame" => 3, "linkedGame" => 6, _ => 0 }))
            throw new InvalidOperationException($"fileManagement.s: incomplete initialFileVariables {variant}.");
    }

    public bool HasGlobalFlag(int flag)
    {
        ValidateGlobalFlag(flag);
        return (_data[FlagOffset + flag / 8] & (1 << (flag & 7))) != 0;
    }

    public bool HasTreasure(int treasure)
    {
        if (treasure is < 0 or >= 0x80)
            throw new ArgumentOutOfRangeException(nameof(treasure));
        return (ReadField("wObtainedTreasureFlags", treasure / 8) &
            (1 << (treasure & 7))) != 0;
    }

    public void SetGlobalFlag(int flag, bool value = true)
    {
        ValidateGlobalFlag(flag);
        SetMask(FlagOffset + flag / 8, (byte)(1 << (flag & 7)), value);
    }

    public byte GetRoomFlags(int group, int room)
    {
        ValidateRoom(group, room);
        return _data[GetRoomFlagTableOffset(group) + room];
    }

    public bool HasRoomFlag(int group, int room, byte mask) =>
        (GetRoomFlags(group, room) & mask) != 0;

    internal void AddRoomFlagByte(int group, int room, int value)
    {
        ValidateRoom(group, room);
        int offset = GetRoomFlagTableOffset(group) + room;
        byte next = unchecked((byte)(_data[offset] + value));
        if (_data[offset] == next) return;
        _data[offset] = next;
        PublishChange();
    }

    public void SetRoomFlag(int group, int room, byte mask, bool value = true)
    {
        ValidateRoom(group, room);
        SetMask(GetRoomFlagTableOffset(group) + room, mask, value);
    }

    public void SetMinimapLocation(int group, int room)
    {
        ValidateRoom(group, room);
        bool changed = WriteField("wMinimapGroup", (byte)group);
        changed |= WriteField("wMinimapRoom", (byte)room);
        if (changed)
            PublishChange();
    }

    public void SetLinkedGame(bool linked)
    {
        if (WriteField("wFileIsLinkedGame", linked ? (byte)0x01 : (byte)0x00))
            PublishChange();
    }

    public void SetLinkName(string name)
    {
        Span<byte> encoded = stackalloc byte[6];
        EncodeName(name, encoded, nameof(name));
        if (WriteFields("wLinkName", encoded))
            PublishChange();
    }

    public void NameChild(string name)
    {
        bool changed = WriteChildName(name);
        changed |= WriteField("wChildStatus", InitialChildStatus());
        changed |= WriteField("wc6e2", (byte)(ReadField("wc6e2") | 0x01));
        changed |= WriteField("wNextChildStage", 0x01);
        if (changed) PublishChange();
    }

    internal void SetChildName(string name)
    {
        if (WriteChildName(name)) PublishChange();
    }

    private bool WriteChildName(string name)
    {
        Span<byte> encoded = stackalloc byte[6];
        EncodeName(name, encoded, nameof(name));
        return WriteFields("wKidName", encoded);
    }

    internal void DecideInitialChildStatus()
    {
        if (WriteField("wChildStatus", InitialChildStatus())) PublishChange();
    }

    private byte InitialChildStatus()
    {
        int lowNibbleSum = 0;
        for (int index = 0; ReadField("wKidName", index) != 0; index++)
            lowNibbleSum = (lowNibbleSum + (ReadField("wKidName", index) & 0x0f)) & 0xff;
        return (byte)(lowNibbleSum % 3 + 1);
    }

    public void SetTextSpeed(int speed)
    {
        if (speed is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(speed));
        if (WriteField("wTextSpeed", (byte)speed))
            PublishChange();
    }

    // tryToBreakTile_body calls incHlRefWithCap for TILEINDEX_SIGN.
    internal void RecordSignDestroyed()
    {
        if (WriteField("wTotalSignsDestroyed",
            (byte)Math.Min(0xff, ReadField("wTotalSignsDestroyed") + 1)))
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
        bool changed = WriteField("wGashaMaturity", (byte)next);
        changed |= WriteField("wGashaMaturity", (byte)(next >> 8), 1);
        if (changed)
            PublishChange();
    }

    internal void SubtractGashaMaturity(int amount)
    {
        if (amount is < 0 or > 0xffff)
            throw new ArgumentOutOfRangeException(nameof(amount));
        int next = Math.Max(0, GashaMaturity - amount);
        bool changed = WriteField("wGashaMaturity", (byte)next);
        changed |= WriteField("wGashaMaturity", (byte)(next >> 8), 1);
        if (changed)
            PublishChange();
    }

    internal bool IsGashaSpotPlanted(int subId)
    {
        ValidateGashaSpot(subId);
        return (ReadField("wGashaSpotFlags", 1 + subId / 8) & (1 << (subId & 7))) != 0;
    }

    internal void SetGashaSpotPlanted(int subId, bool planted)
    {
        ValidateGashaSpot(subId);
        int offset = 1 + subId / 8;
        int mask = 1 << (subId & 7);
        byte value = ReadField("wGashaSpotFlags", offset);
        value = planted ? (byte)(value | mask) : (byte)(value & ~mask);
        if (WriteField("wGashaSpotFlags", value, offset))
            PublishChange();
    }

    internal int GetGashaSpotKillCounter(int subId)
    {
        ValidateGashaSpot(subId);
        return ReadField("wGashaSpotKillCounters", subId);
    }

    internal void SetGashaSpotKillCounter(int subId, int count)
    {
        ValidateGashaSpot(subId);
        if (count is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (WriteField("wGashaSpotKillCounters", (byte)count, subId))
            PublishChange();
    }

    internal void SetGashaHarvestFlag(int bit)
    {
        if (bit is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(bit));
        byte value = (byte)(ReadField("wGashaSpotFlags") | (1 << bit));
        if (WriteField("wGashaSpotFlags", value))
            PublishChange();
    }

    internal void SetMapleKillCounter(int value)
    {
        if (value is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (WriteField("wMapleKillCounter", (byte)value))
            PublishChange();
    }

    internal void SetMapleState(int value)
    {
        if (value is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (WriteField("wMapleState", (byte)value))
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

        bool changed = WriteField("wDeathRespawnBuffer", (byte)group);
        changed |= WriteField("wDeathRespawnBuffer.room", (byte)room);
        changed |= WriteField("wDeathRespawnBuffer.stateModifier", (byte)stateModifier);
        changed |= WriteField("wDeathRespawnBuffer.facingDir", (byte)facing);
        changed |= WriteField("wDeathRespawnBuffer.y", (byte)y);
        changed |= WriteField("wDeathRespawnBuffer.x", (byte)x);
        if (rememberedCompanion is { } state)
        {
            changed |= WriteField(
                "wDeathRespawnBuffer.rememberedCompanionId",
                checked((byte)state.Companion.Id));
            changed |= WriteField(
                "wDeathRespawnBuffer.rememberedCompanionGroup",
                checked((byte)state.Companion.Group));
            changed |= WriteField(
                "wDeathRespawnBuffer.rememberedCompanionRoom",
                checked((byte)state.Companion.Room));
            changed |= WriteField(
                "wDeathRespawnBuffer.linkObjectIndex",
                checked((byte)state.LinkObjectIndex));
            changed |= WriteField(
                "wDeathRespawnBuffer.rememberedCompanionY",
                checked((byte)state.Companion.Y));
            changed |= WriteField(
                "wDeathRespawnBuffer.rememberedCompanionX",
                checked((byte)state.Companion.X));
        }
        if (changed)
            PublishChange();
    }

    public void IncrementDeathCount()
    {
        int next = Math.Min(999, DeathCount + 1);
        bool changed = WriteField("wDeathCounter", ToBcd(next % 100));
        changed |= WriteField("wDeathCounter", (byte)(next / 100), 1);
        if (changed)
            PublishChange();
    }

    internal void AdvancePlaytime()
    {
        // bank0.s:mainThread increments the live four-byte little-endian
        // counter before game dispatch, including cutscenes and menus.
        for (int offset = 0; offset < 4; offset++)
        {
            byte next = unchecked((byte)(ReadField("wPlaytimeCounter", offset) + 1));
            WriteField("wPlaytimeCounter", next, offset);
            if (next != 0) break;
        }
    }

    internal void ResetHealthIfDepleted()
    {
        byte health = ReadField("wLinkHealth");
        if (health != 0 && (health & 0x80) == 0)
            return;
        // bank1.initializeGame: SRL / AND $fc rounds half the maximum to
        // whole hearts; CP $0c imposes a three-heart minimum after depletion.
        byte restored = (byte)Math.Max(0x0c, (ReadField("wLinkMaxHealth") >> 1) & 0xfc);
        if (WriteField("wLinkHealth", restored))
            PublishChange();
    }

    internal void SetLastAnimalMountPoint(byte y, byte x)
    {
        // companionFinalizeMounting writes the shared Y/X pair inside the
        // live $c5b0-$caff image. Only an explicit save copies it to disk.
        bool changed = WriteField("wLastAnimalMountPointY", y);
        changed |= WriteField("wLastAnimalMountPointX", x);
        if (changed) PublishChange();
    }

    public byte[] Serialize()
    {
        byte[] output = (byte[])_data.Clone();
        FileSignature.CopyTo(output.AsSpan(VerificationOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(
            output.AsSpan(ChecksumOffset, sizeof(ushort)), CalculateChecksum(output));
        return output;
    }

    internal static bool TryDeserialize(ReadOnlySpan<byte> source, OracleGame expectedGame, out OracleSaveData? save)
    {
        save = null;
        if (source.Length != FileSize)
        {
            return false;
        }
        var signature = source.Slice(VerificationOffset, 8);
        OracleGame game;
        if (signature.SequenceEqual(AgesSaveLayout.VerificationString)) game = OracleGame.Ages;
        else if (signature.SequenceEqual(SeasonsSaveLayout.VerificationString)) game = OracleGame.Seasons;
        else return false;
        if (game != expectedGame) return false;

        ushort stored = BinaryPrimitives.ReadUInt16LittleEndian(
            source.Slice(ChecksumOffset, sizeof(ushort)));
        if (stored != CalculateChecksum(source))
            return false;

        save = new OracleSaveData(source.ToArray(), game);
        return true;
    }

    internal void RestoreFrom(OracleSaveData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Game != Game) throw new ArgumentException("Cannot restore another game's save layout.", nameof(source));
        source._data.CopyTo(_data, 0);
        PublishChange();
    }

    internal byte ReadWramByte(int address) => _data[OffsetForWram(address)];

    internal byte ReadField(string symbol, int offset = 0) =>
        OracleWramLayout.Shared.Value.Resolve(Game, symbol, offset) is int actual ? ReadWramByte(actual) : (byte)0;

    internal Span<byte> FieldBytes(string symbol, int count) =>
        _data.AsSpan(OffsetForWram(OracleWramLayout.Shared.Value.Require(Game, symbol)), count);

    internal bool WriteField(string symbol, byte value, int offset = 0)
    {
        if (OracleWramLayout.Shared.Value.Resolve(Game, symbol, offset) is int actual) return WriteWramByte(actual, value);
        if (value != 0) throw new NotSupportedException($"include/wram.s:{symbol} is absent in {Game}.");
        return false; // The absent game's item level remains explicitly unowned.
    }

    internal void ReadFields(string symbol, Span<byte> destination, int offset = 0)
    {
        for (int index = 0; index < destination.Length; index++) destination[index] = ReadField(symbol, offset + index);
    }

    internal bool WriteFields(string symbol, ReadOnlySpan<byte> source, int offset = 0)
    {
        bool changed = false;
        for (int index = 0; index < source.Length; index++) changed |= WriteField(symbol, source[index], offset + index);
        return changed;
    }

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

    internal void ApplyDeathRespawnPreset(byte mask, ReadOnlySpan<byte> values)
    {
        if (values.Length != 7 || (mask & 1) != 0)
            throw new ArgumentException("bank1.loadDeathRespawnBufferPreset requires seven bytes and a terminating mask bit.");
        bool changed = false;
        for (int offset = 0; offset < values.Length; offset++)
            if ((mask & (0x80 >> offset)) != 0)
                changed |= WriteField("wDeathRespawnBuffer", values[offset], offset);
        if (changed) PublishChange();
    }

    private string ReadName(string symbol, int byteCount)
    {
        Span<byte> name = stackalloc byte[byteCount];
        ReadFields(symbol, name);
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
