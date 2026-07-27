using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Identifies placed NPC actors whose imported save predicates may be
/// refreshed live. Script-created cutscene actors deliberately do not opt in;
/// the adapter's implementation classification remains independent.
/// </summary>
internal interface IOrdinaryNpcEntity
{
    NpcCharacter Npc { get; }
}
