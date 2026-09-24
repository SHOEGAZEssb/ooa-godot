using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmasherCommonStates()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(SmasherCharacter).GetProperty("State", flags)!;
        var expiration = typeof(SmasherCharacter).GetProperty("ExpirationCounter", flags)!;
        foreach (int nativeState in new[] { 1, 3, 4, 5, 6, 7 })
        {
            _saveData.SetRoomFlag(4, 0xb4, 0x80, false);
            LoadValidationRoom(4, 0xb4);
            _player.WarpTo(new(48, 136));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, [], [], false);
            Step(2);
            var ball = _entities.Entities<SmasherCharacter>().Single(actor => actor.IsBall);
            var parent = _entities.Entities<SmasherCharacter>().Single(actor => !actor.IsBall);
            state.SetValue(ball, nativeState);
            state.SetValue(parent, nativeState);
            expiration.SetValue(ball, 0);
            Vector2 ballPosition = ball.Position, parentPosition = parent.Position;
            ball.Counter1 = parent.Counter1 = 37;
            int ballZ = ball.ZFixed, parentZ = parent.ZFixed;
            // Native commonState jump table: RET, with the even-frame ball
            // timer executed before dispatch. Two updates consume one tick.
            Step(2);
            FailIf(ball.State != nativeState || parent.State != nativeState ||
                ball.Position != ballPosition || parent.Position != parentPosition ||
                ball.ZFixed != ballZ || parent.ZFixed != parentZ ||
                ball.Counter1 != 37 || parent.Counter1 != 37 || ball.ExpirationCounter != 1,
                $"Smasher common state${nativeState:x2} must preserve motion/counters except the shared expiration timer.");
            expiration.SetValue(ball, 179);
            Step();
            if (ball.State == nativeState) Step();
            FailIf(ball.State != 14 || ball.ExpirationCounter != 0 || ball.Counter1 != 60 || ball.Visible ||
                parent.State != nativeState || parent.Position != parentPosition,
                $"Ball expiration must dispatch disappearing state$0d before common-state${nativeState:x2}'s RET.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
