using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareSwitchTileTogglerGatesRom()
    {
        int fixture = 0;
        foreach (int queued in new[] { 0,30,31 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _entities.RuntimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,0);
            LoadValidationRoom(4,0x89); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var record = new SkullDungeonDatabase().GetRoomRecords(4,0x89).Single();
            var rail = new SwitchTileTogglerRoomEntity(record,new DungeonInteractionDatabase(),
                _entities.RuntimeState,_roomView.QueueRedraw,_rooms.TrySetTile);
            _entities.AddEntity(rail);
            _player.WarpTo(new(40.25f,136.5f)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Rail gate fixture must begin on original room$4:$89 floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,40,136);
            rom.Word(0xd00a,136*256+128); rom.Word(0xd00c,40*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x78; rom[0xd242] = 4;
            rom[0xd24b] = 0x67; rom[0xd24d] = 0x0b;
            var sounds = _sound.AttachPlayRequestAudit();
            void Publish(byte state)
            {
                // Declared external switch producer; do not manufacture a
                // PART hit or feed the handler's resulting tile to the ROM.
                _entities.RuntimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,state);
                rom[0xcdd3] = state;
            }
            void Step(int count = 1) => StepSomariaMotionRom(rom,count,batch,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                FailIf(SomariaPrivate<bool>(rail,"_initialized") != (rom[0xd244] == 1) ||
                    SomariaPrivate<int>(rail,"_lastSwitchState") != rom[0xd243] ||
                    _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),
                    $"Rail gate queued={queued}, batch={batch}: initialization, complete switch byte or queue differs.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls-seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                    "Rail state changes must retain native cues and shared RNG.");
            });
            _dialogue.ShowGameplayMessage("Rail gate pause",120); rom[0xcba0] = 1;
            Publish(4); Step();
            FailIf(_currentRoom.Layout[0x67] != 0x5c || rom[0xd243] != 4,
                "State0 must sample current switch byte through text without applying an earlier change.");
            Publish(0x84); Step(3);
            FailIf(_currentRoom.Layout[0x67] != 0x5c || rom[0xd243] != 4,
                "Initialized rail must retain the preceding switch byte throughout text.");
            _dialogue.Close(); rom[0xcba0] = 0;
            for (int write = 0; write < queued; write++)
            {
                FailIf(!_rooms.TrySetTile(0x11,0xa0),"Rail gate must fill only the declared accepted queue entries.");
                rom.SetTile(0x11,0xa0);
            }
            Step(); Step(8);
            FailIf(_currentRoom.Layout[0x67] != (queued == 31 ? 0x5c : 0x5d) || rom[0xd243] != 0x84,
                "Queue rejection must consume the state change without a delayed rail retry.");
            Publish(0x85); Step();
            FailIf(_currentRoom.Layout[0x67] != 0x5d,
                "An unrelated bit change must reapply the selected rail from the complete switch byte.");
            Publish(0x81); Step(); Step(3);
            FailIf(_currentRoom.Layout[0x67] != 0x5c || rom[0xcdd3] != 0x81,
                "Clearing the rail mask must restore the original branch and retain unrelated switch bits.");
        }
    }
}
