using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// roomSpecificCode.s, run by initializeRoom before sea effects and object parsing.
// These handlers select music; checkPlayRoomMusic owns the later sound request.
internal sealed class RoomEntryEffectDatabase
{
    private readonly Dictionary<(int Group, int Room), Entry> _entries = new();

    internal RoomEntryEffectDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(AgesGameContent.AssetRoot + "metadata/room_entry_effects.tsv",
            new GeneratedTableSchema("room-entry side effects", GeneratedTableKeySemantics.Unique,
                ["group", "room", "code", "source"], ["group", "room"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var code = (Handler)row.HexByte(2);
            if (!Enum.IsDefined(code)) throw row.Invalid(2, "a supported roomSpecificCode handler");
            _entries.Add((row.Decimal(0, 0, 7), row.HexByte(1)), new(code, row.RequiredString(3)));
        }
        if (_entries.Count != 7)
            throw new InvalidOperationException("roomSpecificCode.s: expected 7 room-entry side-effect bindings.");
    }

    internal void Apply(int group, int room, OracleSaveData save, OracleSoundEngine sound, Vector2I entryDirection)
    {
        if (!_entries.TryGetValue((group, room), out Entry entry)) return;
        switch (entry.Code)
        {
            case Handler.PalaceMinimap:
                save.WriteWramByte(AgesWramAddress.wMinimapRoom, 0x06);
                save.SetRoomFlag(1, 0x06, 0x10);
                break;
            case Handler.NayruHouseMusic:
                if (!save.HasGlobalFlag(AgesGlobalFlag.SavedNayru)) sound.SetNativeRoomMusic(SoundId.MusSadness);
                break;
            case Handler.RalphPortalMusic:
                if (entryDirection == Vector2I.Right && !save.HasGlobalFlag(AgesGlobalFlag.RalphEnteredPortal))
                    sound.SetNativeRoomMusic(SoundId.MusRalph);
                break;
            case Handler.FinishedGameMusic:
                if (save.HasGlobalFlag(AgesGlobalFlag.FinishedGame)) sound.SetNativeRoomMusic(SoundId.MusNayru);
                break;
            case Handler.PresentMermaidMinimap:
                save.SetMinimapLocation(0, 0x3c);
                break;
            case Handler.PastMermaidMinimap:
                save.SetMinimapLocation(1, 0x3c);
                break;
            case Handler.VireMusic:
                if (sound.NativeActiveMusic == 0) sound.SetNativeActiveMusic(0xff);
                break;
            default:
                throw new InvalidOperationException($"{entry.Source}: unsupported room-entry handler ${(int)entry.Code:x2}.");
        }
    }

    private readonly record struct Entry(Handler Code, string Source);
    private enum Handler
    {
        PalaceMinimap = 0x04,
        NayruHouseMusic = 0x05,
        RalphPortalMusic = 0x08,
        FinishedGameMusic = 0x09,
        PresentMermaidMinimap = 0x0a,
        PastMermaidMinimap = 0x0b,
        VireMusic = 0x0c
    }
}
