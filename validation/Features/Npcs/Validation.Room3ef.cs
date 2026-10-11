using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom3efSeaFairy()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetGlobalFlag(0x20, false); _saveData.SetGlobalFlag(0x30, false);
            _inventory.LoseTreasure(TreasureId.FairyPowder);
            LoadValidationRoom(3, 0xef);
            var fairy = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x32);
            var story = _roomEvents.Get<SeaFairyEvent>();
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Approach()
            {
                _player.WarpTo(new(0x50, 0x68)); _player.Face(Vector2I.Up);
                FailIf(_collision.Collides(_player.Position) || _entities.BlocksLink(_player.Position),
                    "Room3:ef approach must start on open fairy-cave floor.");
                Step(3);
                for (int i = 0; i < 80 && !fairy.CanTalkTo(_player); i++)
                    StepGameplayUpdates(1, Vector2.Up, ["move_up"], i == 0 ? ["move_up"] : [], batched);
                Step(1);
                FailIf(!fairy.CanTalkTo(_player) || _collision.Collides(_player.Position) || _entities.BlocksLink(_player.Position),
                    $"Room3:ef $32:$02 is unreachable through its collision geometry: Link={_player.Position}.");
            }
            void Talk()
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                Step(2);
                FailIf(!_dialogue.IsOpen, "Room3:ef A-button contact did not run greatFairyOctorokScript.");
            }
            FailIf(fairy.Record is not { Id: 0x32, SubId: 2, X: 0x50, Y: 0x28,
                DefaultAnimation: 2, CanFace: true, Implementation: NpcImplementationClassification.EventOwned },
                "Room3:ef must import source $32:$02 at $2850 with all four Octorok directions.");
            FailIf(fairy.CurrentTerrainEffect is not null || fairy.TerrainShadowDrawn,
                "Uninitialized room3:ef $32:$02 must not draw terrain effects.");
            Step(1);
            for (int i = 0; i < 32; i++)
            {
                // bank0.s:_drawObjectTerrainEffects and terrainEffects.s:
                // stationary puddles advance globally every eight updates.
                int frame = (_entities.FrameCounter / 8) % 4;
                LinkTerrainEffectFrame? terrain = fairy.CurrentTerrainEffect;
                FailIf(terrain is null || terrain.Kind != LinkTerrainEffectKind.Puddle ||
                    terrain.Frame != frame || fairy.TerrainShadowDrawn ||
                    fairy.Position != new Vector2(0x50, 0x28),
                    $"Room3:ef stationary $32:$02 lost native puddle frame {frame} at update {_entities.FrameCounter}.");
                Step(1);
            }
            Approach(); Talk();
            FailIf(fairy.CurrentTerrainEffect is not { Kind: LinkTerrainEffectKind.Puddle },
                "Room3:ef textbox must retain the grounded Octorok puddle effect.");
            // TX_4106 has no terminator: the instruction stream falls into TX_4107.
            FailIf(!_dialogue.CurrentMessage.Contains("guardian of this\nsea!") ||
                !_dialogue.CurrentMessage.EndsWith("Please go find\nsome Fairy\nPowder!") ||
                _saveData.HasGlobalFlag(0x20), "TX_4106 lost its page/fallthrough or committed $20 before text completion.");
            _dialogue.Close(); Step(4);
            FailIf(!_saveData.HasGlobalFlag(0x20) || story.BlocksGameplay,
                "First fairy dialogue must persist $20 and return to checkabutton.");
            Talk(); FailIf(!_dialogue.CurrentMessage.StartsWith("Please go find"), "Fairy repeat talk must select TX_4107.");
            _dialogue.Close(); Step(4);
            _inventory.GiveTreasure(TreasureId.FairyPowder, 0);
            Talk(); FailIf(!_dialogue.CurrentMessage.Contains("Sprinkle the"), "Fairy Powder $51 did not select TX_4108.");
            FailIf(!_inventory.HasTreasure(TreasureId.FairyPowder) || !story.BlocksGameplay ||
                _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0x11,
                "Powder offer must freeze Link/items under $11 while retaining $51.");
            Vector2 link = _player.Position;
            _dialogue.Close(); Step(4);
            for (int i = 0; i < 90 && !story.Rising; i++) Step(1);
            FailIf(!story.Rising || story.Height != -1 || !_inventory.HasTreasure(TreasureId.FairyPowder),
                "The 60-update powder wait must enter its same-update speedZ=-$80 tail.");
            FailIf(fairy.CurrentTerrainEffect is not null ||
                fairy.TerrainShadowDrawn != ((_entities.FrameCounter & 1) != 0),
                "Rising room3:ef $32:$02 must replace puddles with page $d2's alternating ground shadow.");
            Step(29);
            FailIf(story.Height != -15 || !_inventory.HasTreasure(TreasureId.FairyPowder),
                "$32:$02 must retain Fairy Powder until the high Z byte first reaches $f0.");
            // -$0080 on the first tail update gives high $ff, then high $f0 on update 31.
            // The transformation consumes the treasure at that byte boundary.
            Step(1);
            var queen = _entities.EntityAdapters<CleansedFairyRoomEntity>().Single();
            FailIf(_inventory.HasTreasure(TreasureId.FairyPowder) || fairy.Active || queen.State != 0 || queen.Counter != 180,
                "At ZH=$f0 the later $d5:$01 slot must initialize without decrementing its 180 counter.");
            Step(179);
            FailIf(_dialogue.IsOpen || queen.Counter != 1 || _player.Position != link,
                "The healed fairy must wait 180 native updates with Link frozen.");
            Step(1);
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("cleanse the") || queen.State != 1,
                "The healed fairy must open complete TX_4109 on its counter-zero update.");
            Vector2 queenPosition = queen.Node.Position;
            Step(5);
            FailIf(queen.Node.Position != queenPosition || queen.Counter != 64,
                "TX_4109 must pause the circle while its always-update animation remains eligible.");
            _dialogue.Close(); Step(128);
            for (int i = 0; i < 4 && queen.State == 1; i++) Step(1);
            FailIf(queen.State != 2 || queen.Angle != 8,
                "$d5:$01 must complete two ordered 32-angle revolutions before ascending.");
            Step(33);
            FailIf(queen.State != 3 || queen.Height != -82, "Fairy exit must cross ZH=$b0 by subtracting $02 exactly 33 times.");
            var clean = _roomEvents.Get<CleanSeasEvent>();
            (int Group, int Room)[] montage = [(1, 0xa5), (1, 0xd2), (3, 0xb1), (3, 0xb0), (1, 0xa3)];
            int visited = -1, previousPhase = -1;
            for (int i = 0; i < 4500 && !_saveData.HasGlobalFlag(0x30); i++)
            {
                Step(1);
                if (clean.NativeActive && clean.RoomIndex >= 0)
                {
                    FailIf((_rooms.ActiveGroup, _rooms.CurrentRoom.Id) != montage[clean.RoomIndex],
                        "CUTSCENE_CLEAN_SEAS lost source montage room order.");
                    if (visited != clean.RoomIndex)
                    {
                        FailIf(clean.RoomIndex != visited + 1, "Sea cleanup skipped a source room.");
                        visited = clean.RoomIndex; previousPhase = -1;
                        if (visited == 3) FailIf((_runtimeState.ReadWramByte(WramAddress.wTmpcfc0) & 0x80) != 0,
                            "3:b1 must clear the Zora completion signal before loading 3:b0.");
                        if (visited == 4) FailIf((_runtimeState.ReadWramByte(WramAddress.wTmpcfc0) & 0x80) == 0,
                            "3:b0 must retain the Zora completion signal for the final 1:a3 wait.");
                    }
                    if (clean.Phase != previousPhase)
                    {
                        if (clean.Phase == 1) FailIf(clean.Counter != 240, "Sea powder phase must start at $f0 updates.");
                        if (clean.Phase == 2) FailIf(clean.Counter != 120, "Sea whitening must retain $78 for its post-cleaning wait.");
                        if (clean.Phase == 3)
                            for (int y = 0; y < _rooms.CurrentRoom.HeightInTiles; y++)
                            for (int x = 0; x < _rooms.CurrentRoom.WidthInTiles; x++)
                                FailIf(_rooms.CurrentRoom.GetPackedStorageMetatile((byte)(y * 16 + x)) == 0xeb,
                                    $"Sea cleanup retained polluted tile $eb at {montage[visited].Group:x}:{montage[visited].Room:x2}/${y * 16 + x:x2}.");
                        if (clean.Phase == 4) FailIf(clean.Counter != 60, "Final cleanup handoff must wait $3c before committing $30.");
                        previousPhase = clean.Phase;
                    }
                }
                FailIf(_saveData.HasGlobalFlag(0x30) && (clean.NativeActive || clean.HasState),
                    "$30 must be committed only after the last sea-cleaning state releases native ownership.");
            }
            FailIf(visited != 4 || !_saveData.HasGlobalFlag(0x30) || _rooms.ActiveGroup != 1 || _rooms.CurrentRoom.Id != 0xa3 ||
                _saveData.RespawnGroup != 1 || _saveData.RespawnRoom != 0xa3 || _saveData.RespawnX != 0x68 || _saveData.RespawnY != 0x38,
                "CUTSCENE_CLEAN_SEAS must persist $30, return to1:a3/$36 and update the death checkpoint.");
            for (int i = 0; i < 180 && _transitions.IsTransitioning; i++) Step(1);
            FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0 || _player.CutsceneControlled,
                "Sea-cleaning completion retained input/menu masks.");
            LoadValidationRoom(3, 0xef); Step(3);
            FailIf(_entities.Entities<NpcCharacter>().Any(n => n.Record.Id == 0x32 && n.Active) ||
                _entities.EntityAdapters<FountainFairyRoomEntity>().Count() != 1,
                "Clean-water room3:ef must replace $32:$02 with ENEMY_GREAT_FAIRY $38:$00.");
            LoadValidationRoom(3, 0xed);
            _saveData.SetGlobalFlag(0x30, false); LoadValidationRoom(3, 0xef);
            _inventory.GiveTreasure(TreasureId.FairyPowder, 0);
            fairy = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x32); Approach(); Talk();
            LoadValidationRoom(3, 0xed);
            FailIf(_dialogue.IsOpen || _player.CutsceneControlled || story.HasState ||
                !_inventory.HasTreasure(TreasureId.FairyPowder) ||
                _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0,
                "Leaving room3:ef must cancel the powder offer, retain $51 and release its input/menu masks.");
        }
        GD.Print("Validated room3:ef reachable/repeated fairy dialogue, text fallthrough, powder ownership, transformation counters, sea cleanup, checkpoint, cancellation and clean-water re-entry in split/batched gameplay.");
    }
}
