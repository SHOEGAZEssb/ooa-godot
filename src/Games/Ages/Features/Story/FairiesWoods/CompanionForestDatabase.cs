
namespace Oracles;

internal sealed class CompanionForestDatabase
{
    private const string Root = AgesGameContent.AssetRoot + "cutscenes/";
    private readonly Dictionary<(int, int), List<ForestNativeObjectRecord>> _objects = new();
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<int, IReadOnlyList<CutsceneCommand>> _commands = new();
    private readonly Dictionary<int, Vector2> _presets = new();
    private readonly string _sprite;
    private readonly int _tileBase;
    private readonly string _animation;
    private readonly NpcRecord _exclamation;
    private readonly Dictionary<int, ForestCompanionRecord> _companions = new();
    internal ForestCompanionRecord Companion(int id) => _companions.TryGetValue(id, out var record)
        ? record : throw new InvalidOperationException($"companionScripts.s: invalid forest companion ${id:x2}.");
    internal IReadOnlyList<ForestNativeObjectRecord> RoomObjects(int group, int room) =>
        _objects.TryGetValue((group, room), out var objects) ? objects : Array.Empty<ForestNativeObjectRecord>();
    internal int Role(int group, int room) => RoomObjects(group, room).FirstOrDefault(record => record.Id == 0x71)?.SubId ?? -1;
    internal Warp ReturnWarp { get; }
    internal Vector2 Preset(int subId) => _presets[subId];
    internal string Text(int id) => _texts[id];
    internal IReadOnlyList<CutsceneCommand> Commands(int subId) => _commands[subId];

    internal CompanionForestDatabase()
    {
        foreach (var row in GeneratedTable.Load(Root + "companion_forest_companions.tsv",
            new GeneratedTableSchema("Forest companion branches", GeneratedTableKeySemantics.Unique,
                ["companion", "rescue-first", "rescue-unlinked", "rescue-linked", "reward-after",
                 "reward-unlinked", "reward-linked", "notice-animation", "source"], ["companion"], headerRequired: true)).Rows)
            _companions.Add(row.HexByte(0), new(row.HexWord(1), row.HexWord(2), row.HexWord(3),
                row.HexWord(4), row.HexWord(5), row.HexWord(6), row.HexByte(7)));
        foreach (int sub in new[] { 8, 9, 10, 11 })
            _commands.Add(sub, CutsceneCommandCatalog.Load(Root + $"companion_forest_{sub:x2}.tsv"));
        foreach (var row in GeneratedTable.Load(Root + "companion_forest_presets.tsv",
            new("Forest companion presets", GeneratedTableKeySemantics.Unique,
                ["subid", "y", "x", "source"], ["subid"], headerRequired: true)).Rows)
            _presets.Add(row.HexByte(0), new(row.HexByte(2), row.HexByte(1)));
        foreach (var row in GeneratedTable.Load(Root + "companion_forest_rooms.tsv",
            new GeneratedTableSchema("Companion forest placements", GeneratedTableKeySemantics.Ordered,
                ["group", "room", "order", "id", "subid", "y", "x", "source"], headerRequired: true)).Rows)
        {
            var record = new ForestNativeObjectRecord(row.Decimal(0, 0, 7), row.HexByte(1), row.UnsignedDecimal(2),
                row.HexByte(3), row.HexByte(4), row.HexByte(5), row.HexByte(6), row.RequiredString(7));
            if (!(record.Id == 0x71 && record.SubId is >= 8 and <= 12 || record.Id == 0x67 && record.SubId is 4 or 5))
                throw new InvalidOperationException($"{record.Source}: unsupported forest placement ${record.Id:x2}:${record.SubId:x2}.");
            if (!_objects.TryGetValue((record.Group, record.Room), out var objects))
                _objects.Add((record.Group, record.Room), objects = new());
            if (objects.Count != 0 && objects[^1].Order >= record.Order)
                throw new InvalidOperationException($"{record.Source}: forest placement order {record.Order} is duplicated or out of order.");
            objects.Add(record);
        }
        var warp = GeneratedTable.Load(Root + "companion_forest_return_warp.tsv",
            new("Companion forest return warp", GeneratedTableKeySemantics.Ordered,
                ["group", "room", "transition", "position", "request", "source"], headerRequired: true)).SingleRow();
        if (warp.HexByte(0) != 0x80 || warp.HexByte(1) != 0x63 || warp.HexByte(2) != 0 || warp.HexByte(3) != 0x56 || warp.HexByte(4) != 3)
            throw new InvalidOperationException($"{warp.RequiredString(5)}: unsupported companion return warp bytes.");
        ReturnWarp = new(0, 0, -1, 0, 0, warp.HexByte(0) & 7, warp.HexByte(1), warp.HexByte(3),
            warp.HexByte(2) >> 4, warp.HexByte(2) & 15, DirectFadeOut: true, NativeSource: warp.RequiredString(5));
        foreach (var row in GeneratedTable.Load(Root + "companion_forest_text.tsv",
            new GeneratedTableSchema("Companion forest dialogue", GeneratedTableKeySemantics.Ordered,
                ["text-id", "text-base64", "source"], headerRequired: true)).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        var visual = GeneratedTable.Load(Root + "companion_forest_flute.tsv",
            new GeneratedTableSchema("Companion flute graphic", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "animation-base64", "source"], headerRequired: true)).SingleRow();
        _sprite = visual.RequiredString(0); _tileBase = visual.UnsignedDecimal(1); _animation = visual.Base64Utf8(2);
        var mark = GeneratedTable.Load(Root + "companion_forest_exclamation.tsv",
            new GeneratedTableSchema("Companion exclamation", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "animation-base64", "source"], headerRequired: true)).SingleRow();
        string animation = mark.Base64Utf8(3);
        _exclamation = new NpcRecord(0, 0, InteractionId.ExclamationMark, 0, 0, 0, 0, 0, mark.RequiredString(0),
            mark.UnsignedDecimal(1), mark.UnsignedDecimal(2), 0, false,
            animation, animation, animation, animation, string.Empty, NpcImplementationClassification.EventOwned);
    }

    internal NpcRecord FluteRecord(int group, int room, Vector2 point, int companion = 0x0c) => new(
        group, room, AgesInteractionId.CompanionScripts, 0x0a, (int)point.Y, (int)point.X, 0, 0,
        _sprite, _tileBase, ((companion - 0x0a) & 1) * 2 ^ (companion - 0x0a), 0, false, _animation, _animation, _animation, _animation,
        string.Empty, NpcImplementationClassification.EventOwned);
    internal NpcRecord ExclamationRecord(int room, Vector2 point) => _exclamation with { Room = room, X = (int)point.X, Y = (int)point.Y };
}

internal sealed record ForestCompanionRecord(int RescueFirst, int RescueUnlinked, int RescueLinked,
    int RewardAfter, int RewardUnlinked, int RewardLinked, int NoticeAnimation);

internal sealed record ForestNativeObjectRecord(int Group, int Room, int Order, int Id, int SubId, int Y, int X, string Source);
