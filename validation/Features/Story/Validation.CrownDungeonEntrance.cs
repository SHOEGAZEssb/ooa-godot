using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownDungeonEntrance()
    {
        var entrance = _roomEvents.Get<CrownDungeonEntranceEvent>();
        byte Door() => _currentRoom.GetMetatile(new Vector2(0x78, 0x18));
        void FinishEntry()
        {
            for (int i = 0; i < 180 && _transitions.IsTransitioning; i++)
                StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_transitions.IsTransitioning || _rooms.ActiveGroup != 4 || _currentRoom.Id != 0xbb,
                $"Crown Dungeon door did not finish its entry warp: {_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}.");
        }
        void Arrange()
        {
            _saveData.SetRoomFlag(0, 0x0a, 0x80, false);
            LoadValidationRoom(0, 0x0a);
            _player.WarpTo(new Vector2(0x78, 0x48), recordSafe: false);
            StepGameplayUpdates(2, Vector2.Zero);
            FailIf(!entrance.HasState || entrance.BlocksGameplay || Door() != 0xec ||
                _currentRoom.IsSolid(_player.Position),
                "Crown Dungeon $0:$0a must arm $90:$11 with a solid $ec keyhole and a walkable approach.");
            FailIf(_currentRoom.GetBackgroundSubtileForValidation(12, 0) != 0x26 ||
                _currentRoom.GetBackgroundSubtileForValidation(17, 3) != 0x27,
                "roomTileChangesAfterLoad07 did not restore the closed Crown Dungeon facade.");
        }
        Arrange();
        StepGameplayUpdates(70, Vector2.Up);
        FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != "Huh? This has a\nkeyhole." ||
            _saveData.HasRoomFlag(0, 0x0a, 0x80) || entrance.BlocksGameplay,
            "Walking into Crown Dungeon $0:$0a without treasure $43 must show TX_5109 without unlocking.");
        _dialogue.Close();
        StepGameplayUpdates(12, Vector2.Up);
        FailIf(_dialogue.IsOpen, "Crown Dungeon keyhole repeated TX_5109 in the same visit.");
        _inventory.GiveTreasure(0x43, 1);

        foreach (bool batched in new[] { false, true })
        {
            Arrange();
            // Approach on the actual floor. Stop at the shared controller's final push update.
            for (int i = 0; i < 80 && _keyholes.RemainingPushFrames != 2; i++)
                StepGameplayUpdates(1, Vector2.Up);
            FailIf(_keyholes.RemainingPushFrames != 2 || entrance.BlocksGameplay ||
                _saveData.HasRoomFlag(0, 0x0a, 0x80),
                $"Crown Dungeon keyhole did not reach the final doubled push-counter boundary from its floor: batched={batched}, position={_player.Position}, room={_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}, transition={_transitions.IsTransitioning}, counter={_keyholes.RemainingPushFrames}, blocked={entrance.BlocksGameplay}.");
            _sound.ClearPlayRequestAudit();
            StepGameplayUpdates(1, Vector2.Up);
            Vector2 lockedPosition = _player.Position;
            int randomCalls = _entities.RandomCalls;
            FailIf(!entrance.BlocksGameplay || !_player.CutsceneControlled ||
                !_saveData.HasRoomFlag(0, 0x0a, 0x80) || !_inventory.HasTreasure(0x43) ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 1 || entrance.Counter != 0 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopMusic) != 1,
                "Crown Key $43 did not retain the key, set room $80 and stop music before the opening wait.");
            StepGameplayUpdates(1, Vector2.Up);
            FailIf(entrance.Counter != 60, "Crown Dungeon wait 60 was not loaded after setmusic's yield.");
            StepGameplayUpdates(59, Vector2.Up, batched: batched);
            FailIf(entrance.Phase != 0 || entrance.Counter != 1 || Door() != 0xec ||
                _player.Position != lockedPosition || _entities.RandomCalls != randomCalls ||
                !_roomEvents.FreezesNonInteractionObjects,
                "Crown Dungeon opened or moved Link before its first 60-update wait elapsed.");
            for (int phase = 1; phase <= 3; phase++)
            {
                StepGameplayUpdates(1, Vector2.Up);
                FailIf(entrance.Phase != phase || entrance.Counter != 30 || Door() != 0xec ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != phase ||
                    _entities.Entities<PuzzlePuffEffect>().Count(p => p.Flickers) != 4,
                    $"Crown Dungeon drawing phase {phase} missed its 30-update wait, SND_DOORCLOSE, or four $05:$81 puffs.");
                byte expected = phase switch { 1 => 0x4d, 2 => 0x5d, _ => 0x3a };
                FailIf(_currentRoom.GetBackgroundSubtileForValidation(12, 3) != expected,
                    $"Crown Dungeon phase {phase} did not apply its source BG rectangle.");
                FailIf(_currentRoom.GetBackgroundAttributeForValidation(17, 3) != 0x2c ||
                    _entities.ScreenShakeCounter != 14 || _entities.HorizontalScreenShakeCounter != 14 ||
                    _entities.RandomCalls != randomCalls + (phase - 1) * 30 + 2,
                    $"Crown Dungeon phase {phase} lost BG flips or the first Y/X shake RNG draws.");
                StepGameplayUpdates(29, Vector2.Up, batched: batched);
                FailIf(Door() != 0xec || entrance.Counter != 1 || !entrance.BlocksGameplay ||
                    _entities.RandomCalls != randomCalls + phase * 30,
                    $"Crown Dungeon phase {phase} changed collision before its wait completed.");
            }
            StepGameplayUpdates(1, Vector2.Up);
            FailIf(Door() != 0xee || entrance.Counter != 45 || !entrance.BlocksGameplay,
                "Crown Dungeon settilehere did not install door $ee at packed position $17 before the shared tail.");
            StepGameplayUpdates(44, Vector2.Up, batched: batched);
            FailIf(!entrance.BlocksGameplay || !_player.CutsceneControlled,
                "Crown Dungeon released input before miscPuzzles_justOpenedKeyDoor's 45-update wait.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!entrance.BlocksGameplay || _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0,
                "Crown Dungeon resetmusic did not yield before SND_SOLVEPUZZLE.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!entrance.BlocksGameplay || _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
                "Crown Dungeon playsound did not yield before enableinput.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(entrance.HasState || _player.CutsceneControlled || _roomEvents.FreezesNonInteractionObjects ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
                "Crown Dungeon opening did not restore gameplay and play SND_SOLVEPUZZLE exactly once.");
            StepGameplayUpdates(60, Vector2.Up, batched: batched);
            FailIf(_rooms.ActiveGroup == 0 && _currentRoom.Id == 0x0a,
                "The opened Crown Dungeon doorway was not enterable through the real player/warp loop.");
            FinishEntry();
            LoadValidationRoom(0, 0x0a);
            FailIf(entrance.HasState || Door() != 0xee || !_inventory.HasTreasure(0x43),
                "Crown Dungeon did not retain its open entrance and Crown Key on re-entry.");
            _player.WarpTo(new Vector2(0x78, 0x48), recordSafe: false);
            StepGameplayUpdates(60, Vector2.Up, batched: batched);
            FinishEntry();
            FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 1,
                "Crown Dungeon replayed the key-use event on a repeated approach.");
        }

        Arrange();
        for (int i = 0; i < 80 && !entrance.BlocksGameplay; i++) StepGameplayUpdates(1, Vector2.Up);
        StepGameplayUpdates(65, Vector2.Zero);
        LoadValidationRoom(0, 0x11);
        FailIf(entrance.HasState || _player.CutsceneControlled || _roomCamera.Offset != Vector2.Zero,
            "Cancelling Crown Dungeon opening leaked input control or camera shake.");
        LoadValidationRoom(0, 0x0a);
        FailIf(Door() != 0xee || entrance.HasState,
            "Crown Dungeon's key-use flag did not survive cancellation and re-entry.");
        GD.Print("Validated Crown Dungeon $0:$0a key approach, missing-key text, opening phases, collision, entry, persistence and cancellation with single and batched gameplay updates.");
    }
}
