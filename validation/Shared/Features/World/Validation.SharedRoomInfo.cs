using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedScreenVariableClear(GameRoot root)
    {
        var rooms = root._gameplaySession!.Rooms;
        var memory = rooms.RuntimeState;
        var snapshot = memory.CaptureState();
        var native = new BraceletRom(rooms.CurrentRoom, root._scene.Player.Position, 2, 0xff, game: memory.Game);
        try
        {
            for (int index = 0; index < 0x30; index++)
            {
                byte value = (byte)(0xa0 + index);
                memory.SetWramByte(memory.FieldAddress("wScreenVariables") + index, value);
                native[0xcd00 + index] = value;
            }
            memory.SetField("wDisabledObjects", 0x21); native[0xcc8a] = 0x21;
            memory.SetField("wMenuDisabled", 0x80); native[0xcc02] = 0x80;
            memory.SetField("wcc63", 1); native[0xcc63] = 1;
            rooms.ClearScreenVariables(); native.ClearScreenVariables();
            for (int index = 0; index < 0x30; index++)
                FailIf(memory.ReadWramByte(memory.FieldAddress("wScreenVariables") + index) != native[0xcd00 + index] ||
                    native[0xcd00 + index] != (index is 0x28 or 0x2a or 0x2b ? 0xff : 0),
                    $"{memory.Game}: clearScreenVariables differs at offset${index:x2}.");
            FailIf(memory.ReadField("wDisabledObjects") != 0x21 || native[0xcc8a] != 0x21 ||
                memory.ReadField("wMenuDisabled") != 0x80 || native[0xcc02] != 0x80 ||
                memory.ReadField("wcc63") != 1 || native[0xcc63] != 1,
                $"{memory.Game}: clearing screen variables must preserve the separate control/item bytes.");
        }
        finally { memory.RestoreState(snapshot); }
    }

    private static void CheckSharedRoomInfo(FrontendValidationRoot root, bool batched)
    {
        CheckSharedScreenVariableClear(root);
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var app = new ApplicationValidationFixture(root);
        bool ages = rooms.SaveData.Game == OracleGame.Ages;
        var data = EraOrSeasonInfoDatabase.For(rooms.AssetRoot);
        int suppress = ages ? 0x16 : 0x2f;
        int initialGroup = rooms.ActiveGroup, initialRoom = rooms.CurrentRoom.Id;
        byte initialPack = rooms.RuntimeState.ReadField("wLoadingRoomPack");
        byte initialSeason = rooms.RuntimeState.ReadField("wRoomStateModifier");
        var bytes = ValidationRom.LoadCleanUs(rooms.SaveData.Game);
        var visual = GeneratedTable.Load(rooms.AssetRoot + "effects/era_info.tsv",
            new("native banner graphics comparison",GeneratedTableKeySemantics.Unique,
                ["subid","sprite","tile-base","palette","animation","start-y","start-x","enter-step","target-x","hold-updates","exit-step","exit-updates","source"],
                ["subid"],headerRequired:true));
        int graphics = 0x3f * 0x4000 + (ages ? 0x2999 : 0x2877);
        int Word(int at) => bytes.Span[at] | bytes.Span[at + 1] << 8;
        int animationBank = (ages ? 0x16 : 0x14) * 0x4000;
        int animationTable = animationBank + (ages ? 0x1855 : 0x1325);
        int oamTable = animationBank + (ages ? 0x1a23 : 0x14f5);
        int animation = animationBank + (Word(animationBank + (Word(animationTable + 0xe0 * 2) & 0x3fff)) & 0x3fff);
        int oam = animationBank + (Word(oamTable + 0xe0 * 2) & 0x3fff);
        int cells = (ages ? 0x14 : 0x13) * 0x4000 + (Word(oam + bytes.Span[animation + 1]) & 0x3fff);
        string expectedOam = string.Join(';',Enumerable.Range(0,bytes.Span[cells]).Select(cell =>
            string.Join(',',Enumerable.Range(0,4).Select(value => bytes.Span[cells + 1 + cell * 4 + value]))));
        FailIf(visual.Rows.Count != (ages ? 2 : 4),"All native era/season banner variants must be imported.");
        for (int subid = 0; subid < visual.Rows.Count; subid++)
        {
            var row = visual.Rows[subid]; int at = graphics + subid * 3;
            FailIf(bytes.Span[at] != (ages ? 0x70 : 0x55) || row.HexByte(2) != (bytes.Span[at + 1] & 127) ||
                row.UnsignedDecimal(3) != bytes.Span[at + 2] >> 4 || row.RequiredString(1) !=
                    (ages ? "spr_present_past_symbols" : "spr_season_symbols"),"Banner graphics must match each clean-ROM subid table.");
            var definition = OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(4));
            FailIf(definition.Frames.Length != 1 || definition.Frames[0].Duration != bytes.Span[animation] ||
                definition.Frames[0].Parameter != bytes.Span[animation + 2] || definition.Frames[0].EncodedOam != expectedOam,
                "Banner animation and all four OAM cells must resolve independently through the clean-ROM pointer tables.");
        }
        try
        {
            foreach (int variant in Enumerable.Range(0,ages ? 2 : 5))
            {
                if (ages) root.LoadDebugRoom(variant,variant == 0 ? 0x47 : 0x48);
                else root._seasonsOpening!.DebugTravel(0,0xb6);
                entities.Clear(); player.Inventory.EquipA(0); player.Inventory.EquipB(0);
                player.WarpTo(new(72,80)); player.BeginCutsceneControl();
                rooms.SaveData.SetGlobalFlag(suppress,false);
                if (ages) rooms.RuntimeState.SetField("wSentBackByStrangeForce",0);
                int pack = variant == 4 ? 0xff : 0, season = variant == 4 ? 3 : variant & 3;
                rooms.RuntimeState.SetField("wLoadingRoomPack",(byte)pack);
                rooms.RuntimeState.SetField("wRoomStateModifier",(byte)season);
                var native = new RoomInfoRom(rooms.SaveData,rooms.CurrentRoom,pack,season);
                FailIf(!native.TryDisplay() || !data.TryDisplay(rooms,entities),"Native full-room load must request its era/season banner.");
                var banner = entities.Entities<EraOrSeasonInfoDisplay>().Single();
                FailIf(banner.Stage != EraOrSeasonInfoStage.Initializing || banner.Visible || banner.PixelHash != 0 ||
                    banner.SubId != 0 || entities.InteractionSlot(banner) != native.Slot,"Banner allocation must retain the native pending state and physical slot.");
                // State zero samples current season, rather than the allocation-time byte.
                if (!ages && variant != 4)
                {
                    season = (season + 1) & 3;
                    rooms.RuntimeState.SetField("wRoomStateModifier",(byte)season); native.SetSeason(pack,season);
                }
                int updates = 0;
                void Step(int count)
                {
                    app.Step(count,Vector2.Zero,batched:batched);
                    for (int update = 0; update < count; update++) native.Update();
                    updates += count;
                    bool alive = native[0] != 0;
                    FailIf(banner.Finished == alive || alive && (banner.SubId != native[2] ||
                        (int)banner.Stage != native[4] || banner.Counter != native[6] ||
                        unchecked((byte)(int)banner.Position.X) != native[13] || unchecked((byte)(int)banner.Position.Y) != native[11]),
                        $"{rooms.SaveData.Game}: banner variant {variant} differs from native dispatcher on update {updates}.");
                }
                foreach (int freeze in new[] { 1,2,3,0 })
                {
                    rooms.RuntimeState.SetField("wDisabledObjects",(byte)(freeze == 1 ? 2 : 0));
                    rooms.RuntimeState.SetField("wScrollMode",(byte)(freeze == 3 ? 8 : 1));
                    if (freeze == 2) root._scene.Dialogue.ShowMessage("Banner dispatcher gate.",80);
                    else root._scene.Dialogue.Close();
                    native.SetFreeze(freeze);
                    Step(freeze == 1 ? 1 : 4);
                }
                FailIf(!banner.Visible || banner.TextureSize != new Vector2I(32,16) || banner.TextureOffset != new Vector2(-16,-8) ||
                    banner.PixelHash == 0,"Native state zero must install the selected four-cell banner graphics.");
                while (updates < 86) Step(batched ? Math.Min(4,86 - updates) : 1);
                FailIf(banner.Finished,"Native banner must remain alive through update 86.");
                Step(1); Step(3);
                FailIf(entities.Entities<EraOrSeasonInfoDisplay>().Count != 0,"Native banner must retire on update 87 without surviving collection entries.");
                FailIf(!data.TryDisplay(rooms,entities),"A completed banner must allow a later full-room-load request.");
                banner = entities.Entities<EraOrSeasonInfoDisplay>().Single();
                FailIf(entities.InteractionSlot(banner) != 2,"A repeat banner must reuse the lowest dynamic slot.");
                app.Step(1,Vector2.Zero,batched:batched); entities.Clear();
                FailIf(entities.Entities<EraOrSeasonInfoDisplay>().Count != 0,"Room retirement must cancel the active banner.");
                rooms.SaveData.SetGlobalFlag(suppress);
                native = new RoomInfoRom(rooms.SaveData,rooms.CurrentRoom,pack,season);
                FailIf(native.TryDisplay() || data.TryDisplay(rooms,entities) || native.Suppressed || rooms.SaveData.HasGlobalFlag(suppress),
                    "Suppression must consume the native one-shot flag before allocation.");
                for (int slot = 0; slot < 14; slot++)
                    entities.Spawn<StationaryDebrisEffect>(new PuzzlePuffSpawn(new(144,104),SoundId.MusNone));
                native = new RoomInfoRom(rooms.SaveData,rooms.CurrentRoom,pack,season,full:true);
                FailIf(native.TryDisplay() || data.TryDisplay(rooms,entities),"A full interaction pool must reject the optional banner without replacement.");
                entities.Clear(); app.Step(1,Vector2.Zero,batched:batched);
                FailIf(entities.Entities<EraOrSeasonInfoDisplay>().Count != 0,"A failed full-room-load banner request must not retry after capacity is freed.");
                player.EndCutsceneControl();
            }
        }
        finally
        {
            rooms.RuntimeState.SetField("wDisabledObjects",0); rooms.RuntimeState.SetField("wScrollMode",1);
            root._scene.Dialogue.Close(); player.EndCutsceneControl(); entities.Clear();
            if (ages) root.LoadDebugRoom(initialGroup,initialRoom);
            else root._seasonsOpening!.DebugTravel(initialGroup,initialRoom);
            rooms.RuntimeState.SetField("wLoadingRoomPack",initialPack);
            rooms.RuntimeState.SetField("wRoomStateModifier",initialSeason);
            entities.Clear();
        }
    }
}
