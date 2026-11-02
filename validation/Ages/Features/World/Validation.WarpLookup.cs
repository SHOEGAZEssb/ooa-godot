
namespace Oracles;

public sealed partial class ValidationRoot
{
    // Navigation fixtures request both native phases together; gameplay keeps
    // the index pending until its actual destination-load update.
    private bool TryGetResolvedTileWarp(WarpDatabase database, int group, OracleRoomData room, int position, byte tile, out Warp warp)
    {
        if (!database.TryGetTileWarp(group,room,position,tile,out warp)) return false;
        warp = database.ResolveDestination(warp,_runtimeState);
        return true;
    }
    private bool TryGetResolvedEdgeWarp(WarpDatabase database, int group, int room, Vector2I direction, Vector2 position, out Warp warp)
    {
        if (!database.TryGetEdgeWarp(group,room,direction,position,out warp)) return false;
        warp = database.ResolveDestination(warp,_runtimeState);
        return true;
    }
}
