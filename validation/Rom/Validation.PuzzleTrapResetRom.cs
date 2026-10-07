using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePuzzleTrapResetRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x9b); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var record = new PuzzleTrapResetDatabase().GetRoomRecords(4, 0x9b).Single();
            var trap = new PuzzleTrapResetRoomEntity(record, _currentRoom, _runtimeState,
                _entities.OnSoundRequested, _entities.OnRoomWarpRequested);
            _entities.AddEntity(trap);
            _player.WarpTo(new(136, 136)); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 136, 136);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            int slot = (0xd0 + _entities.InteractionSlot(trap)) * 256 + 0x40;
            rom[slot] = 1; rom[slot + 1] = 0x90; rom[slot + 2] = 0x1f;
            var sounds = _sound.AttachPlayRequestAudit();
            void Compare()
            {
                FailIf(trap.State != rom[slot + 4] || trap.Counter != rom[slot + 6] ||
                    _entities.PlayerUpdatesFrozen != ((rom[0xcc8a] & 1) != 0) ||
                    _entities.PlayerMenusDisabled != (rom[0xcc02] != 0) ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"INTERAC$90:$1f: runtime state/counter={trap.State}/${trap.Counter:x2}, lock={_entities.PlayerUpdatesFrozen}/{_entities.PlayerMenusDisabled}; native={rom[slot + 4]}/${rom[slot + 6]:x2}, lock=${rom[0xcc8a]:x2}/${rom[0xcc02]:x2}.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls - seed.Calls != rom.RandomCalls, "Trap scan must preserve shared RNG.");
            }
            void Step(int count = 1, int angle = 0xff) =>
                StepSomariaMotionRom(rom, count, batched, angle, afterUpdate: Compare,
                    contextPrefix: "INTERAC$90:$1f");
            _dialogue.ShowMessage("Pending trap initialization.", 100); rom[0xcba0] = 1;
            Step(4);
            FailIf(trap.State != 1 || trap.Counter != 0,
                "Trap state0 initializes under text without decrementing the initial zero counter.");
            _dialogue.Close(); rom[0xcba0] = 0; Step();
            FailIf(trap.Counter != 255, "Trap's initial zero counter must wrap to $ff.");
            Step(32, 0); Step();
            FailIf(_player.Position != new Vector2(136, 104) || _collision.Collides(_player.Position),
                "Trap scan must approach original floor$68 from the southern floor through unchanged geometry.");
            Step(trap.Counter - 1);
            byte[] offsets = [0xf0, 0xe0, 1, 2, 0x10, 0x20, 0xff, 0xfe];
            Vector2 Center(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
            void Publish(int open)
            {
                // Declare completed block destinations after the real approach.
                // Native scan order and edge skipping come from the ROM handler.
                for (int index = 0; index < offsets.Length; index++)
                    _currentRoom.SetPositionTileAndCollision(Center((byte)(0x68 + offsets[index])),
                        0x2c, (byte)(index == open ? 0 : index % 2 == 0 ? 0x10 : 0x0f), 0);
                rom.CopyRoom(_currentRoom);
            }
            for (int open = 0; open < offsets.Length; open++)
            {
                Publish(open); Step();
                FailIf(trap.State != 1 || trap.Counter != 30,
                    $"Trap probe{open} must reject with an open collision byte and reload the native30-update interval.");
                Step(29);
            }
            Publish(-1);
            foreach (int protection in new[] { 3, -3 })
            {
                _player.ApplyInteractionInvincibility(protection); rom[0xd02b] = unchecked((byte)protection);
                Step();
                FailIf(trap.State != 1 || trap.Counter != 30,
                    "Either sign of protection must reject an otherwise trapped Link.");
                Step(29);
            }
            // Zero layout at a blocked near-up probe skips its open far probe.
            _currentRoom.SetPositionTileAndCollision(Center(0x58), 0, 0xff, 0);
            _currentRoom.SetPositionTileAndCollision(Center(0x48), 0xa0, 0, 0);
            rom.CopyRoom(_currentRoom);
            Step();
            FailIf(trap.State != 2 || trap.Counter != 60,
                "Zero-layout near-edge skipping must still trap vulnerable Link, request one error cue and an exact60-update control/menu lock.");
            _dialogue.ShowMessage("Trap reset pause.", 100); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            Step(59, 16);
            FailIf(trap.Counter != 1 || _player.Position != new Vector2(136, 104),
                "Trap delay must retain Link and its locks through update59 despite movement input.");
            // Compare the original terminal request. Destination loading runs
            // in the port here; native room-load/fade execution is outside this
            // bounded controller fixture and remains source-only coverage.
            StepGameplayUpdates(1, Vector2.Down, batched: batched, afterUpdate: () =>
            {
                rom.UpdateGameplay(0, 0x80, 16, _entities.FrameCounter);
                FailIf(trap.Counter != 0 || rom[slot + 6] != 0 ||
                    rom[0xcc02] != 0 || rom[0xcc8a] != 0 ||
                    rom[0xcc47] != 0x84 || rom[0xcc48] != 0x9b ||
                    rom[0xcc49] != 0 || rom[0xcc4a] != 0x12 || rom[0xcc4b] != 3 ||
                    _rooms.ActiveGroup != 4 || _currentRoom.Id != 0x9b ||
                    _transitions.ActiveWarpDestinationPosition != 0x12 ||
                    ReferenceEquals(trap, _entities.Entities<PuzzleTrapResetRoomEntity>().Single()),
                    "Trap update60 must clear both native locks and select original hardcoded warp$84:$9b/$00/$12/$03; the port must replace the source controller at that handoff.");
            });
            LoadValidationRoom(0, 0x60);
            FailIf(_entities.PlayerUpdatesFrozen || _entities.PlayerMenusDisabled,
                "Room cancellation must release the trap controller's retained restrictions.");
        }
        GD.Print("Validated executed-ROM trap initialization/zero wrap, all8 collision probes, signed protection gates, error/control lock, text pause, delay60 and terminal warp request through split/batched gameplay; destination loading remains source-only.");
    }
}
