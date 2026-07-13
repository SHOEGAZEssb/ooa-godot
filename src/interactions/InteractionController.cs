using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class InteractionController
{
    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly SignDatabase _signs;
    private readonly ChestDatabase _chests;
    private readonly DialogueBox _dialogue;
    private readonly Node _worldRoot;
    private readonly RoomView _roomView;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _addRupees;
    private readonly HashSet<int> _openedChestRooms = new();
    private ChestTreasureEffect? _chestTreasure;
    private ChestDatabase.ChestRecord _pendingChest;

    public bool DialogueOpen => _dialogue.BlocksPlayerInput || _chestTreasure is not null;
    internal bool ChestRewardActive => _chestTreasure is not null;

    public InteractionController(
        RoomSession rooms,
        RoomEntityManager entities,
        SignDatabase signs,
        ChestDatabase chests,
        DialogueBox dialogue,
        Node worldRoot,
        RoomView roomView,
        Func<Vector2, Vector2> worldToScreen,
        Func<long> animationTick,
        Action<int> addRupees)
    {
        _rooms = rooms;
        _entities = entities;
        _signs = signs;
        _chests = chests;
        _dialogue = dialogue;
        _worldRoot = worldRoot;
        _roomView = roomView;
        _worldToScreen = worldToScreen;
        _animationTick = animationTick;
        _addRupees = addRupees;
        _rooms.RoomChanged += ApplyOpenedChestState;
        ApplyOpenedChestState(_rooms.ActiveGroup, _rooms.CurrentRoom);
    }

    public void Update(double delta, Player player)
    {
        if (_chestTreasure is null)
            return;

        _chestTreasure.Advance(delta);
        if (!_chestTreasure.Finished)
            return;

        _worldRoot.RemoveChild(_chestTreasure);
        _chestTreasure.QueueFree();
        _chestTreasure = null;
        _addRupees(_pendingChest.Amount);
        _dialogue.ShowMessage(_pendingChest.Message, _worldToScreen(player.Position).Y);
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
        Vector2 tilePoint = player.Position + (Vector2)player.FacingVector * 8.0f;
        byte tile = room.GetMetatile(tilePoint);
        if (tile == 0xf1)
            return TryOpenChest(player, tilePoint);
        if (tile != 0xf2)
            return false;

        string message;
        if (player.FacingVector != Vector2I.Up)
            message = "You can't read\nit from here!"; // TX_510e
        else if (!_signs.TryGetMessage(
            _rooms.ActiveGroup, room.Id, room.GetPackedPosition(tilePoint), out message!))
            message = "Nothing is written\nhere."; // TX_0901 fallback

        _dialogue.ShowMessage(message, _worldToScreen(player.Position).Y);
        return true;
    }

    public void ResetChestForTesting(int group, int roomId, int position)
    {
        _openedChestRooms.Remove(MakeRoomKey(group, roomId));
        OracleRoomData room = _rooms.World.LoadRoom(group, roomId);
        room.ReplaceMetatile(PointForPackedPosition(position), 0xf0, 0xf1, _animationTick());
        if (_rooms.ActiveGroup == group && _rooms.CurrentRoom.Id == roomId)
            _roomView.QueueRedraw();
    }

    private bool TryOpenChest(Player player, Vector2 tilePoint)
    {
        if (player.FacingVector != Vector2I.Up)
        {
            _dialogue.ShowMessage("It won't open\nfrom this side!", _worldToScreen(player.Position).Y); // TX_510d
            return true;
        }

        OracleRoomData room = _rooms.CurrentRoom;
        int position = room.GetPackedPosition(tilePoint);
        if (!_chests.TryGet(_rooms.ActiveGroup, room.Id, position, out ChestDatabase.ChestRecord chest))
        {
            chest = new ChestDatabase.ChestRecord(
                _rooms.ActiveGroup, room.Id, position,
                "TREASURE_OBJECT_RUPEES_00", true, 1, 0x0001,
                "You got 1 Rupee!\n...");
        }
        if (!chest.Supported)
        {
            _dialogue.ShowMessage(
                "This chest's item\nisn't implemented yet.",
                _worldToScreen(player.Position).Y);
            return true;
        }
        if (!room.ReplaceMetatile(tilePoint, 0xf1, 0xf0, _animationTick()))
            return true;

        _openedChestRooms.Add(MakeRoomKey(_rooms.ActiveGroup, room.Id));
        _roomView.QueueRedraw();
        _pendingChest = chest;
        _chestTreasure = new ChestTreasureEffect { ZIndex = 12 };
        _chestTreasure.Initialize(PointForPackedPosition(position) + new Vector2(0, -8));
        _worldRoot.AddChild(_chestTreasure);
        return true;
    }

    private void ApplyOpenedChestState(int group, OracleRoomData room)
    {
        if (!_openedChestRooms.Contains(MakeRoomKey(group, room.Id)))
            return;

        foreach (ChestDatabase.ChestRecord chest in _chests.GetRoomRecords(group, room.Id))
        {
            room.ReplaceMetatile(
                PointForPackedPosition(chest.Position), 0xf1, 0xf0, _animationTick());
        }
    }

    private static Vector2 PointForPackedPosition(int position) => new(
        (position & 0x0f) * OracleRoomData.MetatileSize + 8,
        (position >> 4) * OracleRoomData.MetatileSize + 8);

    private static int MakeRoomKey(int group, int room) => (group << 8) | room;
}
