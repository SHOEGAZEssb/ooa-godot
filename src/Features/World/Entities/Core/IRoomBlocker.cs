using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IRoomBlocker
{
    bool BlocksLink(Vector2 linkCenter);
}
