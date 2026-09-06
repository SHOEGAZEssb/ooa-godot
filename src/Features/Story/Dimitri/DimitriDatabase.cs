using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class DimitriDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    internal string[] Animations { get; }
    internal int[][] SourceOffsets { get; }
    internal Texture2D[] LinkTextures { get; }
    internal Texture2D[] LinkDamageTextures { get; }
    internal Vector2[] LinkOffsets { get; }
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<int, int> _mouthEffects = new();
    private readonly bool[] _activeCollisions = new bool[128];
    internal bool AcceptsMouthCollision(int type) => type >= 0 && type < 128 && _activeCollisions[type];
    internal bool CanSwallow(int mode) => _mouthEffects.TryGetValue(mode, out int effect)
        ? effect == 0x25 : throw new InvalidOperationException($"Dimitri mouth lacks enemy collision mode ${mode:x2}.");
    private readonly HashSet<(int Group, int Room)> _goodbyeRooms = new();
    private (int Group, int Room) _presetRoom;
    internal Vector2 PresetPosition { get; private set; }
    internal bool ShouldSpawnPreset(int group, int room, OracleSaveData save) =>
        _presetRoom == (group, room) && (save.ReadWramByte(0xc6bf) & 4) != 0 &&
        (save.ReadWramByte(0xc647) & 0x40) == 0;
    internal bool IsGoodbyeRoom(int group, int room) => _goodbyeRooms.Contains((group, room));

    internal DimitriDatabase()
    {
        var active = GeneratedTable.Load(Root + "dimitri_active_collisions.tsv",
            new GeneratedTableSchema("Dimitri enemy collision gates", GeneratedTableKeySemantics.Ordered,
                ["collision-type", "enabled", "source"], headerRequired: true));
        if (active.Rows.Count != 128) throw new InvalidOperationException("Dimitri requires all 128 enemyActiveCollisions.s rows.");
        for (int index = 0; index < 128; index++)
        {
            if (active.Rows[index].HexByte(0) != index) throw active.Rows[index].Invalid(0, "ordered enemy collision type");
            _activeCollisions[index] = active.Rows[index].Decimal(1, 0, 1) != 0;
        }
        foreach (var collision in GeneratedTable.Load(Root + "dimitri_collisions.tsv",
            new GeneratedTableSchema("Dimitri mouth collisions", GeneratedTableKeySemantics.Ordered,
                ["enemy-mode", "effect", "source"], headerRequired: true)).Rows)
            _mouthEffects.Add(collision.HexByte(0), collision.HexByte(1));
        foreach (var placement in GeneratedTable.Load(Root + "dimitri_rooms.tsv",
            new GeneratedTableSchema("Dimitri source placements", GeneratedTableKeySemantics.Ordered,
                ["group", "room", "role", "x", "y", "source"], headerRequired: true)).Rows)
        {
            var key = (placement.HexByte(0), placement.HexByte(1));
            switch (placement.RequiredString(2))
            {
                case "preset":
                    _presetRoom = key;
                    PresetPosition = new(placement.HexByte(3), placement.HexByte(4));
                    break;
                case "goodbye": _goodbyeRooms.Add(key); break;
                default: throw new InvalidOperationException("Unsupported Dimitri source placement role.");
            }
        }
        var row = GeneratedTable.Load(Root + "dimitri_visual.tsv",
            new GeneratedTableSchema("Dimitri $0c visuals", GeneratedTableKeySemantics.Ordered,
                ["sprite", "palette", "animations-base64", "animation-source-offsets-base64",
                 "link-frames-base64", "link-source-offsets", "source"], headerRequired: true)).SingleRow();
        if (row.RequiredString(0) != "spr_dimitri" || row.UnsignedDecimal(1) != 2)
            throw new InvalidOperationException("SPECIALOBJECT_DIMITRI $0c must use spr_dimitri/palette 2.");
        Animations = row.Base64Utf8(2).Split('\n');
        SourceOffsets = row.Base64Utf8(3).Split('\n').Select(line =>
            line.Split(',').Select(value => Convert.ToInt32(value, 16)).ToArray()).ToArray();
        string[] linkFrames = row.Base64Utf8(4).Split('\n');
        int[] sourceOffsets = row.RequiredString(5).Split(',').Select(value => Convert.ToInt32(value, 16)).ToArray();
        LinkTextures = new Texture2D[linkFrames.Length];
        LinkDamageTextures = new Texture2D[linkFrames.Length];
        LinkOffsets = new Vector2[linkFrames.Length];
        Image source = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_link.png");
        for (int i = 0; i < linkFrames.Length; i++)
        {
            string oam = OracleGraphicsCache.GetAnimationDefinition(linkFrames[i]).Frames[0].EncodedOam;
            (LinkTextures[i], LinkOffsets[i]) = NpcCharacter.BuildPositionedOamTexture(
                source, oam, 0, 0, null, sourceGrayscaleInverted: true, sourceOffset: sourceOffsets[i]);
            (LinkDamageTextures[i], _) = NpcCharacter.BuildPositionedOamTexture(
                source, oam, 0, 0, NpcCharacter.GetStandardSpritePalette(5),
                sourceGrayscaleInverted: true, sourceOffset: sourceOffsets[i]);
        }
        foreach (var text in GeneratedTable.Load(Root + "dimitri_texts.tsv",
            new GeneratedTableSchema("Dimitri scripts", GeneratedTableKeySemantics.Ordered,
                ["text-id", "text-base64", "source"], headerRequired: true)).Rows)
            _texts.Add(text.HexWord(0), text.Base64Utf8(1));
        if (Animations.Length != 40 || SourceOffsets.Length != 40)
            throw new InvalidOperationException("SPECIALOBJECT_DIMITRI $0c requires 40 source animations.");
    }

    internal string Text(int id) => _texts.TryGetValue(id, out string? text) ? text :
        throw new InvalidOperationException($"Dimitri script has no imported TX_{id:x4}.");
}
