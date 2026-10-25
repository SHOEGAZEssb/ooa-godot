using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface IRoomBlocker
{
    bool BlocksLink(Vector2 linkCenter);
}
