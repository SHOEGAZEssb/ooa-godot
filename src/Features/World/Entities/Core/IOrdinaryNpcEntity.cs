using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>
/// Identifies placed NPC actors whose imported save predicates may be
/// refreshed live. Script-created cutscene actors deliberately do not opt in;
/// the adapter's implementation classification remains independent.
/// </summary>
internal interface IOrdinaryNpcEntity
{
    NpcCharacter Npc { get; }
    bool RefreshStoryState => true;
    bool RefreshVisibility => true;
    bool RefreshPosition => true;
    void CompleteStoryInitialization() { }
    bool TryQueueDialogue(Func<bool> tryShowDialogue) => false;
}
