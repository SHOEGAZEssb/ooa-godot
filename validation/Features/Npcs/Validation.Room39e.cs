using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom39eInteractions()
    {
        const int group = 3;
        const int roomId = 0x9e;
        static Vector2 PackedCenter(int packed) => new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);

        var npcDatabase = new NpcDatabase();
        IReadOnlyList<NpcRecord> records =
            npcDatabase.GetRoomNpcs(group, roomId);
        int[] impaVariants =
            [0x00, 0x01, 0x02, 0x05, 0x09, 0x0a, 0x0b, 0x0d, 0x0e];
        FailIf(
            records.Count != 11 ||
            records.Count(record =>
                record.Implementation ==
                    NpcImplementationClassification.SpecializedNative) != 11 ||
            records.Where(record =>
                    record is { Id: 0x4f, Var03: 0x01 or 0x0a })
                .Any(record => record.DefaultAnimation != 0x00) ||
            records.Where(record =>
                    record.Id == 0x4f &&
                    record.Var03 is not (0x01 or 0x0a))
                .Any(record => record.DefaultAnimation != 0x02) ||
            !records.Any(record => record is
                {
                    Id: 0x36,
                    SubId: 0x0b,
                    Y: 0x28,
                    X: 0x58,
                    TextId: 0x1d14,
                    CanFace: true
                }) ||
            !records.Any(record => record is
                {
                    Id: 0xad,
                    SubId: 0x07,
                    Y: 0x38,
                    X: 0x78,
                    TextId: 0x0605,
                    CanFace: true
                }),
            "Room 3:9e did not import all nine Impa variants plus " +
            "talkable Nayru and Zelda as specialized native actors.");

        var nativeData = new NayruHouseDatabase();
        FailIf(
            nativeData.Record is not
            {
                Group: group,
                Room: roomId,
                InteractionId: 0x4f,
                SubId: 0x00,
                StairPosition: 0x22,
                StairTile: 0x45,
                PreserveRendered: true
            },
            "The imported room 3:9e hidden-stair contract changed.");

        var save = OracleSaveData.CreateStandardGame();
        save.SetLinkedGame(false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagSavedNayru, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagGotRingFromZelda, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFlameOfDespairLit, value: false);
        save.SetRoomFlag(
            0, 0x83, OracleSaveData.RoomFlag80, value: false);
        save.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlag80, value: false);
        SetTreasure(save, TreasureDatabase.TreasureHarp, value: false);
        SetTreasure(save, TreasureDatabase.TreasureMakuSeed, value: false);
        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();

        var root = new Node { Name = "Room39eInteractionValidation" };
        AddChild(root);
        using var fixture = RoomEntityValidationFixture.ForRoot(
            root, new() { Npcs = npcDatabase, SaveData = save });
        RoomEntityManager manager = fixture.Manager;
        OracleRoomData room = _world.LoadRoom(group, roomId);
        Vector2 stair = PackedCenter(0x22);
        FailIf(
            room.GetMetatile(stair) != 0xe5,
            "Room 3:9e did not begin with concealed metatile $e5 at $22.");

        byte[] renderedBefore = BackgroundMetatile(room, 0x22);
        byte[] attributesBefore = BackgroundAttributes(room, 0x22);
        manager.LoadRoom(group, room);
        List<NpcCharacter> actors = manager.Entities<NpcCharacter>();
        List<NpcCharacter> orderedImpas = actors.Take(9).ToList();
        Node[] childOrder = root.GetChildren().Cast<Node>().ToArray();
        FailIf(
            actors.Count != 11 ||
            !orderedImpas.Select(npc => npc.Record.Var03)
                .SequenceEqual(impaVariants) ||
            actors[9].Record is not { Id: 0x36, SubId: 0x0b } ||
            actors[10].Record is not { Id: 0xad, SubId: 0x07 } ||
            childOrder.Length != 12 ||
            childOrder[^1].Name != "TileChangeWatcher_3",
            "Room 3:9e did not preserve source order: Impa, Nayru, " +
            "Zelda, then $dc:$08 watcher.");

        List<NpcCharacter> ActiveActors() =>
            actors.Where(npc => npc.Active).ToList();

        NpcCharacter initialImpa = orderedImpas[0];
        FailIf(
            ActiveActors() is not [var initial] ||
            !ReferenceEquals(initial, initialImpa) ||
            initialImpa.Position != new Vector2(0x38, 0x38) ||
            initialImpa.TextId != 0x0120 ||
            !CanTalkTo(initialImpa),
            "Room 3:9e did not begin with only talkable Impa state $00.");

        manager.Update(1.0 / 60.0, _player);
        byte logicalStair = room.GetMetatile(stair);
        byte[] renderedAfter = BackgroundMetatile(room, 0x22);
        byte[] attributesAfter = BackgroundAttributes(room, 0x22);
        bool hasBasementWarp = new WarpDatabase().TryGetTileWarp(
            group, roomId, 0x22, 0x45, out Warp basementWarp);
        FailIf(
            logicalStair != 0x45 ||
            !renderedAfter.SequenceEqual(renderedBefore) ||
            !attributesAfter.SequenceEqual(attributesBefore) ||
            !hasBasementWarp ||
            basementWarp is not
                {
                    DestinationGroup: 3,
                    DestinationRoom: 0x9f,
                    DestinationPosition: 0x22,
                    DestinationTransition: 4
                },
            "Impa state 0 did not make concealed tile $22 behave as " +
            "the $45 staircase to room 3:9f without redrawing $e5 " +
            $"(logical=${logicalStair:x2}; " +
            $"tiles={Convert.ToHexString(renderedBefore)}->" +
            $"{Convert.ToHexString(renderedAfter)}; " +
            $"attrs={Convert.ToHexString(attributesBefore)}->" +
            $"{Convert.ToHexString(attributesAfter)}; " +
            $"warp={hasBasementWarp}:{basementWarp}).");

        _player.WarpTo(
            initialImpa.Position + Vector2.Right * 20.0f,
            recordSafe: false);
        manager.Update(1.0 / 60.0, _player);
        FailIf(
            initialImpa.FacingVector != Vector2I.Right,
            "Room 3:9e Impa did not immediately face nearby Link.");
        _player.WarpTo(
            initialImpa.Position + Vector2.Left * 20.0f,
            recordSafe: false);
        manager.Update(1.0 / 60.0, _player);
        FailIf(
            initialImpa.FacingVector != Vector2I.Left,
            "Room 3:9e Impa incorrectly applied the generic 30-update " +
            "facing cooldown.");

        save.SetRoomFlag(0, 0x83, OracleSaveData.RoomFlag80);
        NpcCharacter passageImpa = orderedImpas.Single(
            npc => npc.Record.Var03 == 0x01);
        FailIf(
            ActiveActors() is not [var passage] ||
            !ReferenceEquals(passage, passageImpa) ||
            passageImpa.Position != new Vector2(0x28, 0x48) ||
            passageImpa.TextId != 0x0121 ||
            passageImpa.FacingVector != Vector2I.Up,
            "The opened D2 passage did not live-select Impa state $01.");
        _player.WarpTo(
            passageImpa.Position + Vector2.Right * 20.0f,
            recordSafe: false);
        manager.Update(1.0 / 60.0, _player);
        FailIf(
            passageImpa.FacingVector != Vector2I.Up,
            "Passage-state Impa did not retain her initial up-facing " +
            "animation outside conversation.");
        passageImpa.FaceToward(_player.Position);
        NpcInteractionTarget passageTalk =
            manager.ResolveNpcInteractionTarget(passageImpa);
        passageTalk.Begin();
        FailIf(
            passageImpa.FacingVector != Vector2I.Right,
            "Passage-state Impa did not enter her native talk lifecycle.");
        passageTalk.End();
        FailIf(
            passageImpa.FacingVector != Vector2I.Up,
            "Passage-state Impa did not restore animation $00 after talk.");

        SetTreasure(save, TreasureDatabase.TreasureHarp);
        NpcCharacter harpImpa = orderedImpas.Single(
            npc => npc.Record.Var03 == 0x02);
        FailIf(
            ActiveActors() is not [var harp] ||
            !ReferenceEquals(harp, harpImpa) ||
            harpImpa.Position != new Vector2(0x68, 0x28) ||
            harpImpa.TextId != 0x0122,
            "Obtaining the harp did not select Impa state $02.");

        if (save.WriteWramByte(0xc6bf, 0x04))
            save.CommitInventoryChange();
        FailIf(
            ActiveActors().Count != 0,
            "D3 essence state $03 did not remove Impa from room 3:9e " +
            $"(active={string.Join(", ", ActiveActors().Select(npc =>
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2}/" +
                $"v${npc.Record.Var03:x2}"))}).");

        if (save.WriteWramByte(0xc6bf, 0))
            save.CommitInventoryChange();
        save.SetGlobalFlag(OracleSaveData.GlobalFlagGotRingFromZelda);
        NpcCharacter zelda = actors.Single(
            npc => npc.Record is { Id: 0xad, SubId: 0x07 });
        FailIf(
            ActiveActors() is not [var unrescuedZelda] ||
            !ReferenceEquals(unrescuedZelda, zelda) ||
            zelda.TextId != 0x0605 ||
            !CanTalkTo(zelda),
            "Zelda did not replace absent Impa with talkable TX_0605.");

        save.SetLinkedGame(true);
        NpcCharacter linkedImpa = orderedImpas.Single(
            npc => npc.Record.Var03 == 0x0d);
        FailIf(
            !linkedImpa.Active || !zelda.Active ||
            ActiveActors().Count != 2,
            "Linked state $0d did not retain both Impa and Zelda.");

        save.SetLinkedGame(false);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        NpcCharacter savedImpa = orderedImpas.Single(
            npc => npc.Record.Var03 == 0x05);
        NpcCharacter nayru = actors.Single(
            npc => npc.Record is { Id: 0x36, SubId: 0x0b });
        FailIf(
            !savedImpa.Active || !nayru.Active || !zelda.Active ||
            ActiveActors().Count != 3 ||
            nayru.TextId != 0x1d14 ||
            zelda.TextId != 0x0606 ||
            !CanTalkTo(savedImpa) ||
            !CanTalkTo(nayru) ||
            !CanTalkTo(zelda),
            "SAVED_NAYRU did not select talkable Impa $05, Nayru " +
            "TX_1d14, and Zelda TX_0606.");
        nayru.ResetNativeNpcFacingState();
        _player.WarpTo(
            nayru.Position + Vector2.Right * 20.0f,
            recordSafe: false);
        manager.Update(1.0 / 60.0, _player);
        FailIf(nayru.FacingVector != Vector2I.Right, "Nayru did not run npcFaceLinkAndAnimate.");

        SetTreasure(save, TreasureDatabase.TreasureMakuSeed);
        FailIf(ActiveActors().Count != 0, "The Maku Seed did not retire all room 3:9e story NPCs.");

        Vector2 watchedTile = PackedCenter(0x32);
        FailIf(
            room.GetMetatile(watchedTile) != 0x1c,
            "Room 3:9e watcher did not snapshot source tile $1c at $32.");
        room.SetPositionTileAndCollision(
            watchedTile, 0xa0, null, manager.FrameCounter);
        manager.Update(1.0 / 60.0, _player);
        FailIf(
            !save.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag80) ||
            root.GetChildren().Cast<Node>().Any(
                node => node.Name == "TileChangeWatcher_3"),
            "Room 3:9e $dc:$08 did not set flag $80 and delete itself " +
            "after tile $32 changed.");

        var reentry = new RoomSession(
            group, roomId, () => 0, () => { }, save);
        OracleRoomData persisted = reentry.CurrentRoom;
        FailIf(
            persisted.GetMetatile(PackedCenter(0x31)) != 0x1c ||
            persisted.GetMetatile(PackedCenter(0x32)) != 0xa0,
            "Room 3:9e flag $80 did not reapply its $31/$32 tile pair.");

        manager.Clear();
        RemoveChild(root);
        root.Free();
        GD.Print(
            "Validated room 3:9e: ordered Impa/Nayru/Zelda native actors, " +
            "all story predicates and dialogue, immediate/passage facing, " +
            "concealed $45 basement stairs, and the final persistent " +
            "$dc:$08 tile watcher.");

        bool CanTalkTo(NpcCharacter npc)
        {
            _player.WarpTo(
                npc.Position + Vector2.Down * 12.0f,
                recordSafe: false);
            _player.Face(Vector2I.Up);
            return npc.CanTalkTo(_player);
        }
    }

    private static void SetTreasure(
        OracleSaveData save,
        int treasure,
        bool value = true)
    {
        int address = 0xc69a + treasure / 8;
        byte mask = (byte)(1 << (treasure & 7));
        byte current = save.ReadWramByte(address);
        byte next = value
            ? (byte)(current | mask)
            : (byte)(current & ~mask);
        if (save.WriteWramByte(address, next))
            save.CommitInventoryChange();
    }

    private static byte[] BackgroundMetatile(
        OracleRoomData room,
        int packed) =>
        BackgroundValues(room, packed, attributes: false);

    private static byte[] BackgroundAttributes(
        OracleRoomData room,
        int packed) =>
        BackgroundValues(room, packed, attributes: true);

    private static byte[] BackgroundValues(
        OracleRoomData room,
        int packed,
        bool attributes)
    {
        int left = (packed & 0x0f) * 2;
        int top = (packed >> 4) * 2;
        var values = new byte[4];
        int index = 0;
        for (int y = top; y < top + 2; y++)
        for (int x = left; x < left + 2; x++)
        {
            values[index++] = attributes
                ? room.GetBackgroundAttributeForValidation(x, y)
                : room.GetBackgroundSubtileForValidation(x, y);
        }
        return values;
    }
}
