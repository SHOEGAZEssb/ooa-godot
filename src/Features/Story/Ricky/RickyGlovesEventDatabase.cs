using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported room $0:$6a Ricky spawner, glove script, and special-object
/// visual data.
/// </summary>
internal sealed class RickyGlovesEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    internal RickyGlovesEventRecord Record { get; }
    internal RickyCompanionVisualRecord Visual { get; }
    internal RickyCompanionBehaviorRecord Behavior { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }

    internal RickyGlovesEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "ricky_gloves_event.tsv",
            new GeneratedTableSchema(
                "room 0:6a Ricky gloves event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "controller-id", "controller-subid",
                    "spawner-id", "spawner-subid", "ricky-id", "ricky-y",
                    "ricky-x", "prerequisite-global-flag",
                    "ricky-state-address", "talked-mask", "complete-mask",
                    "left-mask", "gloves-treasure", "animal-companion-id",
                    "initial-animation", "jump-speed-z", "jump-gravity",
                    "ricky-sound", "initial-special-object-updates",
                    "initial-script-updates", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new RickyGlovesEventRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
            row.HexByte(3), row.HexByte(4), row.HexByte(5), row.HexByte(6),
            row.HexByte(7), row.HexByte(8), row.HexByte(9), row.HexWord(10),
            row.HexByte(11), row.HexByte(12), row.HexByte(13), row.HexByte(14),
            row.HexByte(15), row.HexByte(16), row.Decimal(17, -0x8000, 0x7fff),
            row.HexByte(18), row.HexByte(19), row.UnsignedDecimal(20),
            row.UnsignedDecimal(21), row.RequiredString(22));

        GeneratedTableRow visual = GeneratedTable.Load(
            Root + "ricky_companion_visual.tsv",
            new GeneratedTableSchema(
                "Ricky companion visual",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprite", "tile-base", "palette", "animations-base64",
                    "animation-source-offsets-base64", "link-sprite",
                    "link-palette", "link-frames-base64",
                    "link-source-offsets", "source"
                ],
                headerRequired: true)).SingleRow();
        Visual = new RickyCompanionVisualRecord(
            visual.RequiredString(0),
            visual.UnsignedDecimal(1),
            visual.UnsignedDecimal(2),
            visual.Base64Utf8(3).Split(
                '\n', StringSplitOptions.RemoveEmptyEntries),
            visual.Base64Utf8(4).Split(
                '\n', StringSplitOptions.RemoveEmptyEntries).Select(
                    ParseHexOffsets).ToArray(),
            visual.RequiredString(5),
            visual.UnsignedDecimal(6),
            visual.Base64Utf8(7).Split(
                '\n', StringSplitOptions.RemoveEmptyEntries),
            visual.RequiredString(8).Split(',').Select(value =>
                Convert.ToInt32(value, 16)).ToArray(),
            visual.RequiredString(9));

        GeneratedTableRow behavior = GeneratedTable.Load(
            Root + "ricky_companion_behavior.tsv",
            new GeneratedTableSchema(
                "Ricky companion behavior",
                GeneratedTableKeySemantics.Ordered,
                [
                    "idle-animation", "cancel-animation", "punch-animation",
                    "charge-animation", "hop-animation", "ground-speed",
                    "hop-delay", "hop-speed-z", "hop-gravity", "hop-speed",
                    "landing-delay", "punch-lifetime", "punch-damage",
                    "punch-boxes", "charge-updates", "tornado-speed", "tornado-radius-y",
                    "tornado-radius-x", "tornado-damage", "tornado-offsets",
                    "tornado-sprite", "tornado-tile-base", "tornado-palette",
                    "tornado-animation-base64", "jump-sound", "charge-sound",
                    "sword-spin-sound", "sword-slash-sound",
                    "punch-cue-sound", "long-jump-animation",
                    "long-jump-speed-z", "long-jump-delay",
                    "long-jump-speed", "cliff-down-delay",
                    "cliff-down-speed-z", "vine-top-tile", "hole-tiles",
                    "hole-offsets", "cliff-up-probes", "landing-probes",
                    "water-animation", "hole-animation", "source"
                ],
                headerRequired: true)).SingleRow();
        Behavior = new RickyCompanionBehaviorRecord(
            behavior.HexByte(0), behavior.HexByte(1), behavior.HexByte(2),
            behavior.HexByte(3), behavior.HexByte(4), behavior.HexByte(5),
            behavior.UnsignedDecimal(6), behavior.Decimal(7, -0x8000, 0x7fff),
            behavior.HexByte(8), behavior.HexByte(9),
            behavior.UnsignedDecimal(10), behavior.UnsignedDecimal(11),
            behavior.UnsignedDecimal(12),
            ParsePunchBoxes(behavior.RequiredString(13)),
            behavior.UnsignedDecimal(14), behavior.HexByte(15),
            behavior.UnsignedDecimal(16), behavior.UnsignedDecimal(17),
            behavior.UnsignedDecimal(18),
            ParseOffsets(behavior.RequiredString(19)),
            behavior.RequiredString(20), behavior.HexByte(21),
            behavior.UnsignedDecimal(22), behavior.Base64Utf8(23),
            behavior.HexByte(24), behavior.HexByte(25), behavior.HexByte(26),
            behavior.HexByte(27), behavior.HexByte(28),
            behavior.HexByte(29), behavior.Decimal(30, -0x8000, 0x7fff),
            behavior.UnsignedDecimal(31), behavior.HexByte(32),
            behavior.UnsignedDecimal(33),
            behavior.Decimal(34, -0x8000, 0x7fff), behavior.HexByte(35),
            ParseHexOffsets(behavior.RequiredString(36)),
            ParseOffsets(behavior.RequiredString(37)),
            ParseOffsets(behavior.RequiredString(38)),
            ParseOffsets(behavior.RequiredString(39)),
            behavior.HexByte(40), behavior.HexByte(41),
            behavior.RequiredString(42));

        Commands = CutsceneCommandCatalog.Load(
            Root + "ricky_gloves_commands.tsv");
        Validate();
    }

    internal bool ShouldSpawn(int group, int room, OracleSaveData save)
    {
        if (group != Record.Group || room != Record.Room ||
            !save.HasGlobalFlag(Record.PrerequisiteGlobalFlag))
        {
            return false;
        }

        int state = save.ReadWramByte(Record.RickyStateAddress);
        return (state & (Record.CompleteMask | Record.LeftMask)) == 0;
    }

    internal NpcRecord CreateActorRecord() => new(
        Record.Group,
        Record.Room,
        Record.RickyId,
        0,
        Record.RickyY,
        Record.RickyX,
        0,
        0,
        Visual.Sprite,
        Visual.TileBase,
        Visual.Palette,
        0,
        false,
        Visual.Animations[Record.InitialAnimation],
        Visual.Animations[Record.InitialAnimation],
        Visual.Animations[Record.InitialAnimation],
        Visual.Animations[Record.InitialAnimation],
        string.Empty,
        NpcImplementationClassification.EventOwned);

    private void Validate()
    {
        AnimationDefinition idle = OracleGraphicsCache.GetAnimationDefinition(
            Visual.Animations[0]);
        AnimationDefinition postDismountIdle =
            OracleGraphicsCache.GetAnimationDefinition(Visual.Animations[0x17]);
        AnimationDefinition loopedPose = OracleGraphicsCache.GetAnimationDefinition(
            Visual.Animations[0x11]);
        if (Record is not
            {
                Group: 0, Room: 0x6a,
                ControllerId: 0x71, ControllerSubId: 0x03,
                SpawnerId: 0x67, SpawnerSubId: 0x02,
                RickyId: 0x0b, RickyY: 0x40, RickyX: 0x50,
                PrerequisiteGlobalFlag: 0x15,
                RickyStateAddress: 0xc646,
                TalkedMask: 0x01, CompleteMask: 0x20, LeftMask: 0x40,
                GlovesTreasure: 0x48, AnimalCompanionId: 0x0b,
                InitialAnimation: 0, JumpSpeedZ: -0x0100,
                JumpGravity: 0x40, RickySound: 0xc3,
                InitialSpecialObjectUpdates: 2, InitialScriptUpdates: 0
            } ||
            Visual is not
            {
                Sprite: "spr_ricky", TileBase: 0, Palette: 3,
                Animations.Length: 37,
                AnimationSourceOffsets.Length: 37,
                LinkSprite: "spr_link", LinkPalette: 0,
                LinkFrames.Length: 51, LinkSourceOffsets.Length: 51
            } ||
            !Visual.AnimationSourceOffsets[0].AsSpan().SequenceEqual(
                [0, 0x0040, 0, 0x0040, 0, 0, 0x00c0, 0, 0, 0x00c0]) ||
            !Visual.AnimationSourceOffsets[0x17].AsSpan().SequenceEqual(
                [0x0140, 0x0dc0, 0x0140, 0x0dc0, 0x0140,
                    0x0140, 0x01e0, 0x0140, 0x0140, 0x01e0]) ||
            !Visual.AnimationSourceOffsets[0x20].AsSpan().SequenceEqual(
                [0x0260, 0x0fa0, 0x0fa0]) ||
            !Visual.AnimationSourceOffsets[0x21].AsSpan().SequenceEqual(
                [0x0940, 0x1060, 0x0940]) ||
            !Visual.AnimationSourceOffsets[0x22].AsSpan().SequenceEqual(
                [0x0140, 0x0ee0, 0x0ee0]) ||
            !Visual.AnimationSourceOffsets[0x23].AsSpan().SequenceEqual(
                [0x0940, 0x1060, 0x0940]) ||
            !Visual.AnimationSourceOffsets[0x24].AsSpan().SequenceEqual(
                [0x0360, 0x1140, 0x0360]) ||
            idle.Frames.Length != 10 ||
            idle.Frames[1].EncodedOam !=
                "248,0,4,0;248,8,6,0;8,252,0,0;8,4,2,0;8,12,0,32" ||
            postDismountIdle.Frames.Length != 10 ||
            postDismountIdle.Frames[1].EncodedOam !=
                "248,252,0,0;248,4,2,0;248,12,4,0;" +
                "8,252,6,0;8,4,8,0;8,12,6,32" ||
            Behavior is not
            {
                IdleAnimation: 0x20, CancelAnimation: 0x05,
                PunchAnimation: 0x09, ChargeAnimation: 0x13,
                HopAnimation: 0x19, GroundSpeed: 0x1e,
                HopDelay: 16, HopSpeedZ: -0x0180, HopGravity: 0x40,
                HopSpeed: 0x50, LandingDelay: 8, PunchLifetime: 20,
                PunchDamage: 4, ChargeUpdates: 30, TornadoSpeed: 0x78,
                TornadoRadiusY: 6, TornadoRadiusX: 6, TornadoDamage: 4,
                TornadoSprite: "spr_common_items", TornadoTileBase: 0x28,
                TornadoPalette: 1, JumpSound: OracleSoundEngine.SndJump,
                ChargeSound: OracleSoundEngine.SndChargeSword,
                SwordSpinSound: OracleSoundEngine.SndSwordSpin,
                SwordSlashSound: OracleSoundEngine.SndSwordSlash,
                PunchCueSound: OracleSoundEngine.SndUnknown5,
                LongJumpAnimation: 0x0f, LongJumpSpeedZ: -0x0300,
                LongJumpDelay: 8, LongJumpSpeed: 0x32,
                CliffDownDelay: 20, CliffDownSpeedZ: -0x02c0,
                VineTopTile: 0xd4, WaterAnimation: 0x0e,
                HoleAnimation: 0x0d
            } ||
            Behavior.TornadoOffsets.Length != 4 ||
            !Behavior.HoleTiles.AsSpan().SequenceEqual([0xf3, 0xfd]) ||
            !Behavior.HoleOffsets.AsSpan().SequenceEqual(
                [new Godot.Vector2(0, -8), new Godot.Vector2(8, 5),
                    new Godot.Vector2(0, 8), new Godot.Vector2(-8, 5)]) ||
            !Behavior.CliffUpProbes.AsSpan().SequenceEqual(
                [new Godot.Vector2(6, -8), new Godot.Vector2(-6, -8),
                    new Godot.Vector2(6, -24), new Godot.Vector2(-6, -24)]) ||
            !Behavior.LandingProbes.AsSpan().SequenceEqual(
                [new Godot.Vector2(0, 4), new Godot.Vector2(6, 4),
                    new Godot.Vector2(0, -2), new Godot.Vector2(-6, 4)]) ||
            Behavior.PunchBoxes.Length != 4 ||
            Behavior.PunchBoxes[0] != new RickyPunchBox(16, 12, -12, 0) ||
            Behavior.PunchBoxes[1] != new RickyPunchBox(12, 18, -2, 8) ||
            Behavior.PunchBoxes[2] != new RickyPunchBox(16, 12, 8, 0) ||
            Behavior.PunchBoxes[3] != new RickyPunchBox(12, 18, -2, -8) ||
            Behavior.TornadoOffsets[0] != new Godot.Vector2(0, -16) ||
            Behavior.TornadoOffsets[1] != new Godot.Vector2(12, 0) ||
            Behavior.TornadoOffsets[2] != new Godot.Vector2(0, 8) ||
            Behavior.TornadoOffsets[3] != new Godot.Vector2(-12, 0) ||
            OracleGraphicsCache.GetAnimationDefinition(
                Behavior.TornadoAnimation).Frames.Length != 2 ||
            idle.Frames.Length != 10 || idle.LoopStart != 0 ||
            loopedPose.Frames.Length != 2 || loopedPose.LoopStart != 1 ||
            Commands.Count != 22 ||
            Commands[0] is not CutsceneCheckAButtonCommand { Actor: "Ricky" } ||
            Commands[2] is not CutsceneMemoryBranchCommand
                { Binding: "RickyTalked", Value: 1, TargetCommand: 8 } ||
            Commands[4] is not CutsceneMemoryBranchCommand
                { Binding: "AnimalCompanion", Value: 0x0b, TargetCommand: 7 } ||
            Commands[8] is not CutsceneMemoryBranchCommand
                { Binding: "HasRickyGloves", Value: 1, TargetCommand: 13 } ||
            Commands[13] is not CutsceneShowTextCommand { TextId: 0x2004 } ||
            Commands[14] is not CutsceneNativeCommand
                { Handler: "LoseRickyGloves" } ||
            Commands[15] is not CutsceneNativeCommand
                { Handler: "BeginRickyMount" } ||
            Commands[17] is not CutsceneMemoryGateCommand
                { Binding: "RickyMounted", Value: 1 } ||
            Commands[18] is not CutsceneShowTextCommand { TextId: 0x2005 } ||
            Commands[19] is not CutsceneWriteMemoryCommand
                { Binding: "RickyStateOr", Value: 0x20 } ||
            Commands[21] is not CutsceneEndCommand ||
            Commands.Any(command => command.Source.SourceLine <= 0))
        {
            throw new InvalidOperationException(
                "Room 0:6a Ricky glove data diverges from the source contract.");
        }
    }

    private static Godot.Vector2[] ParseOffsets(string encoded) =>
        encoded.Split(';').Select(value =>
        {
            string[] pair = value.Split(',');
            if (pair.Length != 2 ||
                !int.TryParse(pair[0], out int y) ||
                !int.TryParse(pair[1], out int x))
            {
                throw new InvalidOperationException(
                    $"Malformed Ricky tornado offset '{value}'.");
            }
            return new Godot.Vector2(x, y);
        }).ToArray();

    private static int[] ParseHexOffsets(string encoded) =>
        encoded.Split(',').Select(value =>
            Convert.ToInt32(value, 16)).ToArray();

    private static RickyPunchBox[] ParsePunchBoxes(string encoded) =>
        encoded.Split(';').Select(value =>
        {
            string[] fields = value.Split(',');
            if (fields.Length != 4 ||
                !int.TryParse(fields[0], out int radiusY) ||
                !int.TryParse(fields[1], out int radiusX) ||
                !int.TryParse(fields[2], out int offsetY) ||
                !int.TryParse(fields[3], out int offsetX))
            {
                throw new InvalidOperationException(
                    $"Malformed Ricky punch box '{value}'.");
            }
            return new RickyPunchBox(radiusY, radiusX, offsetY, offsetX);
        }).ToArray();
}

