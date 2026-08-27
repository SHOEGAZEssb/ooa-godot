namespace oracleofages;

internal sealed class ShadowHagShadowRoomEntity(ShadowHagShadowEffect effect)
    : FixedEffectRoomEntityAdapter<ShadowHagShadowEffect>(effect);

internal sealed record ShadowHagShadowSpawn(
    ShadowHagBoss Owner,
    int AngleIndex) : RoomEntitySpawn(UpdateThisFrame: true);
