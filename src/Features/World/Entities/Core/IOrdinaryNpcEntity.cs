using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Identifies ordinary placed NPCs whose imported save predicates may be
/// refreshed live. Script-created cutscene actors deliberately do not opt in.
/// </summary>
internal interface IOrdinaryNpcEntity
{
    NpcCharacter Npc { get; }
}
