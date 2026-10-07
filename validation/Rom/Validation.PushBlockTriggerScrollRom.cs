using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ComparePushBlockTriggerScrollRom()
    {
        int fixture = 0;
        foreach (bool initialized in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xa5); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(220.25f,88.5f)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position),"Trigger cancellation must approach the original room$4:$a5 exit floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,220,88);
            rom.Word(0xd00a,88*256+128); rom.Word(0xd00c,220*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom,9,batch,8);
            var data = new DungeonMechanicDatabase();
            var source = data.GetRoomRecords(4,0x0c).Single(row => row.Id == 0x13);
            // Declare the outgoing controller after the last object pass.
            // Original incoming parsing/scroll coordinates have separate
            // comparisons; this fixture isolates the owner's dispatch gates.
            var trigger = new PushBlockTriggerRoomEntity(source,_currentRoom,data,() => _entities.RoomEnemyCount,
                () => _entities.FrameCounter,_entities.ClearRoomEnemyCount,_entities.IsOutgoingEntity);
            _entities.AddEntity(trigger);
            rom[0xd240] = 1; rom[0xd241] = 0x13; rom[0xd242] = 1;
            rom[0xd24b] = 72; rom[0xd24d] = 120;
            if (initialized) StepSomariaMotionRom(rom,1,batch);
            int state = SomariaPrivate<int>(trigger,"_state");
            FailIf(!_rooms.TryGetNeighbor(Vector2I.Right,out int target) || target != 0xa6,
                "Trigger cancellation must retain imported adjacency$4:$a5 -> $4:$a6.");
            _transitions.BeginScroll(_player,Vector2I.Right,target);
            rom.SetOutgoingInteractions(); rom[0xcd00] = 8; rom[0xcdd1] = 0;
            var sounds = _sound.AttachPlayRequestAudit();
            int frame = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.AdvanceInteractions(_entities.FrameCounter);
                if (!_transitions.ScrollActive) rom.ClearOutgoingInteractions();
                bool alive = rom[0xd240] != 0;
                FailIf(_entities.OutgoingEntities<PushBlockTriggerRoomEntity>().Count != (alive ? 1 : 0) ||
                    _transitions.ScrollActive && (trigger.Finished == alive ||
                        alive && SomariaPrivate<int>(trigger,"_state") != rom[0xd244]) ||
                    sounds.Requests.Any() || rom.Sounds.Any(),
                    $"Trigger scroll initialized={initialized}, batch={batch}, update{++frame}: outgoing count/state/lifetime/cues differ.");
            });
            Step();
            FailIf(trigger.Finished == initialized || initialized && SomariaPrivate<int>(trigger,"_state") != state,
                "Scroll mode$08 must delete outgoing state0 before its scroll gate and retain an initialized trigger until bulk clear.");
            Step(_transitions.ScrollTotalFrames-1);
            FailIf(_transitions.ScrollActive || _entities.OutgoingEntities<PushBlockTriggerRoomEntity>().Count != 0,
                "Scroll completion must release the retained outgoing controller.");
            LoadValidationRoom(4,0x0c);
            FailIf(_entities.RoomEnemyCount != 0 || _currentRoom.Layout[0x47] != 0x18 ||
                _entities.Entities<PushBlockTriggerRoomEntity>().Count != 1,
                "Cancellation/re-entry must construct the original pending trigger without its cancelled count.");
        }
    }
}
