using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record LightableTorchSpawn(
    DarkRoomState State,
    int PackedPosition)
    : RoomEntitySpawn(UpdateThisFrame: true);
