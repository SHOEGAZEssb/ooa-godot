using System;

namespace Oracles;

internal sealed class EraOrSeasonInfoDatabase
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<EraOrSeasonInfoDatabase>> Packs = new(StringComparer.Ordinal);
    internal static EraOrSeasonInfoDatabase For(string root) => Packs.GetOrAdd(root,
        static path => new Lazy<EraOrSeasonInfoDatabase>(() => new(path))).Value;
    internal string AssetRoot { get; }
    private readonly EraOrSeasonInfoRecord[] _records;
    private readonly string _variantSource, _displaySource;
    private readonly int _suppressFlag, _outdoorsMask, _largeIndoorsMask, _pastMask, _sentBackAddress;

    private EraOrSeasonInfoDatabase(string root)
    {
        AssetRoot = root;
        var profile = GeneratedTable.Load(root + "metadata/era_info.tsv",
            new("native era/season banner predicates",GeneratedTableKeySemantics.Ordered,
                ["variant-source","display-source","suppress-global-flag","outdoors-mask","large-indoors-mask","past-mask","sent-back-address","source"],headerRequired:true)).SingleRow();
        _variantSource = profile.RequiredString(0); _displaySource = profile.RequiredString(1);
        _suppressFlag = profile.HexByte(2); _outdoorsMask = profile.HexByte(3);
        _largeIndoorsMask = profile.HexByte(4); _pastMask = profile.HexByte(5); _sentBackAddress = profile.HexWord(6);
        if ((_variantSource,_displaySource) is not (("tileset-past","outdoors") or ("room-pack-season","group-zero")) ||
            (_sentBackAddress != 0 && _sentBackAddress is < 0xcc00 or >= 0xdf00))
            throw profile.Invalid(0,"a native era/season selection and full-room-load predicate");
        var table = GeneratedTable.Load(root + "effects/era_info.tsv",
            new("INTERAC_ERA_OR_SEASON_INFO presentation",GeneratedTableKeySemantics.Unique,
                ["subid","sprite","tile-base","palette","animation","start-y","start-x","enter-step","target-x","hold-updates","exit-step","exit-updates","source"],
                ["subid"],headerRequired:true));
        int count = _variantSource == "tileset-past" ? 2 : 4;
        if (table.Rows.Count != count) throw new InvalidOperationException($"{root}: incomplete native era/season variants.");
        _records = new EraOrSeasonInfoRecord[count];
        for (int index = 0; index < count; index++)
        {
            var row = table.Rows[index];
            if (row.HexByte(0) != index) throw row.Invalid(0,"ordered native banner subids");
            _records[index] = new(root,index,row.RequiredString(1),row.HexByte(2),row.Decimal(3,0,7),row.RequiredString(4),
                row.HexByte(5),row.HexByte(6),row.Decimal(7,1,255),row.HexByte(8),row.Decimal(9,1,255),
                row.Decimal(10,1,255),row.Decimal(11,1,255),row.RequiredString(12));
        }
    }

    internal bool TryDisplay(RoomSession rooms, RoomEntityManager entities)
    {
        if (rooms.AssetRoot != AssetRoot || entities.AssetRoot != AssetRoot ||
            !ReferenceEquals(rooms.RuntimeState,entities.RuntimeState) || !ReferenceEquals(rooms.SaveData,entities.SaveData))
            throw new ArgumentException("Era/season banner requires the selected room, save and runtime owners.");
        var save = rooms.SaveData;
        if (save.HasGlobalFlag(_suppressFlag))
        {
            save.SetGlobalFlag(_suppressFlag,false);
            return false;
        }
        if (_sentBackAddress != 0 && rooms.RuntimeState.ReadWramByte(_sentBackAddress) == 1) return false;
        byte flags = rooms.CurrentRoom.TilesetFlags;
        if (_displaySource == "group-zero" ? rooms.ActiveGroup != 0 :
            (flags & _largeIndoorsMask) != 0 || (flags & _outdoorsMask) == 0) return false;
        return entities.TryCreateInteraction(new EraOrSeasonInfoSpawn()) is not null;
    }

    internal EraOrSeasonInfoRecord Select(OracleRoomData room, OracleRuntimeState memory)
    {
        if (room.AssetRoot != AssetRoot || memory.Game != OracleAssetPack.GameFor(AssetRoot))
            throw new ArgumentException("Era/season state zero requires the selected live room and memory.");
        int subid = _variantSource == "tileset-past" ? (room.TilesetFlags & _pastMask) != 0 ? 1 : 0 :
            memory.ReadField("wLoadingRoomPack") == 0xff ? 0 : memory.ReadField("wRoomStateModifier");
        if ((uint)subid >= _records.Length) throw new NotSupportedException(
            $"{AssetRoot}: INTERAC_ERA_OR_SEASON_INFO $e0 state zero selected unsupported subid 0x{subid:x2}.");
        return _records[subid];
    }
}

internal readonly record struct EraOrSeasonInfoRecord(
    string AssetRoot, int SubId, string Sprite, int TileBase, int Palette, string Animation,
    int StartY, int StartX, int EnterStep, int TargetX, int HoldUpdates, int ExitStep, int ExitUpdates, string Source);
