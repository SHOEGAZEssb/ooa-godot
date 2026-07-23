using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface ILinkContactEntity
{
    void HandleLinkContact(Player player);
}
