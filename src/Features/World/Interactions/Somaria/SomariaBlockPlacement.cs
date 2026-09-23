using Godot;

namespace oracleofages;

// Owns ITEM18.var32 only. The tile underneath is always the room's shared
// layout buffer, which buttons and other native handlers may change in place.
internal sealed class SomariaBlockPlacement(OracleRoomData room, SomariaPlacementDatabase data)
{
    internal int PackedPosition { get; private set; } = -1;
    private Vector2 TilePosition => new((PackedPosition&15)*16+8,(PackedPosition>>4)*16+8);
    internal bool CanAppear(int group,Vector2 position,int z)
    {
        if (!data.RoomAllowed(group,room.Id) || !data.HeightAllowed(z) ||
            room.GetTerrainInfo(position).Collision!=0 || (room.TilesetFlags&0x40)!=0) return false;
        return SupportedBelow(position);
    }
    internal bool SupportedBelow(Vector2 position)
    {
        if ((room.TilesetFlags&0x20)==0) return true;
        int below=(room.GetPackedPosition(position)+0x10)&0xff;
        return room.GetTerrainInfo(new((below&15)*16+8,(below>>4)*16+8)).Collision==data.Collision;
    }
    internal bool TryCreate(int group,Vector2 position,int z,long tick)
    {
        if (!CanAppear(group,position,z)) return false;
        Vector2 aligned=data.Align(position);
        byte previous=room.GetMetatile(aligned);
        if (data.Hazard(room.ActiveCollisions,previous)!=0) return false;
        PackedPosition=room.GetPackedPosition(aligned);
        room.SetUnderlyingMetatile(aligned,previous);
        // The native code writes layout/collision bytes directly; it does
        // not replace the background mapping underneath the object sprite.
        room.SetPositionTileAndCollision(aligned,data.Tile,data.Collision,tick,preserveRenderedTile:true);
        return true;
    }
    internal bool InPlace => PackedPosition>=0 && room.GetMetatile(TilePosition)==data.Tile &&
        room.GetTerrainInfo(TilePosition).Collision==data.Collision;
    internal bool Remove(long tick)
    {
        if (!InPlace) return false;
        room.SetPositionTileAndCollision(TilePosition,room.GetUnderlyingMetatile(TilePosition),null,tick);
        return true;
    }
}
