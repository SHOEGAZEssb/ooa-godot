using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed class FakeOctorokState(FakeOctorokRecord record, NpcCharacter actor)
{
    public FakeOctorokRecord Record { get; } = record;
    public NpcCharacter Actor { get; } = actor;
    public FakeOctorokStage Stage { get; set; }
    public int Counter { get; set; }
}
