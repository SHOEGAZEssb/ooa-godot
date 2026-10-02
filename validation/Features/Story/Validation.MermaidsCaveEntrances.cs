using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidsCaveEntrances()
    {
        var entrance = _roomEvents.Get<MermaidsCaveEntranceEvent>();
        _inventory.GiveTreasure(TreasureId.Sword, 1);
        _inventory.EquipA(TreasureId.Sword);
        foreach (var (group, room, key, otherKey, destination) in new[]
        {
            (3, 0x0f, 0x44, 0x45, 0x44),
            (1, 0x0e, 0x45, 0x44, 0x26)
        })
        {
            string source = $"Mermaid's Cave {group:x}:{room:x2} / $90:$12";
            Vector2 doorPosition = new(0x68, 0x18);
            byte Door() => _currentRoom.GetMetatile(doorPosition);
            void Arrange()
            {
                _saveData.SetRoomFlag(group, room, 0x80, false);
                LoadValidationRoom(group, room);
                _player.WarpTo(new Vector2(0x68, 0x38), recordSafe: false);
                StepGameplayUpdates(2, Vector2.Zero);
                FailIf(!entrance.HasState || entrance.BlocksGameplay || Door() != 0xae ||
                    _currentRoom.IsSolid(_player.Position) || !_currentRoom.IsSolid(doorPosition),
                    $"{source} must arm on a walkable approach below solid keyhole $ae: tile=${Door():x2}, position={_player.Position}.");
            }
            void ReachFinalPush()
            {
                for (int i = 0; i < 100 && _keyholes.RemainingPushFrames != 2; i++)
                    StepGameplayUpdates(1, Vector2.Up);
                FailIf(_keyholes.RemainingPushFrames != 2 || entrance.BlocksGameplay ||
                    _saveData.HasRoomFlag(group, room, 0x80),
                    $"{source} did not reach its final doubled push update through collision: position={_player.Position}, counter={_keyholes.RemainingPushFrames}.");
            }
            void FinishEntry()
            {
                for (int i = 0; i < 180 && _transitions.IsTransitioning; i++)
                    StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_transitions.IsTransitioning || _rooms.ActiveGroup != 5 || _currentRoom.Id != destination,
                    $"{source} doorway did not enter 5:{destination:x2}: actual {_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}.");
            }

            // These expectations come from interactableTiles.s's group tables,
            // mainData.s placements, and miscPuzzles' script, not generated rows.
            FailIf(!_keyholes.Database.TryGet(group, room, out var record) || record.Treasure != key ||
                record.SubId != (group == 3 ? 2 : 3) || record.TileBase != 0x12 ||
                record.Palette != (group == 3 ? 5 : 4),
                $"{source} lost its distinct key ${key:x2} or $18 sprite visual.");
            _inventory.LoseTreasure(key);
            _inventory.LoseTreasure(otherKey);
            Arrange();
            StepGameplayUpdates(70, Vector2.Up);
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != "Huh? This has a\nkeyhole." ||
                _saveData.HasRoomFlag(group, room, 0x80) || entrance.BlocksGameplay,
                $"{source} must show TX_5109 without key ${key:x2}: position={_player.Position}, counter={_keyholes.RemainingPushFrames}, collisions={_currentRoom.ActiveCollisions}, room={_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}, dialogue={_dialogue.IsOpen}/{_dialogue.CurrentMessage}, flags=${_saveData.GetRoomFlags(group, room):x2}, tiles=" +
                string.Join(",", Enumerable.Range(0, 5).Select(y => _currentRoom.GetMetatile(new Vector2(0x68, y * 16 + 8)).ToString("x2"))));
            _dialogue.Close();
            StepGameplayUpdates(12, Vector2.Up);
            FailIf(_dialogue.IsOpen, $"{source} repeated TX_5109 in the same visit.");
            _inventory.GiveTreasure(otherKey, 1);
            Arrange();
            StepGameplayUpdates(70, Vector2.Up);
            FailIf(!_dialogue.IsOpen || _saveData.HasRoomFlag(group, room, 0x80) || entrance.BlocksGameplay,
                $"{source} incorrectly accepted the other era's key ${otherKey:x2}.");
            _dialogue.Close();
            _inventory.GiveTreasure(key, 1);

            foreach (bool batched in new[] { false, true })
            {
                Arrange();
                ReachFinalPush();
                // Neutral wLinkAngle rejects the preceding pushing signal
                // before the final doubled decrement can open the keyhole.
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_keyholes.RemainingPushFrames != 20 || entrance.BlocksGameplay,
                    $"{source} retained partial push progress after breaking contact.");
                ReachFinalPush();
                // Arrange a live parent at the handoff boundary. Subsequent
                // freeze/release checks run through the actual gameplay loop.
                _player.StartSwordAttack();
                _player.AdvanceSwordForValidation(17, buttonHeld: true);
                FailIf(_player.SwordState != SwordActionState.Held,
                    $"{source} failed to arrange the held sword parent for its $81 handoff.");
                int roomMusic = _sound.ActiveMusic;
                _sound.ClearPlayRequestAudit();
                StepGameplayUpdates(1, Vector2.Up);
                Vector2 lockedPosition = _player.Position;
                int swordFrame = _player.SwordStateFrame;
                int randomCalls = _entities.RandomCalls;
                FailIf(!entrance.BlocksGameplay || _player.CutsceneControlled || !_roomEvents.MenusDisabled ||
                    !_roomEvents.FreezesNonInteractionObjects || !_saveData.HasRoomFlag(group, room, 0x80) ||
                    !_inventory.HasTreasure(key) || _sound.PlayRequestsFor(SoundId.SndOpenChest) != 1 ||
                    _sound.PlayRequestsFor(SoundId.SndCtrlStopMusic) != 0 || entrance.Counter != 0 ||
                    !_player.IsAttacking ||
                    _entities.Entities<OverworldKeyUseEffect>().Single().Position != doorPosition,
                    $"{source} key use failed to retain ${key:x2}, set flag $80, spawn $18, yield at checkcfc0bit or acquire $81 control.");
                StepGameplayUpdates(1, Vector2.Up);
                FailIf(entrance.Counter != 0 || _sound.PlayRequestsFor(SoundId.SndCtrlStopMusic) != 1,
                    $"{source} must stop music on the update after scriptCmd_checkCFC0Bit's signal-read yield.");
                StepGameplayUpdates(1, Vector2.Up);
                FailIf(entrance.Counter != 60, $"{source} did not load wait 60 after setmusic's yield.");
                StepGameplayUpdates(59, Vector2.Up, batched: batched);
                FailIf(Door() != 0xae || entrance.Counter != 1 || _player.Position != lockedPosition ||
                    _entities.RandomCalls != randomCalls || !_player.IsAttacking || _player.SwordStateFrame != swordFrame,
                    $"{source} opened, moved Link or advanced non-interaction RNG before wait 60 elapsed.");
                StepGameplayUpdates(1, Vector2.Up);
                FailIf(Door() != 0xae || entrance.Counter != 0 || _sound.PlayRequestsFor(SoundId.SndDoorClose) != 1,
                    $"{source} must yield at SND_DOORCLOSE on wait 60's zero update before changing tile $16.");
                StepGameplayUpdates(1, Vector2.Up);
                FailIf(Door() != 0xaf || _currentRoom.IsSolid(doorPosition) || entrance.Counter != 0 || !entrance.BlocksGameplay,
                    $"{source} settilehere must install walkable $af then yield at scriptjump.");
                StepGameplayUpdates(1, Vector2.Up);
                FailIf(entrance.Counter != 45, $"{source} did not load the shared wait 45 after the jump yield.");
                StepGameplayUpdates(44, Vector2.Up, batched: batched);
                FailIf(entrance.Counter != 1 || !entrance.BlocksGameplay || _player.Position != lockedPosition ||
                    _entities.RandomCalls != randomCalls || _entities.Entities<OverworldKeyUseEffect>().Count != 0,
                    $"{source} released input early, advanced frozen objects or retained the expired key sprite.");
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(!entrance.BlocksGameplay || _sound.ActiveMusic != roomMusic || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
                    $"{source} resetmusic must restore this era's track and yield before SND_SOLVEPUZZLE.");
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(!entrance.BlocksGameplay || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1,
                    $"{source} must yield at SND_SOLVEPUZZLE before enableinput.");
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(entrance.HasState || _player.CutsceneControlled || _roomEvents.MenusDisabled ||
                    _roomEvents.FreezesNonInteractionObjects || !_inventory.HasTreasure(key) ||
                    !_player.IsAttacking || _player.SwordStateFrame != swordFrame,
                    $"{source} failed to release control on enableinput/scriptend.");
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_player.IsAttacking && _player.SwordStateFrame == swordFrame,
                    $"{source} did not resume the frozen sword parent after enableinput.");
                StepGameplayUpdates(60, Vector2.Up, batched: batched);
                FinishEntry();
                LoadValidationRoom(group, room);
                FailIf(entrance.HasState || Door() != 0xaf || !_inventory.HasTreasure(key),
                    $"{source} failed its persistent singleTileChanges.s $80/$16/$af substitution.");
                _player.WarpTo(new Vector2(0x68, 0x38), recordSafe: false);
                StepGameplayUpdates(60, Vector2.Up, batched: batched);
                FinishEntry();
                FailIf(_sound.PlayRequestsFor(SoundId.SndOpenChest) != 1 || _sound.PlayRequestsFor(SoundId.SndDoorClose) != 1,
                    $"{source} replayed key use on a repeated doorway approach.");
            }

            foreach (int elapsed in new[] { 10, 65 })
            {
                Arrange();
                ReachFinalPush();
                StepGameplayUpdates(1, Vector2.Up);
                StepGameplayUpdates(elapsed, Vector2.Zero);
                LoadValidationRoom(0, 0x11);
                FailIf(entrance.HasState || _player.CutsceneControlled || _roomEvents.MenusDisabled,
                    $"{source} cancellation after {elapsed} updates leaked input/menu ownership.");
                LoadValidationRoom(group, room);
                FailIf(entrance.HasState || Door() != 0xaf || !_inventory.HasTreasure(key),
                    $"{source} lost its unlock flag or key on cancellation/re-entry.");
            }
        }
        GD.Print("Validated Mermaid's Cave 3:0f and 1:0e keyholes, distinct retained keys, source waits/jump yield, real collision approach and entry, repeated entry, cancellation and single/batched gameplay updates.");
    }
}
