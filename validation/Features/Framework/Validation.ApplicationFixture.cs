using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateApplicationValidationFixture()
    {
        ValidateShardPlanning();
        List<(long Update, bool Held, bool Edge)>? split = null;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x60);
            _entities.Clear();
            _player.WarpTo(new(72, 40));
            var trace = new List<(long Update, bool Held, bool Edge)>();
            _entities.AddEntity(new ItemPhaseValidationEntity(() =>
            {
                FailIf(!Input.OriginalUpdateActive,
                    "Gameplay entities must observe the current application's input snapshot.");
                trace.Add((Application.UpdateCount,
                    Input.IsActionPressed("attack"), Input.IsActionJustPressed("attack")));
            }));

            Application.Capture(["attack"], ["attack"], Vector2.Zero);
            FailIf(Application.Advance(0.5 / 60.0) != 0 || trace.Count != 0 ||
                Application.UpdateCount != 0 || Math.Abs(Application.Remainder - 0.5) > 1e-9,
                "A fractional host sample must retain its edge without advancing gameplay.");
            Application.Capture(["attack"], [], Vector2.Zero);
            FailIf(Application.Advance(0.5 / 60.0) != 1,
                "The next fractional host sample must complete exactly one original update.");

            int observations = 0;
            Application.Step(3, Vector2.Zero, held: ["attack"], batched: batched,
                afterUpdate: () =>
                {
                    observations++;
                    FailIf(Input.OriginalUpdateActive || trace.Count != observations + 1,
                        "The fixture observer must run once after each complete gameplay update.");
                });
            Application.Step(1, Vector2.Zero);
            Application.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"]);

            // specialObjects.s:updateGameKeysPressed computes just-pressed
            // as current & ~previous. Held input has no repeated edge;
            // release/repress produces a new edge on its owning update.
            (long Update, bool Held, bool Edge)[] expected =
            [ (1, true, true), (2, true, false), (3, true, false),
              (4, true, false), (5, false, false), (6, true, true) ];
            FailIf(observations != 3 || !trace.SequenceEqual(expected) ||
                Application.UpdateCount != 6 || Math.Abs(Application.Remainder) > 1e-9,
                "The real gameplay loop must consume buffered edges once and preserve exact update order.");
            if (split is null) split = trace;
            else FailIf(!trace.SequenceEqual(split),
                "Split and batched fixture calls must produce identical gameplay input traces.");
        }

        var failure = new InvalidOperationException("Fixture failure probe");
        _entities.Clear();
        _entities.AddEntity(new ItemPhaseValidationEntity(() => throw failure));
        try
        {
            Application.Step(1, Vector2.Zero);
            FailIf(true, "The fixture must propagate gameplay failures.");
        }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, failure)) { }
        FailIf(Input.OriginalUpdateActive,
            "A failed gameplay update must release its input snapshot scope.");

        ApplicationValidationFixture fixture = Application;
        Application.Capture(["attack"], ["attack"], Vector2.Zero);
        Application.Advance(0.5 / 60.0);
        ReinitializeGameplayForValidation();
        FailIf(!ReferenceEquals(fixture, Application) || fixture.UpdateCount != 0 || fixture.Remainder != 0,
            "The fixture must remain usable across session resets without retaining counters or partial time.");
        _entities.Clear();
        _entities.AddEntity(new ItemPhaseValidationEntity(() =>
            FailIf(Input.IsActionPressed("attack") || Input.IsActionJustPressed("attack"),
                "A fresh session must discard the prior session's pending input.")));
        fixture.Step(1, Vector2.Zero);
        FailIf(fixture.UpdateCount != 1 || Input.OriginalUpdateActive,
            "The reused fixture must advance the replacement gameplay graph and release input.");
        GD.Print("Validated the shared application fixture against live gameplay input, split/batched updates, failure cleanup and session reset.");
    }
}
