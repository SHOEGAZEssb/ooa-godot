using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareSkullStationaryOrbRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4,0x74,0xff,false);
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0);
            LoadValidationRoom(4,0x74); _entities.Clear();
            var data = new DungeonMechanicDatabase();
            var record = data.GetRoomRecords(4,0x74).Single();
            var chestRecord = new SkullDungeonDatabase().GetRoomRecords(4,0x74)[1];
            FailIf(record is not { Id:3,SubId:0,Order:1,PackedPosition:0x6a } ||
                chestRecord is not { Id:0x20,SubId:2,Order:2,X:72,Y:88 },
                "Original room$4:$74 must place PART$03:$00 at$6a before INTERAC$20:$02 at$58/$48.");
            // Isolate the two puzzle owners from the platform and Keese;
            // retain the original room's floor, holes and solid orb tile.
            var orb = new DungeonOrbRoomEntity(record,data,new DungeonInteractionVisualDatabase().Visual("grotto-orb"),
                _currentRoom,_runtimeState,() => (long)_animationTicks,_sound.PlaySound);
            var chest = new DungeonOrbChestRoomEntity(chestRecord,_runtimeState,_sound.PlaySound,_entities.IsScriptTextActive,
                () => _saveData.HasRoomFlag(4,0x74,OracleSaveData.RoomFlagItem),() => _rooms.TrySetTile(0x54,0xf1));
            _entities.AddEntity(orb); _entities.AddEntity(chest);
            _inventory.GiveTreasure(TreasureId.Sword,1); _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(0);
            _inventory.ApplyDamage(4);
            _player.WarpTo(new(184.25f,120.5f)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Stationary orb must be approached from original east-shore floor$7b.");
            byte[] background = new[] { (20,12),(21,12),(20,13),(21,13) }
                .Select(point => _currentRoom.GetBackgroundSubtileForValidation(point.Item1,point.Item2)).ToArray();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,184,120) { HostilePartsEnabled = true };
            rom.Word(0xd00a,120*256+128); rom.Word(0xd00c,184*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 4;
            rom[0xd0c0] = 1; rom[0xd0c1] = 3; rom[0xd0cb] = 104; rom[0xd0cd] = 168;
            rom[0xd240] = 1; rom[0xd241] = 0x20; rom[0xd242] = 2; rom[0xd24b] = 88; rom[0xd24d] = 72;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool attack = false,int angle = 0xff)
            {
                int tick = 0;
                int keys = angle switch { 0 => 0x40,24 => 0x20,_ => 0 };
                StepGameplayUpdates(count,angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    attack ? ["attack"] : [],attack ? ["attack"] : [],batch,() => {
                    rom.UpdateGameplay(attack && tick++ == 0 ? 1 : 0,(attack ? 1 : 0)|keys,angle,_entities.FrameCounter);
                    rom.AdvanceTileGraphics(); CompareSomariaMotionRom(rom,$"Stationary orb batch={batch}, update{++update}");
                    FailIf(orb.Visible != ((rom[0xd0da]&0x80) != 0) || orb.Palette != rom[0xd0db] ||
                        orb.ToggleMask != rom[0xd0c3] || orb.PendingHit != ((rom[0xd0ea]&0x80) != 0) ||
                        orb.HitLockout != -unchecked((sbyte)rom[0xd0eb]) ||
                        _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != rom[0xcdd2] ||
                        chest.Finished != (rom[0xd240] == 0) || !chest.Finished && chest.Counter >= 0 && chest.Counter != rom[0xd246] ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        $"Stationary orb update{update}: palette={orb.Palette}/{rom[0xd0db]}, pending={orb.PendingHit}/{rom[0xd0ea]:x2}, lock={orb.HitLockout}/{rom[0xd0eb]:x2}, toggle={_runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress):x2}/{rom[0xcdd2]:x2}, chest={chest.Finished}:{chest.Counter}/{rom[0xd240]:x2}:{rom[0xd246]}.");
                    FailIf(!background.SequenceEqual(new[] { (20,12),(21,12),(20,13),(21,13) }
                        .Select(point => _currentRoom.GetBackgroundSubtileForValidation(point.Item1,point.Item2))),
                        "PART$03 must preserve its floor image while installing logical$0a and collision$0f.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Stationary orb and chest must preserve original global RNG order.");
                });
            }
            _dialogue.ShowGameplayMessage("Pending stationary orb",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            Step(16,angle:0); Step(16,angle:24);
            FailIf(_currentRoom.IsSolid(_player.Position) || _player.Position.X < 180,
                $"Actual east-shore approach must stop outside the stationary orb's solid tile: {_player.Position}.");
            Step(attack:true);
            for (int wait = 0; !orb.PendingHit && wait < 40; wait++) Step();
            FailIf(!orb.PendingHit || chest.Counter != -1,"Actual Sword must publish the orb hit before the PART/chest handoff.");
            _dialogue.ShowGameplayMessage("Stationary orb pending contact",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step();
            Step(2); FailIf(chest.Counter != 15,"PART toggle must precede Solve, puff and wait15 on successive script updates.");
            Step(14); FailIf(chest.Finished,"The chest must remain pending through wait1.");
            Step(); FailIf(!chest.Finished || _currentRoom.GetMetatile(new(72,88)) != 0xf1,"Native wait zero must install chest$54.");
            Step(32); Step(attack:true);
            for (int wait = 0; !orb.PendingHit && wait < 40; wait++) Step();
            FailIf(!orb.PendingHit,"A repeated actual Sword must hit the stationary orb from its reachable east shore.");
            Step(); FailIf(orb.IsOn || orb.Palette != 1 || sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Repeated contact must reverse the orb without restarting the completed chest.");
            Step(32);
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,1); rom[0xcdd2] = 1; Step();
            FailIf(orb.Palette != 1 || !orb.IsOn,"An unrelated writer changes the shared bit without changing the orb's local palette.");
            orb.ClearHealthAndCollision(); rom[0xd0e4] &= 0x7f; rom[0xd0e9] = 0;
            Step(attack:true); Step(32);
            FailIf(orb.PendingHit || orb.Palette != 1 || !orb.IsOn || !orb.Visible,
                "PARTSTATUS_DEAD must retain the stationary orb while rejecting new Sword contacts.");
        }
    }
}
