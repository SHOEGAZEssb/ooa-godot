using Godot;

namespace oracleofages;

/// <summary>
/// Creates the interaction selected by tryToBreakTile_body's effect byte.
/// Sources which suppress or defer that interaction do not call this helper.
/// </summary>
internal static class BreakableTileEffectSpawn
{
    internal static RoomEntitySpawn? Create(
        OracleRoomData room,
        Vector2 position,
        int effect)
    {
        int interaction = effect & 0x0f;
        bool flickers = (effect & 0x10) != 0;
        if (interaction is 0x06 or 0x0c)
            return new RockDebrisSpawn(position, interaction);
        if (interaction is 0x00 or 0x01)
        {
            return new GrassDebrisSpawn(
                position,
                interaction,
                flickers,
                (room.TilesetFlags & 0x40) != 0);
        }
        return null;
    }
}
