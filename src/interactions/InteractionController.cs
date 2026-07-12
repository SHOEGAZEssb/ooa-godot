using Godot;
using System;

namespace oracleofages;

public sealed class InteractionController
{
    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly SignDatabase _signs;
    private readonly DialogueBox _dialogue;
    private readonly Func<Vector2, Vector2> _worldToScreen;

    public bool DialogueOpen => _dialogue.IsOpen;

    public InteractionController(
        RoomSession rooms,
        RoomEntityManager entities,
        SignDatabase signs,
        DialogueBox dialogue,
        Func<Vector2, Vector2> worldToScreen)
    {
        _rooms = rooms;
        _entities = entities;
        _signs = signs;
        _dialogue = dialogue;
        _worldToScreen = worldToScreen;
    }

    public bool TryInteract(Player player)
    {
        NpcCharacter? npc = _entities.FindTalkTarget(player);
        if (npc != null)
        {
            npc.FaceToward(player.Position);
            _dialogue.ShowMessage(npc.Message, _worldToScreen(player.Position).Y);
            return true;
        }

        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 signPoint = player.Position + (Vector2)player.FacingVector * 8.0f;
        if (room.GetMetatile(signPoint) != 0xf2)
            return false;

        string message;
        if (player.FacingVector != Vector2I.Up)
            message = "You can't read it\nfrom this side!"; // TX_510e
        else if (!_signs.TryGetMessage(
            _rooms.ActiveGroup, room.Id, room.GetPackedPosition(signPoint), out message!))
            message = "Nothing is written\nhere."; // TX_0901 fallback

        _dialogue.ShowMessage(message, _worldToScreen(player.Position).Y);
        return true;
    }
}
