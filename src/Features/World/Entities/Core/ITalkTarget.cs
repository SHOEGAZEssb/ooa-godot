using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface ITalkTarget
{
    NpcCharacter? FindTalkTarget(Player player);
}
