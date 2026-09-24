using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmasherNativeBytes()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(SmasherCharacter).GetProperty("State", flags)!;
        foreach (bool batch in new[] { false, true })
        {
            _saveData.SetRoomFlag(4, 0xb4, 0x80, false);
            LoadValidationRoom(4, 0xb4);
            _player.WarpTo(new(48, 136));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, [], [], batch);
            Step(2);
            var ball = _entities.Entities<SmasherCharacter>().Single(actor => actor.IsBall);
            var parent = _entities.Entities<SmasherCharacter>().Single(actor => !actor.IsBall);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                // ecom_decCounter1 is DEC (HL), whereas counter2 saturates.
                // Inject zero to isolate byte underflow without a long fight.
                state.SetValue(ball, 11);
                state.SetValue(parent, 9);
                parent.Counter1 = 0;
                Step(2);
                FailIf(parent.Counter1 != 254 || parent.State != 9,
                    "Smasher counter1 must wrap zero to$ff, then decrement to$fe while wandering.");
                state.SetValue(parent, 1);
                state.SetValue(ball, 14);
                ball.Counter1 = 0;
                Step(2);
                FailIf(ball.Counter1 != 254 || ball.State != 14,
                    "Smasher respawn counter1 must use byte wrapping rather than a negative integer.");
            }
            // The ball's pickup handler calls ecom_moveTowardPosition directly;
            // it must not call the parent's direction/animation helper.
            foreach (int side in new[] { 1, -1 })
            {
                state.SetValue(parent, 1);
                parent.Position = new(88 + side * 16, 88);
                state.SetValue(ball, 10);
                ball.Position = new(88, 88);
                ball.CopyCarriedPosition(new(88, 88), 0);
                int direction = ball.Direction, animation = ball.AnimationIndex;
                Step(2);
                FailIf(ball.Direction != direction || ball.AnimationIndex != animation ||
                    ball.Angle != (side == 1 ? 8 : 24) ||
                    ball.Position != new Vector2(88 + side * 1.25f, 88),
                    "Smasher pickup must move at SPEED_a0 and update angle without changing direction or animation.");
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
