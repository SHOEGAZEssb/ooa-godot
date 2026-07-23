using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface ITalkTarget
{
    NpcCharacter? FindTalkTarget(Player player);
}
