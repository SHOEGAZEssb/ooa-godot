using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Observes the real host immediately after a script motion call, before a
// different actor or phase can replace its shared temporary bytes. Expected
// arithmetic and publication execute the clean ROM's objectApplySpeed.
internal sealed class ScriptMovementPublicationTrace(
    OracleRuntimeState memory,
    Func<CutsceneCommandSource, (int Speed, int Angle, Vector2 Position)?> sample,
    Func<object>? lane = null,
    bool comparePositions = true)
    : ICutsceneCommandTraceSink
{
    private readonly Dictionary<(object Lane, CutsceneCommandSource Source), ObjectMotionRom> _expected = [];
    private readonly Dictionary<(object Lane, CutsceneCommandSource Source), int> _setupUpdates = [];
    internal HashSet<CutsceneCommandSource> Visited { get; } = [];
    internal int MotionVisits { get; private set; }

    public void Record(CutsceneCommandTraceEntry entry)
    {
        if (sample(entry.Source) is not { } state) return;
        var key = (lane?.Invoke() ?? this, entry.Source);
        const int obj = ObjectMotionRom.Object + 0x40;
        if (entry.Phase == CutsceneCommandTracePhase.Started)
        {
            var rom = new ObjectMotionRom();
            rom.Word(obj + 10, (int)(state.Position.Y * 256));
            rom.Word(obj + 12, (int)(state.Position.X * 256));
            _expected[key] = rom; _setupUpdates[key] = entry.ScriptUpdate;
            return;
        }
        if (entry.Counter == 0 || _setupUpdates[key] == entry.ScriptUpdate) return;
        ObjectMotionRom expected = _expected[key];
        expected[obj + 9] = (byte)state.Angle; expected[obj + 0x10] = (byte)state.Speed;
        expected.Call(ObjectMotionRom.ApplySpeed, slot: 0x40);
        if (comparePositions && state.Position != new Vector2(expected.Word(obj + 12) / 256f, expected.Word(obj + 10) / 256f))
            throw new InvalidOperationException($"{entry.Source}: host position {state.Position} differs from native script movement.");
        for (int i = 0; i < 4; i++)
            if (memory.ReadWramByte(0xcec0 + i) != expected[0xcec0 + i])
                throw new InvalidOperationException($"{entry.Source}: movement scratch ${0xcec0 + i:x4} differs before the next actor/phase.");
        Visited.Add(entry.Source); MotionVisits++;
    }
}
