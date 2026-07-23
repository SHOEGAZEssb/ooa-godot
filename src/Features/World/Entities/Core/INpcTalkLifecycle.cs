using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface INpcTalkLifecycle
{
    NpcCharacter TalkNpc { get; }
    void OnNpcTalkStarted();
    void OnNpcTalkEnded();
}
