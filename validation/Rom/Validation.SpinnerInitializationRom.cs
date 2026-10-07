using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareSpinnerInitializationRom()
    {
        int fixture = 0;
        foreach (int room in new[] {0x60,0x52})
        foreach (bool crystals in new[] {false,true})
        foreach (int free in new[] {0,1,2})
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(0x0f,crystals);
            _runtimeState.SetWramByte(OracleRuntimeState.SpinnerStateAddress,0xa0);
            LoadValidationRoom(4,room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.ApplicationUpdateOwned = true;
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,
                (int)_player.Position.X,(int)_player.Position.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 3; rom[0xcdd4] = 0xa0;
            for (int slot = 2; slot < 16-free; slot++)
            {
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200,120),SoundId.MusNone));
                int address = (0xd0+slot)*256+0x40;
                rom[address] = 1; rom[address+1] = 5; rom[address+2] = 0x80; rom[address+0xb] = 120; rom[address+0xd] = 200;
            }
            // These source rooms have empty main object streams. Isolate the
            // room-specific producer, preserving the manager's live pool.
            var factory = SomariaPrivate<RoomEntityFactory>(_entities,"_factory");
            foreach (var candidate in factory.CreateRoomEntities(4,_currentRoom,default))
                if (candidate is DungeonSpinnerRoomEntity) _entities.AddEntity(candidate);
                else candidate.Node.Free();
            rom.InitializeRoomSpecificInteractions();
            var spinner = _entities.Entities<DungeonSpinnerRoomEntity>().SingleOrDefault();
            int parentSlot = 16-free, parentAddress = (0xd0+parentSlot)*256+0x40;
            bool expected = (room == 0x60 ? !crystals : crystals) && free != 0;
            bool nativeParent = free != 0 && rom[parentAddress] != 0 && rom[parentAddress+1] == 0x7d;
            FailIf((spinner != null) != nativeParent || nativeParent != expected,
                $"Original room-specific spinner gate room$4:${room:x2}, crystals={crystals}, free={free} differs.");
            if (spinner != null) FailIf(_entities.InteractionSlot(spinner) != parentSlot ||
                spinner.Position != new Vector2(120,88) || rom[parentAddress+0xb] != 0x57 || rom[parentAddress+0xd] != 1,
                "Original room-specific producer must retain packed$57/mask$01 and the first free physical slot.");
            int updates = 0;
            void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
                var arrow = _entities.Entities<DungeonSpinnerArrowRoomEntity>().SingleOrDefault();
                bool nativeArrow = free == 2 && rom[parentAddress+0x100] != 0 && rom[parentAddress+0x101] == 0x7d;
                FailIf((arrow != null) != nativeArrow || (spinner != null) != expected,
                    $"Spinner initialization update{++updates}, room${room:x2}, free={free}: parent/arrow capacity or retry differs.");
                if (spinner != null) FailIf(!spinner.Visible || rom[parentAddress+4] != 1 ||
                    spinner.WaitCounter != rom[parentAddress+6] ||
                    arrow != null && _entities.InteractionSlot(arrow) != parentSlot+1,
                    "Pending spinner must initialize once and its separately allocated later arrow must run in original slot order.");
            });
            Step(3);
            if (free < 2) Step(32); // Released filler slots cannot retry either failed allocation.
        }
    }
}
