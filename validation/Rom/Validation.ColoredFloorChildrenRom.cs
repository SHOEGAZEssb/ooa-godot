using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareColoredFloorChildrenRom()
    {
        int fixture = 0;
        foreach (int gate in new[] { 0,1,2,3 }) // Normal, text, interaction mask, full graphics queue.
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x79); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(168,120)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Floor-child fixture requires original$4:$79 floor for Link.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,168,120);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit();
            var disabled = _entities.InitializedObjectsDisabledSource;
            if (gate == 1) { _dialogue.ShowMessage("Declared pending floor child.",100); rom[0xcba0] = 1; }
            if (gate == 2) { _entities.InitializedObjectsDisabledSource = () => true; rom[0xcc8a] = 2; }
            if (gate == 3)
                for (int index = 0; index < 31; index++)
                { FailIf(!_rooms.TrySetTile(0x11,0xa0),"Floor-child fixture requires31 queued writes."); rom.SetTile(0x11,0xa0); }
            int indexCase = 0;
            try
            {
                // A pending child never repeats the parent's colored-tile test.
                foreach (var test in new[] { (Tile:0xad,Next:0xae),(Tile:0xae,Next:0xaf),(Tile:0xaf,Next:0xad),
                    (Tile:0xda,Next:0xad),(Tile:0x00,Next:0x01),(Tile:0xff,Next:0x00) })
                {
                    _currentRoom.SetPositionTileAndCollision(new(56,72),(byte)test.Tile,null,0); rom.CopyRoom(_currentRoom);
                    var child = new ToggleFloorTileRoomEntity(0x43,0,0xad,() => _entities.ActiveRoom,_rooms.TrySetTile,_entities.OnSoundRequested);
                    _entities.AddEntity(child);
                    int address = (0xd0+_entities.InteractionSlot(child))*256+0x40;
                    rom[address] = 1; rom[address+1] = 0x15; rom[address+2] = 1; rom[address+3] = 0x43;
                    StepSomariaMotionRom(rom,1,batched,afterUpdate:() =>
                    {
                        rom.AdvanceTileGraphics();
                        int expectedTile = gate == 3 && indexCase == 0 ? test.Tile : test.Next;
                        FailIf(!child.Finished || rom[address] != 0 || _currentRoom.Layout[0x43] != expectedTile ||
                            _currentRoom.GetUnderlyingStorageMetatile(0x43) != test.Next ||
                            _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) ||
                            !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                            $"Floor child$15:$01 tile=${test.Tile:x2}, gate={gate}, batch={batched}: byte increment/clamp, queue rejection, underlying write or deletion/cues differ.");
                    });
                    indexCase++;
                }
                // Landing on the captured pre-jump tile cancels without a write.
                byte landing = ToggleFloorRoomEntity.LinkTilePosition(_player);
                var cancelled = new ToggleFloorTileRoomEntity(0x43,landing,0xad,() => _entities.ActiveRoom,_rooms.TrySetTile,_entities.OnSoundRequested);
                _entities.AddEntity(cancelled);
                int cancel = (0xd0+_entities.InteractionSlot(cancelled))*256+0x40;
                rom[cancel] = 1; rom[cancel+1] = 0x15; rom[cancel+2] = 1; rom[cancel+3] = 0x43; rom[cancel+0x30] = landing;
                int cueCount = rom.Sounds.Count;
                StepSomariaMotionRom(rom,2,batched,afterUpdate:() =>
                {
                    rom.AdvanceTileGraphics();
                    FailIf(!cancelled.Finished || rom[cancel] != 0 || rom.Sounds.Count != cueCount,
                        "Floor child must cancel on its takeoff tile without a repeat color write or cue.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Floor children must preserve shared RNG.");
                });
            }
            finally { _entities.InitializedObjectsDisabledSource = disabled; _dialogue.Close(); }
        }
    }
}
