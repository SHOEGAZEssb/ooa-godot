using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Importer-owned ITEM_HARP parent contract. The source animation itself is
/// shared with the room 3:ae teaching cutscene through NewGameIntroDatabase.
/// </summary>
internal sealed class HarpItemDatabase
{
    internal HarpItemRecord Record { get; }
    internal EffectRecord MusicNote { get; }
    internal IntroSpriteFrame[] LinkFrames { get; }
    internal int[] LinkAnimationParameters { get; }

    internal HarpItemDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/objects/harpItem.tsv",
            new GeneratedTableSchema(
                "ITEM_HARP parent",
                GeneratedTableKeySemantics.Ordered,
                [
                    "item", "harp-treasure", "echoes-treasure",
                    "currents-treasure", "ages-treasure", "song-frames",
                    "empty-song-frames", "note-interval",
                    "prohibited-tileset-mask", "past-mask",
                    "portal-room-flag", "empty-sound", "echoes-sound",
                    "currents-sound", "ages-sound", "animation-parameters",
                    "no-effect-text"
                ],
                headerRequired: true)).SingleRow();
        Record = new HarpItemRecord(
            row.HexByte(0),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.HexByte(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.HexByte(14),
            row.Base64Utf8(16));
        LinkAnimationParameters = row.RequiredString(15)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Convert.ToInt32(value, 16))
            .ToArray();
        MusicNote = LoadMusicNote();
        LinkFrames = new NewGameIntroDatabase().SpriteFrames("link-harp-item");
        Validate();
    }

    internal int SoundForSong(int song) => song switch
    {
        0 => Record.EmptySound,
        1 => Record.EchoesSound,
        2 => Record.CurrentsSound,
        3 => Record.AgesSound,
        _ => throw new InvalidOperationException(
            $"ITEM_HARP selected unsupported song ${song:x2}.")
    };

    internal int FramesForSong(int song) =>
        song == 0 ? Record.EmptySongFrames : Record.SongFrames;

    internal int AnimationParameterAtUpdate(int actionUpdate)
    {
        if (actionUpdate <= 0)
            throw new ArgumentOutOfRangeException(nameof(actionUpdate));

        int elapsed = actionUpdate - 1;
        for (int index = 0; index < LinkFrames.Length; index++)
        {
            if (elapsed < LinkFrames[index].Duration)
                return LinkAnimationParameters[index];
            elapsed -= LinkFrames[index].Duration;
        }
        return LinkAnimationParameters[^1];
    }

    private static EffectRecord LoadMusicNote()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_intro_effects.tsv",
            new GeneratedTableSchema(
                "shared Harp music-note effect",
                GeneratedTableKeySemantics.Unique,
                [
                    "name", "sprite", "tile-base", "palette", "duration",
                    "speed", "angle", "sway", "velocity-x-fixed",
                    "velocity-y-fixed", "animation"
                ],
                ["name"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (row.RequiredString(0) != "MusicNote")
                continue;
            return new EffectRecord(
                "MusicNote",
                row.RequiredString(1),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.FiniteFloat(5),
                row.Decimal(6),
                row.Boolean01(7),
                row.Decimal(8),
                row.Decimal(9),
                row.RequiredString(10));
        }
        throw new InvalidOperationException(
            "The shared effect table has no ITEM_HARP MusicNote record.");
    }

    private void Validate()
    {
        if (Record is not
            {
                Item: InventoryState.ItemHarp,
                HarpTreasure: TreasureDatabase.TreasureHarp,
                EchoesTreasure: TreasureDatabase.TreasureTuneOfEchoes,
                CurrentsTreasure: TreasureDatabase.TreasureTuneOfCurrents,
                AgesTreasure: TreasureDatabase.TreasureTuneOfAges,
                SongFrames: 260,
                EmptySongFrames: 261,
                NoteInterval: 32,
                ProhibitedTilesetMask: 0x7e,
                PastMask: 0x80,
                PortalRoomFlag: OracleSaveData.RoomFlagPortalSpotDiscovered,
                EmptySound: OracleSoundEngine.SndFilledHeartContainer,
                EchoesSound: OracleSoundEngine.SndTuneOfEchoes,
                CurrentsSound: OracleSoundEngine.SndTuneOfCurrents,
                AgesSound: OracleSoundEngine.SndTuneOfAges
            } ||
            string.IsNullOrWhiteSpace(Record.NoEffectText) ||
            LinkFrames.Length != 17 ||
            !LinkAnimationParameters.SequenceEqual(
                [0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0x81, 0xff]) ||
            MusicNote is not
            {
                SpriteName: "spr_common_sprites",
                TileBase: 0x44,
                Palette: 1,
                Duration: 70,
                Sway: true,
                VelocityXFixed: 53,
                VelocityYFixed: -79
            })
        {
            throw new InvalidOperationException(
                "Imported ITEM_HARP behavior no longer matches the supported Ages ROM.");
        }
    }
}

internal readonly record struct HarpItemRecord(
    int Item,
    int HarpTreasure,
    int EchoesTreasure,
    int CurrentsTreasure,
    int AgesTreasure,
    int SongFrames,
    int EmptySongFrames,
    int NoteInterval,
    int ProhibitedTilesetMask,
    int PastMask,
    int PortalRoomFlag,
    int EmptySound,
    int EchoesSound,
    int CurrentsSound,
    int AgesSound,
    string NoEffectText);
