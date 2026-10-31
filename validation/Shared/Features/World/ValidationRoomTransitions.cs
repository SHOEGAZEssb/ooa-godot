using Godot;
using System.Reflection;

namespace Oracles;

internal static class ValidationRoomTransitions
{
    private static readonly FieldInfo Rooms = typeof(RoomTransitionController).GetField("_rooms",BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Entities = typeof(RoomTransitionController).GetField("_entities",BindingFlags.Instance | BindingFlags.NonPublic)!;

    // Direct scroll fixtures stage screenTransitionState2's ordinary boundary
    // write. Actual gameplay and forced requests exercise their real producer.
    internal static void BeginValidationScroll(this RoomTransitionController owner, Player player, Vector2I direction, int destination)
    {
        var room = ((RoomSession)Rooms.GetValue(owner)!).CurrentRoom;
        bool horizontal = direction.X != 0;
        int boundary = direction == Vector2I.Up || direction == Vector2I.Left ? 6 : horizontal ? room.Width - 6 : room.Height - 7;
        var transitionOwner = ((RoomEntityManager)Entities.GetValue(owner)!).PlayerScreenTransitionOwner;
        if (transitionOwner is null) player.SetScreenTransitionBoundaryCoordinate(horizontal,boundary);
        else transitionOwner.SetScreenTransitionBoundaryCoordinate(horizontal,boundary,player);
        owner.BeginScroll(player,direction,destination);
    }
}
