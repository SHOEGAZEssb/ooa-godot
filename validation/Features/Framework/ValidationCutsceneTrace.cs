using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;
internal sealed class ValidationCutsceneTrace : ICutsceneCommandTraceSink
{
    public List<CutsceneCommandTraceEntry> Entries { get; } = new();
    public List<CutsceneObservationTraceEntry> Observations { get; } = new();

    public void Record(CutsceneCommandTraceEntry entry) => Entries.Add(entry);
    public void RecordObservation(CutsceneObservationTraceEntry entry) => Observations.Add(entry);
    public bool Saw(string observation, string? actor = null, int? value = null) => Observations.Any(entry => entry.Observation == observation && (actor is null || entry.Actor?.Value == actor) && (!value.HasValue || entry.Value == value.Value));
    public int LastValue(string observation) => Observations.Last(entry => entry.Observation == observation).Value;
    public int OrValues(string observation) => Observations.Where(entry => entry.Observation == observation).Aggregate(0, (mask, entry) => mask | entry.Value);
    public int Count(string observation) => Observations.Count(entry => entry.Observation == observation);
    public bool SawPosition(string observation, string actor, Vector2 position) => Observations.Any(entry => entry.Observation == observation && entry.Actor?.Value == actor && entry.Position.IsEqualApprox(position));
}
