using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidatePuzzlePuffTiming()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var counter = typeof(RoomEntityManager).GetField("_enemyFrameCounter", flags)!;
        foreach (bool batch in new[] { false, true })
        foreach (int initialFrame in new[] { 0xfe, 0xff })
        foreach (string mode in new[] { "normal", "text", "freeze", "scroll" })
        {
            LoadValidationRoom(4, 0xbc);
            _player.WarpTo(new(24, 24));
            _entities.Clear();
            counter.SetValue(_entities, initialFrame);
            void Step(int count = 1)
            {
                input.CaptureForValidation([], [], Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0, update);
            }
            PuzzlePuffEffect Puff() => _entities.Spawn<PuzzlePuffEffect>(
                new PuzzlePuffSpawn(new(24, 24), 0, Flickers: true));
            var even = Puff();
            var odd = Puff();
            FailIf(_entities.InteractionSlot(even) != 2 || _entities.InteractionSlot(odd) != 3,
                "Puffs must use native dynamic slots $d2/$d3.");
            var oldFreeze = _entities.NonInteractionObjectsDisabledSource;
            try
            {
                if (mode == "text") _dialogue.ShowMessage("Puff timing.", 120);
                if (mode == "freeze") _entities.NonInteractionObjectsDisabledSource = () => true;
                if (mode == "scroll") _entities.BeginScreenTransition(4, _currentRoom, new(240, 0), _player);
                Step();
                FailIf(!even.Visible || !odd.Visible || even.ElapsedUpdates != 1 || odd.ElapsedUpdates != 1,
                    "INTERAC$05 state0 must show both slot parities before state1 flickering.");
                Step(2);
                // breakTileDebris.s: state1 XORs the global frame with d;
                // bit0 clear is visible. This also crosses the byte wrap.
                int expectedFrame = (initialFrame + 3) & 0xff;
                FailIf(_entities.FrameCounter != expectedFrame || even.Visible != ((expectedFrame & 1) == 0) ||
                    odd.Visible != ((expectedFrame & 1) == 1),
                    $"Puff flicker lost global frame/slot parity during {mode} (batch={batch}).");
                var later = Puff();
                // Incoming 4:bc allocates its chest in $d4 while the old
                // puffs retain $d2/$d3. Its four buttons use PART slots.
                int laterSlot = mode == "scroll" ? 5 : 4;
                FailIf(_entities.InteractionSlot(later) != laterSlot,
                    $"Unexpected later puff allocation during {mode}.");
                Step(2);
                expectedFrame = (initialFrame + 5) & 0xff;
                FailIf(later.ElapsedUpdates != 2 || later.Visible != (((expectedFrame ^ laterSlot) & 1) == 0) ||
                    later.Visible != (mode == "scroll" ? odd.Visible : even.Visible),
                    $"Different-age puffs in matching slot parities must share the global phase during {mode}.");
                Step(14);
                FailIf(even.Finished || even.CurrentParameter != 0xff || odd.Finished,
                    "Puffs must retain the terminal animation parameter through update19.");
                Step();
                FailIf(!even.Finished || !odd.Finished || later.Finished,
                    "Puffs must delete on update20 without deleting a younger allocation.");
                var reused = Puff();
                FailIf(_entities.InteractionSlot(reused) != 2,
                    "A deleted puff must release native slot $d2 for immediate reuse.");
                Step(2);
                expectedFrame = (initialFrame + 22) & 0xff;
                FailIf(reused.Visible != ((expectedFrame & 1) == 0),
                    "Reusing a puff slot must retain the global phase instead of restarting a local phase.");
            }
            finally
            {
                _entities.NonInteractionObjectsDisabledSource = oldFreeze;
                _dialogue.Close();
                if (mode == "scroll") _entities.FinishScreenTransition();
                LoadValidationRoom(0, 0x60);
            }
        }
    }
}
