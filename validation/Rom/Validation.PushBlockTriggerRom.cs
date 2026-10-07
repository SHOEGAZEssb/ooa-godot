using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidatePushBlockTriggerRom()
    {
        int fixture = 0;
        foreach (int mode in new[] { 0,1,2,3 }) // Original block, changed live input, late count, late puff.
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x0c);
            _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            var records = data.GetRoomRecords(4,0x0c).ToArray();
            var trigger = _entities.Entities<PushBlockTriggerRoomEntity>().Single();
            var door = _entities.Entities<DungeonDoorRoomEntity>().Single();
            FailIf(records.Length != 2 || records[0] is not { Order:0,Id:0x13,SubId:1,PackedPosition:0x47 } ||
                records[1] is not { Order:1,Id:0x1e,SubId:8,PackedPosition:7 } ||
                _currentRoom.Layout[0x47] != 0x18 || _currentRoom.Layout[7] != 0x78 ||
                _entities.InteractionSlot(trigger) != 2 || _entities.InteractionSlot(door) != 3,
                "Original mainData.s room$4:$0c must place trigger$13:$01 before shutter$1e:$08 at native$d2/$d3.");
            Vector2 block = new(120,72);
            if (mode == 1) _currentRoom.SetPositionTileAndCollision(block,0x19,0x0b,0);
            Vector2 start = mode == 1 ? new(104.25f,72.5f) : new(120.25f,88.5f);
            int angle = mode == 1 ? 8 : 0;
            _player.WarpTo(start); _player.Face(mode == 1 ? Vector2I.Right : Vector2I.Up);
            FailIf(_collision.Collides(start),"Trigger approach must begin on unchanged room$4:$0c floor.");
            // Declare camera origin zero in both fixtures so the distant
            // upper shutter's native visibility gate has the same input.
            _entities.WorldToScreen = static point => point;
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,mode == 1 ? 1 : 0,(int)start.X,(int)start.Y);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x13; rom[0xd242] = 1;
            rom[0xd24b] = 72; rom[0xd24d] = 120;
            rom[0xd340] = 1; rom[0xd341] = 0x1e; rom[0xd342] = 8; rom[0xd34b] = 7;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int direction = 0xff) => StepSomariaMotionRom(rom,count,batch,direction,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                string context = $"Push trigger mode{mode}, batch={batch}, update{++update}";
                FailIf(trigger.Finished != (rom[0xd240] == 0 || rom[0xd241] != 0x13) ||
                    !trigger.Finished && (SomariaPrivate<int>(trigger,"_state") != rom[0xd244] ||
                        SomariaPrivate<int>(trigger,"_counter") != rom[0xd246] ||
                        SomariaPrivate<byte>(trigger,"_originalTile") != rom[0xd259]) ||
                    _entities.RoomEnemyCount != rom[0xcdd1] || door.Finished != (rom[0xd340] == 0) ||
                    SomariaPrivate<int>(door,"_counter") != rom[0xd346] ||
                    _entities.BossEntrySignal != rom[0xcc93] || !sounds.Requests.SequenceEqual(rom.Sounds),
                    context+$": trigger state/count runtime={SomariaPrivate<int>(trigger,"_state")}/{SomariaPrivate<int>(trigger,"_counter")}/{_entities.RoomEnemyCount}, native={rom[0xd244]}/{rom[0xd246]}/{rom[0xcdd1]}, saved=${SomariaPrivate<byte>(trigger,"_originalTile"):x2}/${rom[0xd259]:x2}; door={SomariaPrivate<DoorState>(door,"_state")}/{door.Finished}/{SomariaPrivate<int>(door,"_counter")}, native=${rom[0xd340]:x2}:${rom[0xd341]:x2}/${rom[0xd344]:x2}:{rom[0xd345]}/{rom[0xd346]}, signal={_entities.BossEntrySignal}/{rom[0xcc93]}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls-seed.Calls != rom.RandomCalls,context+": shared RNG differs.");
            });
            _dialogue.ShowGameplayMessage("Pending push trigger",100); rom[0xcba0] = 1;
            Step(); Step(3);
            FailIf(rom[0xd244] != 1 || rom[0xcf47] != 0x1d || rom[0xcdd1] != 1,
                "State0 must save live CF, write only CF$1d and increment the count during text; state1 then freezes.");
            // CF-only restoration must retain a collision change made while
            // state1 waits. The geometry producer is an explicit fixture input.
            if (mode == 1)
            {
                _currentRoom.SetPositionTileAndCollision(block,0x1d,0x0f,0,preserveRenderedTile:true);
                rom[0xce47] = 0x0f;
            }
            _dialogue.Close(); rom[0xcba0] = 0; Step(); Step(3);
            for (int wait = 0; rom[0xd244] != 3 && wait < 64; wait++) Step(direction:angle);
            FailIf(rom[0xd244] != 3 || rom[0xd246] != 30 || rom[0xcf47] != 0xa0,
                "Actual collision approach must push the restored directional block and install a separate 30-update trigger delay.");
            if (mode == 2)
            {
                // Original objectDataOp6 failure retains its counted slot.
                // Its allocating producer is outside this trigger fixture.
                _entities.RetainFailedPlacementCount(0); rom[0xcdd1]++;
            }
            _dialogue.ShowGameplayMessage("Push trigger delay",100); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            Step(29);
            FailIf(rom[0xd246] != 1 || sounds.Requests.Contains(SoundId.SndSolvePuzzle),
                "The trigger must retain its enemy count through update29 and dialogue.");
            if (mode == 3)
            {
                // Declare a newly transferred counted death puff immediately
                // before the trigger clears the count. Its enemy producer is
                // covered elsewhere; native PART$02 owns its later decrement.
                _entities.Spawn<EnemyDeathPuffEffect>(new EnemyDeathPuffSpawn(new(24,24),
                    EnemyId:0x0c,DecrementsRoomCount:true,DropsItem:false));
                rom[0xd0c0] = 1; rom[0xd0c1] = 2; rom[0xd0c2] = 0x0c;
                rom[0xd0cb] = rom[0xd0cd] = 24; rom[0xd0c7] = 0x81; rom[0xcdd1]++;
                SomariaPrivate<FrontendRom>(rom,"_rom").Sounds.Add(SoundId.SndKillEnemy);
                rom.HostilePartsEnabled = true;
            }
            Step();
            FailIf(!trigger.Finished || _entities.RoomEnemyCount != 0 || sounds.Requests.Contains(SoundId.SndSolvePuzzle),
                "Update30 clears the whole shared count before the later shutter checks it; its solve command yields separately.");
            Step(20); Step(3); Step(12,angle);
            FailIf(!door.Finished || _currentRoom.Layout[7] != 0xa0 || _currentRoom.IsSolid(door.Position) ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1 ||
                sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 2,
                "The shutter must solve/open once after the trigger, including repeated input after completion.");
            if (mode == 3)
            {
                FailIf(_entities.Entities<EnemyDeathPuffEffect>().Count != 0 || _entities.RoomEnemyCount != 0,
                    "PART$02 must retire after its guarded decrement without underflowing a count already cleared by INTERAC$13.");
                _entities.RetainFailedPlacementCount(0); rom[0xcdd1]++; Step(3);
                FailIf(_entities.RoomEnemyCount != 1,"A later count contribution must increment from the guarded zero byte.");
            }
            LoadValidationRoom(4,0x0c);
            FailIf(_currentRoom.Layout[0x47] != 0x18 || _currentRoom.Layout[7] != 0x78 ||
                _entities.RoomEnemyCount != 0 || _entities.Entities<PushBlockTriggerRoomEntity>().Count != 1,
                "Room re-entry must reconstruct the original block, pending trigger and closed shutter without the completed count.");
            _entities.WorldToScreen = _transitions.WorldToGameplayScreen;
        }
        ComparePushBlockTriggerScrollRom();
    }
}
