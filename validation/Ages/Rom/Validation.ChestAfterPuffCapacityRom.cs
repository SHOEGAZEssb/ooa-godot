using Godot;
using System.Linq;

namespace Oracles;

public partial class ValidationRoot
{
    private void CompareChestAfterPuffCapacityRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int freeSlots in new[] { 0, 1 })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4, 0xba, OracleSaveData.RoomFlagItem, false);
            LoadValidationRoom(4, 0xba);
            _entities.Clear();
            _inventory.EquipA(0);
            _inventory.EquipB(0);
            _player.WarpTo(new(120, 120));
            FailIf(_collision.Collides(_player.Position),
                "INTERAC$20:$02 full-pool fixture requires Crown$4:$ba's original south floor.");
            var record = new CrownDungeonDatabase().GetRoomRecords(4, 0xba).Single();
            var script = new DungeonTriggerChestScriptRoomEntity(record.Position, _runtimeState, 7, 15,
                () => _entities.ActiveTriggers,
                () => _saveData.HasRoomFlag(4, 0xba, OracleSaveData.RoomFlagItem),
                _entities.IsScriptTextActive, _sound.PlaySound, () => _rooms.TrySetTile(0x57, 0xf1));
            _entities.AddEntity(script);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 120, 120);
            native.InitializeLinkGameplay();
            native.InitializeLinkWalkingAnimation();
            native[0xcc39] = 5;
            native[0xd240] = 1;
            native[0xd241] = 0x20;
            native[0xd242] = 2;
            native[0xd24b] = 88;
            native[0xd24d] = 120;
            IRoomEntity? released = null;
            int releasedAddress = 0;
            for (int index = 0; index < 13 - freeSlots; index++)
            {
                var puff = _entities.Spawn<PuzzlePuffEffect>(
                    new PuzzlePuffSpawn(new(200, 120), SoundId.MusNone));
                int slot = _entities.InteractionSlot(puff);
                int address = 0xd040 + slot * 0x100;
                native[address] = 1;
                native[address + 1] = 0x05;
                native[address + 2] = 0x80;
                native[address + 0x0b] = 120;
                native[address + 0x0d] = 200;
                released = _entities.EntityAdapters<IRoomEntity>().Single(entity =>
                    ReferenceEquals(entity.Node, puff));
                releasedAddress = address;
            }
            _entities.SetTrigger(0, true);
            _entities.SetTrigger(1, true);
            _entities.SetTrigger(2, true);
            native[0xcca0] = 7;
            var sounds = _sound.AttachPlayRequestAudit();
            void Compare()
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                native.AdvanceTileGraphics();
                FailIf(script.Finished != (native[0xd240] == 0) ||
                    !script.Finished && script.Counter >= 0 && script.Counter != native[0xd246] ||
                    _currentRoom.GetPackedStorageMetatile(0x57) != native[0xcf57] ||
                    !sounds.Requests.SequenceEqual(native.Sounds),
                    $"INTERAC$20:$02 capacity={freeSlots}, batch={batched}, repeat={repeat}: " +
                    "script wait, chest tile or source sound continuation differs.");
                int puffs = Enumerable.Range(0xd2, 14).Count(page =>
                    native[page * 0x100 + 0x40] != 0 && native[page * 0x100 + 0x41] == 0x05);
                FailIf(_entities.Entities<PuzzlePuffEffect>().Count != puffs,
                    "createpuff must preserve native full/one-free allocation and later deletion.");
            }
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero,
                batched: batched, afterUpdate: Compare);
            var orphans = ValidationNodeLifetime.OrphanIds().ToHashSet();
            Step(3);
            FailIf(ValidationNodeLifetime.OrphanIds().Any(id => !orphans.Contains(id)),
                "Failed queued createpuff must not construct an unowned native node.");
            FailIf(script.Counter != -1 || _entities.Entities<PuzzlePuffEffect>().Count != 13,
                "Failed and successful createpuff both yield before installing wait15.");
            if (freeSlots == 0)
            {
                // Releasing capacity after the failed command must not cause a
                // retry: scriptCmd_createPuff advanced its source pointer.
                _entities.RetireEntity(released!);
                native[releasedAddress] = 0;
            }
            Step();
            FailIf(script.Counter != 15, "Failed createpuff must still install the complete wait15.");
            Step(14);
            FailIf(script.Finished || script.Counter != 1,
                "Chest completion must wait until the source counter reaches zero.");
            Step();
            FailIf(!script.Finished || _currentRoom.GetPackedStorageMetatile(0x57) != 0xf1,
                "A full INTERACTION pool must not prevent the script from publishing chest$f1.");
            Step(20);
            FailIf(sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1 ||
                sounds.Requests.Count(cue => cue == SoundId.SndPoof) != freeSlots,
                "Only a successfully allocated puff may request its state-zero cue; solve cue occurs once.");
        }
        ReinitializeGameplayForValidation();
    }
}
