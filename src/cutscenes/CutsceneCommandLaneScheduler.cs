using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Advances independently imported actor scripts once per fixed update in
/// stable insertion order. Each lane keeps its own instruction, counters,
/// registers, call stack, and yield boundaries.
/// </summary>
internal sealed class CutsceneCommandLaneScheduler(ICutsceneCommandHost host)
{
    private sealed record Lane(string Name, CutsceneCommandRunner Runner);

    private readonly List<Lane> _lanes = new();
    private readonly Dictionary<string, Lane> _byName =
        new(StringComparer.Ordinal);

    public int Count => _lanes.Count;
    public bool Active
    {
        get
        {
            foreach (Lane lane in _lanes)
            {
                if (lane.Runner.Active)
                    return true;
            }
            return false;
        }
    }

    public CutsceneCommandRunner StartLane(
        string name,
        IReadOnlyList<CutsceneCommand> commands)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A cutscene lane name cannot be empty.", nameof(name));
        if (_byName.ContainsKey(name))
            throw new InvalidOperationException($"Cutscene lane '{name}' is already registered.");

        var runner = new CutsceneCommandRunner(host);
        runner.Start(commands);
        var lane = new Lane(name, runner);
        _lanes.Add(lane);
        _byName.Add(name, lane);
        return runner;
    }

    public CutsceneCommandRunner GetLane(string name) =>
        _byName.TryGetValue(name, out Lane? lane)
            ? lane.Runner
            : throw new InvalidOperationException($"Unknown cutscene lane '{name}'.");

    public void AdvanceFrame()
    {
        // Object order is observable in the ROM. Never compact or reorder this
        // list while dispatching an update.
        foreach (Lane lane in _lanes)
        {
            if (lane.Runner.Active)
                lane.Runner.AdvanceFrame();
        }
    }

    public void Clear()
    {
        foreach (Lane lane in _lanes)
            lane.Runner.Clear();
        _lanes.Clear();
        _byName.Clear();
    }
}
