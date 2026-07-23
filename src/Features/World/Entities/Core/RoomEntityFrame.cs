using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal readonly record struct RoomEntityFrame(
    Player Player,
    int Counter,
    bool AnyButtonJustPressed);
