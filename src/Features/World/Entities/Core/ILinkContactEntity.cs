using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface ILinkContactEntity
{
    void HandleLinkContact(Player player);
}