internal readonly record struct RickyGlovesEventRecord(
    int Group,
    int Room,
    int ControllerId,
    int ControllerSubId,
    int SpawnerId,
    int SpawnerSubId,
    int RickyId,
    int RickyY,
    int RickyX,
    int PrerequisiteGlobalFlag,
    int RickyStateAddress,
    int TalkedMask,
    int CompleteMask,
    int LeftMask,
    int GlovesTreasure,
    int AnimalCompanionId,
    int InitialAnimation,
    int JumpSpeedZ,
    int JumpGravity,
    int RickySound,
    int InitialSpecialObjectUpdates,
    int InitialScriptUpdates,
    string Source);

internal readonly record struct RickyCompanionVisualRecord(
    string Sprite,
    int TileBase,
    int Palette,
    string[] Animations,
    int[][] AnimationSourceOffsets,
    string LinkSprite,
    int LinkPalette,
    string[] LinkFrames,
    int[] LinkSourceOffsets,
    string Source);

internal readonly record struct RickyCompanionBehaviorRecord(
    int IdleAnimation,
    int CancelAnimation,
    int PunchAnimation,
    int ChargeAnimation,
    int HopAnimation,
    int GroundSpeed,
    int HopDelay,
    int HopSpeedZ,
    int HopGravity,
    int HopSpeed,
    int LandingDelay,
    int PunchLifetime,
    int PunchDamage,
    RickyPunchBox[] PunchBoxes,
    int ChargeUpdates,
    int TornadoSpeed,
    int TornadoRadiusY,
    int TornadoRadiusX,
    int TornadoDamage,
    Godot.Vector2[] TornadoOffsets,
    string TornadoSprite,
    int TornadoTileBase,
    int TornadoPalette,
    string TornadoAnimation,
    int JumpSound,
    int ChargeSound,
    int SwordSpinSound,
    int SwordSlashSound,
    int PunchCueSound,
    int LongJumpAnimation,
    int LongJumpSpeedZ,
    int LongJumpDelay,
    int LongJumpSpeed,
    int CliffDownDelay,
    int CliffDownSpeedZ,
    int VineTopTile,
    int[] HoleTiles,
    Godot.Vector2[] HoleOffsets,
    Godot.Vector2[] CliffUpProbes,
    Godot.Vector2[] LandingProbes,
    int WaterAnimation,
    int HoleAnimation,
    string Source);

internal readonly record struct RickyPunchBox(
    int RadiusY,
    int RadiusX,
    int OffsetY,
    int OffsetX);
