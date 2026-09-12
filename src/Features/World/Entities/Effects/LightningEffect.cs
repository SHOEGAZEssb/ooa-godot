using Godot;
using System;
using System.Linq;

namespace oracleofages;

/// <summary>PART_LIGHTNING $27 with no related target. The caller owns its part-phase updates.</summary>
internal sealed class LightningEffect
{
    private static readonly GeneratedTableRow Data = GeneratedTable.Load(
        "res://assets/oracle/effects/lightning_native.tsv",
        new GeneratedTableSchema("PART_LIGHTNING $27", GeneratedTableKeySemantics.Ordered,
            ["z-offsets", "frames", "shake", "debris-offsets"], headerRequired: true)).SingleRow();
    private static readonly int[] Heights = Bytes(Data.RequiredString(0));
    private static readonly int[][] Frames = Pairs(Data.RequiredString(1));
    private static readonly int[][] Debris = Pairs(Data.RequiredString(3));
    private readonly NpcCharacter _actor;
    private readonly Func<int> _random;
    private readonly Action<int> _sound;
    private readonly Action<int> _shake;
    private readonly Action<Vector2> _debris;
    private int _state, _randomOffset, _frame, _counter = Frames[0][0];
    private int _parameter = Frames[0][1];
    internal bool Finished { get; private set; }
    internal LightningEffect(NpcCharacter actor, Func<int> random, Action<int> sound,
        Action<int> shake, Action<Vector2> debris)
    {
        _actor = actor; _random = random; _sound = sound; _shake = shake; _debris = debris;
        actor.SetAnimationRate(0); actor.SetScriptVisible(false);
        actor.SetFixedDrawPriority(NpcCharacter.InFrontOfLinkZIndex); // objectSetVisible81
    }
    internal void UpdateFrame()
    {
        if (Finished) return;
        if (_state == 0)
        {
            _randomOffset = _random() & 6;
            _actor.SetScriptDrawOffset(new Vector2(0, -64)); _state = 1; return;
        }
        if (_state == 1)
        {
            _sound(OracleSoundEngine.SndLightning); _actor.SetScriptVisible(true); _state = 2; return;
        }
        _actor.AdvanceAnimationUpdates(1);
        if (--_counter == 0)
        {
            _frame++; _counter = Frames[_frame][0]; _parameter = Frames[_frame][1];
            if (_parameter == 0xff) { Cancel(); return; }
        }
        if ((_parameter & 0x80) != 0)
        {
            _parameter &= 0x7f;
            int[] offset = Debris[(_randomOffset + (_parameter & 0x0e) - 2) >> 1];
            _debris(_actor.Position + new Vector2(unchecked((sbyte)offset[1]), unchecked((sbyte)offset[0])));
        }
        _actor.SetScriptDrawOffset(new Vector2(0, unchecked((sbyte)Heights[(_parameter & 0x70) >> 4])));
        if ((_parameter & 1) != 0) { _parameter--; _shake(Data.HexByte(2)); }
        // var03 is zero for the bomb-fairy helper, so it does not write $cfd2.
    }
    internal void Cancel() { Finished = true; if (GodotObject.IsInstanceValid(_actor)) _actor.SetActive(false); }
    private static int[] Bytes(string text) => text.Split(',').Select(v => Convert.ToInt32(v, 16)).ToArray();
    private static int[][] Pairs(string text) => text.Split(',').Select(p => p.Split(':').Select(v => Convert.ToInt32(v, 16)).ToArray()).ToArray();
}
