using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareKeyholeScrollRom()
    {
        var database = new KeyholeControllerDatabase(); int fixture = 0;
        foreach (var row in new[] { (Group:0,Room:0x0a), (Group:1,Room:0x0e), (Group:1,Room:0xa5), (Group:0,Room:0x5c) })
        foreach (bool completed in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            // Deliberately oppose the source record's room flag and the live
            // destination flag. getThisRoomFlags reads active room identity.
            _saveData.SetRoomFlag(row.Group,row.Room,OracleSaveData.RoomFlag80,!completed);
            _saveData.SetRoomFlag(4,0xa6,OracleSaveData.RoomFlag80,completed);
            LoadValidationRoom(4,0xa5); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(220.25f,88.5f)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position),"Keyhole scroll fixture must approach the original room$4:$a5 exit floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,220,88);
            rom.Word(0xd00a,88*256+128); rom.Word(0xd00c,220*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom,9,batch,8);
            FailIf(!database.TryGet(row.Group,row.Room,out var source),"Expected one of the original placed keyhole controllers.");
            // Declare a pending outgoing owner after the final object pass.
            // Incoming parsing and scroll coordinates have separate coverage.
            var controller = new KeyholeControllerRoomEntity(source,_saveData,_rooms);
            _entities.AddEntity(controller);
            rom[0xd240] = 1; rom[0xd241] = (byte)source.Id; rom[0xd242] = (byte)source.SubId;
            rom[0xd24b] = (byte)source.Position.Y; rom[0xd24d] = (byte)source.Position.X;
            FailIf(!_rooms.TryGetNeighbor(Vector2I.Right,out int target) || target != 0xa6,
                "Keyhole cancellation must use imported adjacency$4:$a5 -> $4:$a6.");
            _transitions.BeginScroll(_player,Vector2I.Right,target);
            rom.SetOutgoingInteractions(); rom[0xcd00] = 8; rom[0xcc30] = 0xa6;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.AdvanceInteractions(_entities.FrameCounter);
                if (!_transitions.ScrollActive) rom.ClearOutgoingInteractions();
                bool alive = rom[0xd240] != 0;
                FailIf(_entities.OutgoingEntities<Node2D>().Contains(controller.Node) != alive ||
                    _transitions.ScrollActive && (controller.Finished == alive ||
                        alive && controller.Initialized != (rom[0xd244] != 0)) || sounds.Requests.Any() || rom.Sounds.Any(),
                    $"Keyhole${source.Id:x2}:${source.SubId:x2} scroll completed={completed}, batch={batch}, update{++update}: pending initialization/deletion/slot/cues differ.");
            });
            Step();
            bool crown = source.Id == 0x90 && source.SubId == 0x11;
            FailIf(controller.Initialized != (!crown && !completed) || controller.Finished != (!crown && completed),
                "Only Crown waits at scroll mode$08; other pending keyhole controllers read the destination flag$80 before setting their always-update bit.");
            Step(_transitions.ScrollTotalFrames-1);
            FailIf(_transitions.ScrollActive || _entities.OutgoingEntities<Node2D>().Contains(controller.Node),
                "Scroll completion must release every outgoing keyhole allocation.");
        }
    }
}
