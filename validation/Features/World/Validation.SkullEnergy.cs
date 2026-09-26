using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullEnergyBeads()
    {
        var data = BlueEnergyBeadDatabase.Shared;
        FailIf(data.Count != 8 || data.Speed != 0x78 || data.Radius != 0x38 || data.DelayMask != 7 || data.DeleteAddress != 0xcd2d,
            "PART_BLUE_ENERGY_BEAD lost its source eight-slot allocation, SPEED_300, radius38, delay mask07 or cd2d signal.");
        // SPEED_100 signed vectors times $38, taking only each high byte.
        Vector2[] starts = [new(120,32), new(159,48), new(176,88), new(159,127),
            new(120,144), new(80,127), new(64,88), new(80,48)];
        int[] vx = [0,-543,-768,-543,0,543,768,543];
        int[] vy = [768,543,0,-543,-768,-543,0,543];
        var originalText = _entities.TextActiveSource;
        try
        {
            foreach (bool batch in new[] { false, true })
            {
                void Step(int count = 1) =>
                    StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
                LoadValidationRoom(4, 0x69);
                _player.WarpTo(new(120, 140));
                _entities.TextActiveSource = () => true;
                var prediction = new OracleRandom(); prediction.RestoreState(_random.CaptureState());
                int[] delays = Enumerable.Range(0, 8).Select(_ => (prediction.Next().Value & 7) + 1).ToArray();
                var beads = _entities.CreateEnergySwirl(new(120.75f, 88.5f));
                FailIf(!beads.Select(b => b.Index).SequenceEqual(new[] {7,6,5,4,3,2,1,0}) ||
                    beads.Any(b => b.Initialized || b.Visible), "Swirl allocation must assign7..0 without running PART state0 or drawing.");
                _entities.RuntimeState.SetWramByte(WramAddress.wDeleteEnergyBeads, 1);
                Step();
                FailIf(_random.Calls != prediction.Calls || _entities.RuntimeState.ReadWramByte(WramAddress.wDeleteEnergyBeads) != 0 ||
                    beads.Where((b, i) => !b.Initialized || b.Visible || b.Delay != delays[i]).Any(),
                    "PART state0 under text must clear the delete signal and draw eight ordered RNG delays without decrementing them.");
                bool[] seen = new bool[8];
                for (int tick = 1; tick <= 10; tick++)
                {
                    Step();
                    for (int n = 0; n < 8; n++)
                    {
                        var bead = beads[n]; int index = bead.Index;
                        FailIf(bead.Visible != (tick >= delays[n]), "Energy bead delay boundary changed under text.");
                        if (tick < delays[n]) continue;
                        int moves = tick - delays[n];
                        var expected = starts[index] + new Vector2(vx[index], vy[index]) * (moves / 256.0f);
                        FailIf(bead.PrecisePosition != expected,
                            $"Energy bead${index:x2} lost native whole-byte start or SPEED_300 movement: {bead.PrecisePosition} != {expected}.");
                        seen[index] = true;
                    }
                }
                FailIf(seen.Any(value => !value), "Some source energy directions never became visible.");
                var diagonal = beads.Single(b => b.Index == 1);
                for (int i = 0; diagonal.Visible && i < 24; i++) Step();
                var retainedFraction = diagonal.PrecisePosition - diagonal.PrecisePosition.Floor();
                FailIf(retainedFraction == Vector2.Zero, "The diagonal flight must accumulate a source fraction before restarting.");
                for (int i = 0; !diagonal.Visible && i < 8; i++) Step();
                FailIf(!diagonal.Visible || diagonal.PrecisePosition != starts[1] + retainedFraction,
                    "Circle restart must replace only high bytes, preserving the previous flight's fractional bytes.");
                _entities.RuntimeState.SetWramByte(WramAddress.wDeleteEnergyBeads, 1);
                Step();
                FailIf(_entities.Entities<BlueEnergyBeadRoomEntity>().Count != 0 || _entities.RuntimeState.ReadWramByte(WramAddress.wDeleteEnergyBeads) != 1,
                    "The shared delete signal must remove all initialized beads under text without clearing itself.");
                // Fourteen occupied native PART slots leave only two: allocation
                // stops immediately, retaining descending indices7 and6.
                for (int i = 0; i < 14; i++) _entities.Spawn(new BlueEnergyBeadSpawn(i & 7, new(120,88), 1));
                var partial = _entities.CreateEnergySwirl(new(120,88), 0);
                FailIf(!partial.Select(b => b.Index).SequenceEqual(new[] {7,6}) ||
                    _entities.CreateEnergySwirl(new(120,88)).Count != 0,
                    "Energy creation must stop at the first failed native PART allocation.");
                Step(); // state0: duration is untouched, deletion flag clears.
                FailIf(_entities.Entities<BlueEnergyBeadRoomEntity>().Count != 16, "Duration1 expired during state0.");
                Step();
                FailIf(_entities.Entities<BlueEnergyBeadRoomEntity>().Count != 2 || partial.Any(b => b.Duration != 0xff),
                    "Duration1 expires next update; duration00 wraps toff and remains infinite.");
                Step(3);
                FailIf(partial.Any(b => b.Duration != 0xff), "Durationff must stay infinite.");
                LoadValidationRoom(4, 0x91);
                FailIf(_entities.Entities<BlueEnergyBeadRoomEntity>().Count != 0, "Room replacement retained energy PARTs.");
            }
        }
        finally { _entities.TextActiveSource = originalText; }
    }
}
