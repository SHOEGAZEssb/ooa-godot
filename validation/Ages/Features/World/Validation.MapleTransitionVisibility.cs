using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMapleCutsceneGates()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Essence, 7);
            var remote = _roomEvents.Get<RemoteMakuEighthEssenceEvent>();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _saveData.SetMapleKillCounter(30);
                LoadValidationRoom(1, 0x5c);
                var maple = _entities.Entities<MapleEncounter>().Single();
                StepGameplayUpdates(1, Vector2.Zero);
                MapleEncounterStage stage = maple.Stage;
                Vector2 position = maple.Position;
                StepGameplayUpdates(24, Vector2.Zero, batched: batched);
                FailIf(!remote.HasState || maple.Stage != stage || maple.Position != position ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0x81,
                    "Remote Maku $8a/v$0a must freeze Maple through disableinput on repeated incomplete entries.");
                remote.Cancel();
                FailIf(remote.HasState || _player.CutsceneControlled || _entities.PlayerMenusDisabled ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                    _saveData.HasRoomFlag(1, 0x5c, 0x40),
                    "Cancelling remote Maku must release its masks without marking room$1:$5c complete.");
                StepGameplayUpdates(4, Vector2.Zero, batched: batched);
                FailIf(maple.Finished || maple.Stage != MapleEncounterStage.Flying,
                    "Cancelling remote Maku must release the waiting Maple encounter.");
            }

            // mapleState1 checks only wMenuDisabled on its zero decrement;
            // DISABLE_COMPANION ($20) and DISABLE_ALL_BUT_INTERACTIONS ($80)
            // instead suspend even Maple's uninitialized state.
            _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced);
            foreach (byte mask in new byte[] { 0x20, 0x80, 0x81 })
            {
                _saveData.SetMapleKillCounter(30);
                LoadValidationRoom(1, 0x02);
                var maple = _entities.Entities<MapleEncounter>().Single();
                int calls = _entities.RandomCalls;
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, mask);
                StepGameplayUpdates(8, Vector2.Zero, batched: batched);
                FailIf(maple.Stage != MapleEncounterStage.Initializing || maple.Visible || _entities.RandomCalls != calls,
                    $"Maple state0 must retain entrance RNG and visibility under wDisabledObjects=${mask:x2}.");
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
                _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0x80);
                int meetings = _saveData.MapleState;
                StepGameplayUpdates(1, Vector2.Zero);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(maple.Finished || maple.Stage != MapleEncounterStage.EntryDelay,
                    "Maple must not sample wMenuDisabled before the third entrance decrement.");
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(!maple.Finished || maple.Visible || _entities.MaplePresent() ||
                    _saveData.MapleState != meetings || _saveData.MapleKillCounter != 0 ||
                    _sound.ActiveMusic == SoundId.MusMapleTheme ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0x80,
                    "Maple's menu-only abort must delete on decrement three, retain spent kills and meeting count, and preserve the menu owner's mask.");
                _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0);
            }
        }
    }

    private void ValidateMapleTransitionVisibility()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced);
            LoadValidationRoom(1, 0x02);
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(80, 80));
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.Clear();
                _saveData.SetMapleKillCounter(30);
                _entities.BeginScreenTransition(1, _currentRoom, new(160, 0), _player);
                MapleEncounter maple = _entities.Entities<MapleEncounter>().Single();
                int randomCalls = _entities.RandomCalls;
                FailIf(maple.Visible || maple.Stage != MapleEncounterStage.Initializing,
                    "Incoming Maple must remain hidden: companionRetIfInactiveWithoutStateCheck gates state0 during scrolling.");
                for (int update = 0; update < 32; update += 2)
                {
                    _entities.SetScreenTransitionOffsets(new(-update * 5, 0), new(160 - update * 5, 0));
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                    FailIf(maple.Visible || maple.Stage != MapleEncounterStage.Initializing ||
                        maple.ShadowDrawn || _entities.RandomCalls != randomCalls,
                        "Scrolling must not expose Maple at (0,0), initialize her path, or consume entry RNG.");
                }
                if (cancel)
                {
                    LoadValidationRoom(1, 0x02);
                    FailIf(_entities.Entities<MapleEncounter>().Count != 0,
                        "Replacing an incoming Maple screen must discard the hidden encounter.");
                    continue;
                }
                _entities.FinishScreenTransition();
                FailIf(maple.Visible || maple.Stage != MapleEncounterStage.Initializing,
                    "Scroll completion must not initialize Maple before the next eligible gameplay update.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                // mapleState0 writes yh=$10, xh=$b8, zh=$88, animation $19;
                // its counter starts at 3 without decrementing in state0.
                FailIf(!maple.Visible || maple.Position != new Vector2(0xb8, 0x10) ||
                    maple.AnimationIndex != 0x19 || maple.Stage != MapleEncounterStage.EntryDelay ||
                    _entities.RandomCalls != randomCalls + 1,
                    "Maple must become visible only with initialized entrance coordinates and one source RNG draw.");
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(maple.Stage != MapleEncounterStage.EntryDelay,
                    "Maple's three-update entrance delay must not advance during scrolling.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(maple.Stage != MapleEncounterStage.Flying,
                    "Maple must start her flight on the third post-initialization update.");
            }
        }
        ReinitializeGameplayForValidation();
    }
}
