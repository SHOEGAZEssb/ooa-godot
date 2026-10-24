using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFinalDungeonEntryLifecycle()
    {
        // endgameCutsceneHandler_20's hardcoded warp uses destination $0f,
        // packed $57, with the inherited $81 masks and an invisible Link.
        ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x38);
        _saveData.SetLinkedGame(true); _saveData.SetRoomFlag(5, 0xf4, 0x40, false);
        _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x81);
        _runtimeState.SetWramByte(WramAddress.wMenuDisabled, 0x81);
        _transitions.ApplyWarp(_player, new Warp(0, 0x38, -1, 0, WarpSourceTransition.Instant,
            5, 0xf4, 0x57, 0, WarpDestinationTransition.Invisible));
        int arrivalUpdates = 0;
        do { StepGameplayUpdates(1, Vector2.Zero); }
        while (++arrivalUpdates < 400 && (_transitions.IsTransitioning || !_player.Visible ||
            _roomEvents.Get<FinalDungeonEntryEvent>().HasState));
        FailIf(arrivalUpdates == 400 || _rooms.ActiveGroup != 5 || _currentRoom.Id != 0xf4 ||
            !_saveData.HasRoomFlag(5, 0xf4, 0x40) || _player.CutsceneControlled ||
            _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
            _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0 ||
            _saveData.ReadWramByte(0xc62b) != 5 || _saveData.ReadWramByte(0xc62c) != 0xf4 ||
            _saveData.ReadWramByte(0xc62f) != 0x58 || _saveData.ReadWramByte(0xc630) != 0x78,
            "Room of Rites invisible arrival failed the real warp/entry handoff, source checkpoint, visibility or control release.");
        foreach (int elapsed in new[] { 0, 10, 125, 140, 260 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0xf4, 0x40, false);
            LoadValidationRoom(5, 0xf4);
            _player.WarpTo(new(0x78, 0x58));
            _transitions.DeactivateWarpAtPlayerPosition(_player);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x81);
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled, 0x81);
            if (elapsed != 0) StepGameplayUpdates(elapsed, Vector2.Zero);
            var owner = _roomEvents.Get<FinalDungeonEntryEvent>();
            owner.Cancel(); owner.Cancel();
            FailIf(owner.HasState || _transitions.NativePaletteFadeActive || _player.CutsceneControlled ||
                _entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Any() ||
                _saveData.HasRoomFlag(5, 0xf4, 0x40) != (elapsed != 0) ||
                _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != (elapsed == 0 ? 0x81 : 0) ||
                _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != (elapsed == 0 ? 0x81 : 0),
                $"Final dungeon entry cancellation after {elapsed} updates retained resources or changed the committed flag boundary.");
            // The caller owns the inherited raw mask before state zero runs.
            if (elapsed == 0)
            {
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
                _runtimeState.SetWramByte(WramAddress.wMenuDisabled, 0);
            }
            LoadValidationRoom(5, 0xf3);
            FailIf(_entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Any(),
                "Room exit failed to retire the entrance's PART children.");
        }
        foreach (int occupied in new[] { 13, 16 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0xf4, 0x40, false); LoadValidationRoom(5, 0xf4);
            _player.WarpTo(new(0x78, 0x58)); _transitions.DeactivateWarpAtPlayerPosition(_player);
            var fillers = Enumerable.Range(0, occupied).Select(_ =>
                _entities.Spawn<BlueEnergyBeadRoomEntity>(new BlueEnergyBeadSpawn(0, new(0x20, 0x20), 0xff))).ToHashSet();
            int requests = _sound.PlayRequestsFor(SoundId.SndEnergyThing);
            StepGameplayUpdates(1, Vector2.Zero);
            var born = _entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Where(bead => !fillers.Contains(bead)).ToArray();
            FailIf(!born.Select(bead => bead.Index).SequenceEqual(Enumerable.Range(0, 16 - occupied).Select(i => 7 - i)) ||
                born.Any(bead => bead.Initialized) || !_saveData.HasRoomFlag(5, 0xf4, 0x40) ||
                _sound.PlayRequestsFor(SoundId.SndEnergyThing) != requests + 1,
                $"Final dungeon entry with {occupied} occupied PART slots lost checked partial allocation, ordered indices, next-pass initialization, flag or sound.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(born.Any(bead => !bead.Initialized), "Entrance PART children did not initialize in the following PART pass.");
            _roomEvents.Get<FinalDungeonEntryEvent>().Cancel();
            FailIf(_entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Count() != occupied,
                "Cancelling the entrance must retire its own partial swirl and retain unrelated PART allocations.");
        }
        foreach (var direction in new[] { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0xf2); _entities.Clear();
            int requests = _sound.PlayRequestsFor(SoundId.SndSolvePuzzle);
            var exit = new EyePuzzleExitRoomEntity(_runtimeState, direction, _sound.PlaySound);
            _entities.AddEntity(exit);
            _runtimeState.SetWramByte(WramAddress.wScrollMode, 0);
            StepGameplayUpdates(2, Vector2.Zero);
            FailIf(exit.Finished || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != requests,
                "Eye maze exit cue did not wait for scroll-mode bit 0.");
            _runtimeState.SetWramByte(WramAddress.wScrollMode, 1);
            StepGameplayUpdates(2, Vector2.Zero);
            FailIf(!exit.Finished || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != requests + (direction == Vector2I.Up ? 1 : 0),
                "Eye maze exit cue did not preserve the source's north-only, one-shot direction gate.");
        }
        GD.Print("Validated final-dungeon entry cancellation before initialization, during wait/flash/fade and after completion; checked partial/full PART allocation; north-only exit cue and scroll gate.");
    }
}
