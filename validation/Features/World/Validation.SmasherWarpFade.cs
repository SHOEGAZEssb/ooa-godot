using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmasherWarpFade()
    {
        foreach (bool batch in new[] { false, true })
        {
            _saveData.SetRoomFlag(4, 0xb4, 0x80, false);
            LoadValidationRoom(4, 0xb4);
            _player.WarpTo(new(48, 136));
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, [], [], batch);
            Step(2);
            var ball = _entities.Entities<SmasherCharacter>().Single(actor => actor.IsBall);
            var parent = _entities.Entities<SmasherCharacter>().Single(actor => !actor.IsBall);
            int ballState = ball.State, parentState = parent.State;
            int expiration = ball.ExpirationCounter, counter = parent.Counter1;
            Vector2 ballPosition = ball.Position, parentPosition = parent.Position;
            _transitions.ApplyWarpWithFadeOut(_player, new Warp(4, 0xb4, -1, 0, 0, 4, 0xb4, 0x83, 0, 0));
            Step(31);
            FailIf(ball.State != ballState || parent.State != parentState ||
                ball.Position != ballPosition || parent.Position != parentPosition ||
                ball.ExpirationCounter != expiration || parent.Counter1 != counter,
                "Initialized Smasher slots must freeze AI, movement and timers during source palette fade.");
            Step(1);
            var arrivals = _entities.Entities<SmasherCharacter>();
            FailIf(arrivals.Count != 2 || arrivals.Any(actor => actor.State != 8) || arrivals.Contains(ball),
                "Destination state0 must initialize both linked Smasher slots during fade-in.");
            ball = arrivals.Single(actor => actor.IsBall);
            parent = arrivals.Single(actor => !actor.IsBall);
            expiration = ball.ExpirationCounter;
            Step(31);
            FailIf(ball.State != 8 || parent.State != 8 || ball.ExpirationCounter != expiration ||
                ball.Position != new Vector2(88, 88) || parent.Position != new Vector2(120, 88),
                "Destination Smasher must remain frozen through the visually transparent, nonterminal fade update.");
            Step(1);
            FailIf(ball.State != 9 || parent.State != 10,
                "Terminal fade-in must resume ball then parent in the same enemy pass.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
