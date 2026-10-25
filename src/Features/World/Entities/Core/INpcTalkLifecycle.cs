using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface INpcTalkLifecycle
{
    NpcCharacter TalkNpc { get; }
    void OnNpcTalkStarted();
    void OnNpcTalkEnded();
}
