namespace Oracles;

internal sealed record BossDeathExplosionSpawn(Vector2 Position,int BossId,int ZHigh=0,int? NativeSubid=null)
    : RoomEntitySpawn(UpdateThisFrame:true);
