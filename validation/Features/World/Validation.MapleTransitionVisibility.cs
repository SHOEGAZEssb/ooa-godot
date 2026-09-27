using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapleTransitionVisibility()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.TuniNutPlaced);
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
