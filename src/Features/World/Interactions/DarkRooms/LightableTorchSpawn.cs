namespace Oracles;

internal sealed record LightableTorchSpawn(LightableTorchState State,int PackedPosition)
    : RoomEntitySpawn(UpdateThisFrame:true);
