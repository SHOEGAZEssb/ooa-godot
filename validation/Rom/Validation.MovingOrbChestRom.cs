using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareSkullMovingOrbRom()
    {
        int fixture = 0;
        foreach (bool fullQueue in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4,0x92,0xff,false);
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0);
            LoadValidationRoom(4,0x92);
            var records = new SkullDungeonDatabase().GetRoomRecords(4,0x92);
            FailIf(records is not [{ Id:0x20,SubId:3,Order:0,X:40,Y:104 },{ Id:0x0b,SubId:0,Order:1,X:128,Y:72,Var03:2 }],
                "Original room$4:$92 must order INTERAC$20:$03 before PART$0b:$00 with mask$02.");
            // Isolate the two original puzzle owners from enemy interception.
            _entities.Clear();
            var orb = new MovingOrbRoomEntity(records[1],new DungeonInteractionVisualDatabase().Visual("grotto-orb"),
                _runtimeState,_sound.PlaySound,SoundId.SndSwitch);
            var chest = new DungeonOrbChestRoomEntity(records[0],_runtimeState,_sound.PlaySound,_entities.IsScriptTextActive,
                () => _saveData.HasRoomFlag(4,0x92,OracleSaveData.RoomFlagItem),() => _rooms.TrySetTile(0x62,0xf1));
            _entities.AddEntity(chest); _entities.AddEntity(orb);
            FailIf(_entities.InteractionSlot(chest) != 2,"Orb chest must reserve original INTERAC slot$d2 before allocating its puff.");
            _inventory.GiveTreasure(TreasureId.Sword,1); _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(0);
            _inventory.ApplyDamage(4);
            _player.WarpTo(new(128.25f,96.5f)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Moving-orb Sword must begin on original floor$68.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,128,96) { HostilePartsEnabled = true };
            rom.Word(0xd00a,96*256+128); rom.Word(0xd00c,128*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 4;
            rom[0xd240] = 1; rom[0xd241] = 0x20; rom[0xd242] = 3; rom[0xd24b] = 104; rom[0xd24d] = 40;
            rom[0xd0c0] = 1; rom[0xd0c1] = 0x0b; rom[0xd0c3] = 2; rom[0xd0cb] = 72; rom[0xd0cd] = 128;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool attack = false,int angle = 0xff)
            {
                int tick = 0;
                StepGameplayUpdates(count,angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    attack ? ["attack"] : [],attack ? ["attack"] : [],batch,() => {
                    rom.UpdateGameplay(attack && tick++ == 0 ? 1 : 0,(attack ? 1 : 0) | (angle == 0 ? 0x40 : 0),angle,_entities.FrameCounter);
                    CompareSomariaMotionRom(rom,$"Moving orb/chest batch={batch}, update{++update}"); rom.AdvanceTileGraphics();
                    FailIf(orb.State != rom[0xd0c4] || orb.Position != new Vector2(rom.Word(0xd0cc)/256f,rom.Word(0xd0ca)/256f) ||
                        orb.Palette != rom[0xd0db] || orb.PendingHit != ((rom[0xd0ea]&0x80) != 0) ||
                        orb.HitLockout != -unchecked((sbyte)rom[0xd0eb]) ||
                        _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != rom[0xcdd2] ||
                        chest.Finished != (rom[0xd240] == 0) || !chest.Finished && chest.Counter >= 0 && chest.Counter != rom[0xd246] ||
                        _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        $"Moving orb/chest update{update}: orb={orb.State}/{rom[0xd0c4]}, XY={orb.Position}/{rom.Word(0xd0cc)/256f},{rom.Word(0xd0ca)/256f}, palette={orb.Palette}/{rom[0xd0db]}, pending={orb.PendingHit}/{rom[0xd0ea]:x2}, lock={orb.HitLockout}/{rom[0xd0eb]:x2}, toggle={_runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress):x2}/{rom[0xcdd2]:x2}, chest={chest.Finished}:{chest.Counter}/{rom[0xd240]:x2}:{rom[0xd246]}, queue={_rooms.PendingTileGraphics}/{((rom[0xcce0]-rom[0xccdf])&31)}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Moving orb and chest must preserve original global RNG order.");
                });
            }
            _dialogue.ShowGameplayMessage("Pending moving orb",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            Step(48);
            FailIf(orb.State != 9 || orb.Position.X != 152,"Original moving orb must reach$98 after48 SPEED_80 updates.");
            Step(); Step(95);
            FailIf(orb.State != 11 || orb.Position.X != 104.5f,"Original leftward endpoint must retain its half-pixel fraction at$68.");
            Step();
            Step(12,angle:0); Step(attack:true);
            for (int wait = 0; !orb.PendingHit && wait < 40; wait++) Step();
            FailIf(!orb.PendingHit || chest.Counter != -1,"Actual Sword must publish the orb hit before its PART/chest handoff.");
            _dialogue.ShowGameplayMessage("Moving orb pending contact",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step();
            FailIf(chest.Counter != -1 || orb.PendingHit || orb.Palette != 2,
                "The next eligible PART pass must toggle bit1 before INTERAC$20:$03 plays Solve and yields.");
            Step(2);
            FailIf(chest.Counter != 15,"createpuff and wait15 must each execute on their next eligible script update.");
            Step(14);
            byte filler = _currentRoom.GetMetatile(new(24,24));
            for (int index = 0; index < (fullQueue ? 31 : 0); index++) { FailIf(!_rooms.TrySetTile(0x11,filler),"Chest queue fixture requires31 accepted writes."); rom.SetTile(0x11,filler); }
            Step();
            FailIf(!chest.Finished || (_currentRoom.GetMetatile(new(40,104)) == 0xf1) == fullQueue,
                "settilehere must honor queue capacity and finish its script on the same update.");
            Step(12);
            FailIf((_currentRoom.GetMetatile(new(40,104)) == 0xf1) == fullQueue,"Draining graphics must retain the accepted or rejected chest command.");
            Step(32);
            for (int wait = 0; (orb.Position.X < 124 || orb.Position.X > 128) && wait < 192; wait++) Step();
            Step(attack:true);
            for (int wait = 0; !orb.PendingHit && wait < 40; wait++) Step();
            FailIf(!orb.PendingHit,"Repeated actual Sword must contact the moving orb after chest completion.");
            Step();
            FailIf(orb.Palette != 1 || _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != 0 ||
                !chest.Finished || sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Repeated orb contact must reverse bit1 without restarting the completed chest script.");
        }
    }
}
