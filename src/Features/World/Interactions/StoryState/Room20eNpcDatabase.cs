using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported before/after-SAVED_NAYRU states for INTERAC_BOY $3c:$0d and
/// INTERAC_OLD_LADY $3d:$00 in present indoor room 2:0e.
/// </summary>
internal sealed class Room20eNpcDatabase
{
    private readonly Dictionary<(int Id, int SubId, bool SavedNayru),
        Room20eNpcStateRecord> _states = new();
    private readonly HashSet<(int Group, int Room, int Id, int SubId)> _placements = new();

    public Color[] StonePalette { get; }

    public Room20eNpcDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/room20e_npc_states.tsv",
            new GeneratedTableSchema(
                "room 2:0e NPC states",
                GeneratedTableKeySemantics.Unique,
                [
                    "actor", "phase", "group", "room", "id", "subid", "y", "x",
                    "palette-kind", "palette", "initial-animation",
                    "animation-mode", "behavior", "text-id", "animation",
                    "source", "utf8-base64"
                ],
                ["actor", "phase"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            string actor = row.RequiredString(0);
            bool savedNayru = row.RequiredString(1) switch
            {
                "before-saved-nayru" => false,
                "after-saved-nayru" => true,
                _ => throw row.Invalid(
                    1, "before-saved-nayru or after-saved-nayru")
            };
            int group = row.Decimal(2, 0, 7);
            int room = row.HexByte(3);
            int id = row.HexByte(4);
            int subId = row.HexByte(5);
            int y = row.HexByte(6);
            int x = row.HexByte(7);
            Room20ePaletteKind paletteKind = row.RequiredString(8) switch
            {
                "standard" => Room20ePaletteKind.Standard,
                "palh-a2" => Room20ePaletteKind.PalhA2,
                _ => throw row.Invalid(8, "standard or palh-a2")
            };
            int palette = row.HexByte(9);
            int initialAnimation = row.HexByte(10);
            Room20eAnimationMode animationMode = row.RequiredString(11) switch
            {
                "fixed" => Room20eAnimationMode.Fixed,
                "directional" => Room20eAnimationMode.Directional,
                _ => throw row.Invalid(11, "fixed or directional")
            };
            Room20eNpcBehavior behavior = row.RequiredString(12) switch
            {
                "push" => Room20eNpcBehavior.Push,
                "animate" => Room20eNpcBehavior.Animate,
                "face-animate" => Room20eNpcBehavior.FaceAnimate,
                _ => throw row.Invalid(12, "push, animate, or face-animate")
            };
            int textId = row.HexWord(13);
            string animation = row.RequiredString(14);
            string source = row.RequiredString(15);
            string message = row.Base64Utf8(16);

            bool expectedActor = actor switch
            {
                "boy" => id == 0x3c && subId == 0x0d,
                "old-lady" => id == 0x3d && subId == 0x00,
                _ => false
            };
            if (!expectedActor ||
                group != 2 || room != 0x0e ||
                paletteKind == Room20ePaletteKind.Standard &&
                    palette is < 0 or > 5 ||
                paletteKind == Room20ePaletteKind.PalhA2 && palette != 6 ||
                animationMode == Room20eAnimationMode.Directional &&
                    initialAnimation > 3 ||
                animationMode == Room20eAnimationMode.Fixed &&
                    behavior != Room20eNpcBehavior.Push ||
                textId == 0 != string.IsNullOrEmpty(message))
            {
                throw new InvalidOperationException(
                    $"Invalid room 2:0e NPC state at {row.Path}:{row.LineNumber}.");
            }

            var state = new Room20eNpcStateRecord(
                actor, savedNayru, group, room, id, subId, y, x,
                paletteKind, palette, initialAnimation, animationMode,
                behavior, textId, animation, source, message);
            if (!_states.TryAdd((id, subId, savedNayru), state))
            {
                throw new InvalidOperationException(
                    $"Duplicate room 2:0e state for ${id:x2}:${subId:x2}.");
            }
            _placements.Add((group, room, id, subId));
        }
        if (_states.Count != 4 || _placements.Count != 2)
        {
            throw new InvalidOperationException(
                $"Expected four states for two room 2:0e NPCs, got " +
                $"{_states.Count} states for {_placements.Count} placements.");
        }
        foreach ((int id, int subId) in new[] { (0x3c, 0x0d), (0x3d, 0x00) })
        {
            if (!_states.ContainsKey((id, subId, false)) ||
                !_states.ContainsKey((id, subId, true)))
            {
                throw new InvalidOperationException(
                    $"Room 2:0e interaction ${id:x2}:${subId:x2} is missing a SAVED_NAYRU phase.");
            }
        }

        byte[] paletteBytes = FileAccess.GetFileAsBytes(
            "res://assets/oracle/cutscenes/nayru_stone_sprite_palette.bin");
        if (paletteBytes.Length != 12)
        {
            throw new InvalidOperationException(
                $"PALH_a2 stone palette should contain 12 bytes, got " +
                $"{paletteBytes.Length}.");
        }
        StonePalette = new Color[4];
        for (int color = 0; color < StonePalette.Length; color++)
        {
            int offset = color * 3;
            StonePalette[color] = new Color(
                paletteBytes[offset] / 31.0f,
                paletteBytes[offset + 1] / 31.0f,
                paletteBytes[offset + 2] / 31.0f,
                color == 0 ? 0.0f : 1.0f);
        }
    }

    public bool Matches(NpcRecord npc) =>
        _placements.Contains((npc.Group, npc.Room, npc.Id, npc.SubId));

    public Room20eNpcStateRecord State(NpcRecord npc, bool savedNayru)
    {
        if (!Matches(npc) ||
            !_states.TryGetValue((npc.Id, npc.SubId, savedNayru),
                out Room20eNpcStateRecord state))
        {
            throw new InvalidOperationException(
                $"Unknown room 2:0e NPC state for " +
                $"{npc.Group}:${npc.Room:x2} ${npc.Id:x2}:${npc.SubId:x2}.");
        }
        return state;
    }

    public Color[] Palette(Room20eNpcStateRecord state) =>
        state.PaletteKind == Room20ePaletteKind.PalhA2
            ? StonePalette
            : NpcCharacter.GetStandardSpritePalette(state.Palette);
}

internal readonly record struct Room20eNpcStateRecord(
    string Actor,
    bool SavedNayru,
    int Group,
    int Room,
    int Id,
    int SubId,
    int Y,
    int X,
    Room20ePaletteKind PaletteKind,
    int Palette,
    int InitialAnimation,
    Room20eAnimationMode AnimationMode,
    Room20eNpcBehavior Behavior,
    int TextId,
    string Animation,
    string Source,
    string Message);

internal enum Room20ePaletteKind
{
    Standard,
    PalhA2
}

internal enum Room20eAnimationMode
{
    Fixed,
    Directional
}

internal enum Room20eNpcBehavior
{
    Push,
    Animate,
    FaceAnimate
}
