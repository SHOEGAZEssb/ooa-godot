using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class InteractionController
{
    private enum FamilyNamingState
    {
        None,
        AwaitOpeningClose,
        NameEntry,
        AwaitConfirmation,
        AwaitInvalidClose,
        ThanksDelay,
        AwaitThanksClose
    }

    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly SignDatabase _signs;
    private readonly ChestDatabase _chests;
    private readonly TreasureDatabase _treasures;
    private readonly DialogueBox _dialogue;
    private readonly Node _worldRoot;
    private readonly RoomView _roomView;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly InventoryState _inventory;
    private readonly BipinBlossomFamilyInteractionDatabase _familyInteractions = new();
    private readonly KidNameEntryController _kidNameEntry;
    private readonly Dictionary<int, ChestDatabase.ChestRecord> _debugChestOverrides = new();
    private ChestTreasureEffect? _chestTreasure;
    private ChestDatabase.ChestRecord _pendingChest;
    private FamilyNamingState _familyNamingState;
    private string _pendingChildName = string.Empty;
    private float _familyLinkScreenY;
    private double _familyWaitTicks;

    public Func<NpcCharacter, bool>? NpcInteractionOverride { get; set; }

    public bool DialogueOpen => _dialogue.BlocksPlayerInput ||
        _chestTreasure is not null ||
        _familyNamingState != FamilyNamingState.None ||
        _kidNameEntry.Active;
    public bool GameplayMenuActive => _kidNameEntry.Active;
    internal bool ChestRewardActive => _chestTreasure is not null;

    public InteractionController(
        RoomSession rooms,
        RoomEntityManager entities,
        SignDatabase signs,
        ChestDatabase chests,
        TreasureDatabase treasures,
        DialogueBox dialogue,
        Node worldRoot,
        RoomView roomView,
        Func<Vector2, Vector2> worldToScreen,
        Func<long> animationTick,
        InventoryState inventory,
        Node interfaceLayer,
        Action<int>? playSound = null)
    {
        _rooms = rooms;
        _entities = entities;
        _signs = signs;
        _chests = chests;
        _treasures = treasures;
        _dialogue = dialogue;
        _worldRoot = worldRoot;
        _roomView = roomView;
        _worldToScreen = worldToScreen;
        _animationTick = animationTick;
        _inventory = inventory;
        _kidNameEntry = new KidNameEntryController(interfaceLayer, playSound);
        _rooms.RoomChanged += ApplyOpenedChestState;
        ApplyOpenedChestState(_rooms.ActiveGroup, _rooms.CurrentRoom);
    }

    public void Update(double delta, Player player)
    {
        _kidNameEntry.Update();
        UpdateFamilyNaming(delta);

        if (_chestTreasure is null)
            return;

        if (!_chestTreasure.Finished)
        {
            _chestTreasure.Advance(delta);
            if (!_chestTreasure.Finished)
                return;

            // treasure.s:@m3State1 gives the treasure and opens its text after
            // the 32-frame rise, then falls through to @m3State2 without
            // deleting the still-visible interaction.
            _inventory.GiveTreasure(new TreasureDatabase.TreasureObjectRecord(
                _pendingChest.TreasureObject,
                _pendingChest.TreasureId,
                _pendingChest.SubId,
                _pendingChest.Parameter,
                _pendingChest.TextId,
                _pendingChest.Graphic,
                _pendingChest.Message));
            if (!string.IsNullOrEmpty(_pendingChest.Message))
                _dialogue.ShowMessage(_pendingChest.Message, _worldToScreen(player.Position).Y);
            return;
        }

        // treasure.s:@m3State2 waits for wTextIsActive to clear. Keep the
        // reward at its final raised position until the player closes the
        // pickup textbox.
        if (_dialogue.IsOpen)
            return;

        _worldRoot.RemoveChild(_chestTreasure);
        _chestTreasure.QueueFree();
        _chestTreasure = null;
    }

    public bool TryInteract(Player player)
    {
        NpcCharacter? npc = _entities.FindTalkTarget(player);
        if (npc != null)
        {
            if (TryStartFamilyNaming(npc, player))
                return true;
            if (NpcInteractionOverride?.Invoke(npc) == true)
                return true;
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

    private bool TryStartFamilyNaming(NpcCharacter npc, Player player)
    {
        if (npc.Record is not { Id: 0x2b, SubId: 0x00 } ||
            _rooms.SaveData.ChildNamed ||
            _familyNamingState != FamilyNamingState.None)
        {
            return false;
        }

        npc.FaceToward(player.Position);
        _familyLinkScreenY = _worldToScreen(player.Position).Y;
        _dialogue.ShowMessage(npc.Message, _familyLinkScreenY);
        _familyNamingState = FamilyNamingState.AwaitOpeningClose;
        return true;
    }

    private void UpdateFamilyNaming(double delta)
    {
        switch (_familyNamingState)
        {
            case FamilyNamingState.None:
                return;

            case FamilyNamingState.AwaitOpeningClose:
                if (_dialogue.IsOpen)
                    return;
                _kidNameEntry.Open(_rooms.SaveData.ChildName);
                _familyNamingState = FamilyNamingState.NameEntry;
                return;

            case FamilyNamingState.NameEntry:
                if (!_kidNameEntry.TryTakeResult(out string name))
                    return;
                if (string.IsNullOrEmpty(name))
                {
                    var invalid = _familyInteractions.Text(0x440a, _rooms.SaveData);
                    _dialogue.ShowMessage(invalid.Message, _familyLinkScreenY);
                    _familyNamingState = FamilyNamingState.AwaitInvalidClose;
                    return;
                }
                _pendingChildName = name;
                var confirmation = _familyInteractions.Text(
                    0x4407, _rooms.SaveData, _pendingChildName);
                _dialogue.ShowChoiceMessage(confirmation.Message, _familyLinkScreenY);
                _familyNamingState = FamilyNamingState.AwaitConfirmation;
                return;

            case FamilyNamingState.AwaitConfirmation:
                if (!_dialogue.TryTakeChoiceResult(out int choice))
                    return;
                if (choice != 0)
                {
                    _kidNameEntry.Open(_pendingChildName);
                    _familyNamingState = FamilyNamingState.NameEntry;
                    return;
                }
                _rooms.SaveData.NameChild(_pendingChildName);
                RefreshNamedFamilyDialogue();
                _familyWaitTicks = 0.0;
                _familyNamingState = FamilyNamingState.ThanksDelay;
                return;

            case FamilyNamingState.AwaitInvalidClose:
                if (!_dialogue.IsOpen)
                    _familyNamingState = FamilyNamingState.None;
                return;

            case FamilyNamingState.ThanksDelay:
                _familyWaitTicks += delta * 60.0;
                if (_familyWaitTicks < 30.0)
                    return;
                var thanks = _familyInteractions.Text(0x4408, _rooms.SaveData);
                _dialogue.ShowMessage(thanks.Message, _familyLinkScreenY);
                _familyNamingState = FamilyNamingState.AwaitThanksClose;
                return;

            case FamilyNamingState.AwaitThanksClose:
                if (!_dialogue.IsOpen)
                    _familyNamingState = FamilyNamingState.None;
                return;
        }
    }

    private void RefreshNamedFamilyDialogue()
    {
        foreach (NpcCharacter npc in _entities.Entities<NpcCharacter>())
        {
            int textId = npc.Record switch
            {
                { Id: 0x28, SubId: 0x00 } => 0x4301,
                { Id: 0x2b, SubId: 0x00 } => 0x4409,
                _ => 0
            };
            if (textId == 0)
                continue;
            var dialogue = _familyInteractions.Text(textId, _rooms.SaveData);
            npc.SetDialogue(dialogue.TextId, dialogue.Message, canFace: false);
        }
    }

    internal bool FamilyNamingActive =>
        _familyNamingState != FamilyNamingState.None || _kidNameEntry.Active;
    internal MainMenuScreen? KidNameScreenForValidation =>
        _kidNameEntry.ScreenForValidation;
    internal void CommitKidNameForValidation(string name) =>
        _kidNameEntry.CommitForValidation(name);
    internal void UpdateFamilyNamingForValidation(double delta) =>
        UpdateFamilyNaming(delta);

    public void ResetChestForTesting(int group, int roomId, int position) =>
        ResetChestForTesting(group, roomId, position, null);

    public void ResetChestForTesting(int group, int roomId, int position, string? treasureObjectName)
    {
        _rooms.SaveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlagItem, value: false);
        if (treasureObjectName is not null)
        {
            TreasureDatabase.TreasureObjectRecord treasure = _treasures.GetObject(treasureObjectName);
            _debugChestOverrides[MakeChestKey(group, roomId, position)] = new ChestDatabase.ChestRecord(
                group,
                roomId,
                position,
                treasure.Name,
                treasure.TreasureId,
                treasure.SubId,
                treasure.Parameter,
                treasure.TextId,
                treasure.Graphic,
                0,
                treasure.Message);
        }
        OracleRoomData room = _rooms.World.LoadRoom(group, roomId);
        Vector2 point = PointForPackedPosition(position);
        byte current = room.GetMetatile(point);
        if (current != 0xf1)
            room.ReplaceMetatile(point, current, 0xf1, _animationTick());
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
        if (!_debugChestOverrides.TryGetValue(MakeChestKey(_rooms.ActiveGroup, room.Id, position),
                out ChestDatabase.ChestRecord chest) &&
            !_chests.TryGet(_rooms.ActiveGroup, room.Id, position, out chest))
        {
            chest = new ChestDatabase.ChestRecord(
                _rooms.ActiveGroup, room.Id, position,
                "TREASURE_OBJECT_RUPEES_00", TreasureDatabase.TreasureRupees, 0x00,
                0x01, 0x01, 0x2b, 1,
                "You got 1 Rupee!\n...");
        }
        if (!room.ReplaceMetatile(tilePoint, 0xf1, 0xf0, _animationTick()))
            return true;

        _rooms.SaveData.SetRoomFlag(
            _rooms.ActiveGroup, room.Id, OracleSaveData.RoomFlagItem);
        _roomView.QueueRedraw();
        _pendingChest = chest;
        _chestTreasure = new ChestTreasureEffect { ZIndex = 12 };
        _chestTreasure.Initialize(
            PointForPackedPosition(position) + new Vector2(0, -8),
            _treasures.GetTreasureDisplay(chest.TreasureId, chest.Parameter, _inventory));
        _worldRoot.AddChild(_chestTreasure);
        return true;
    }

    private void ApplyOpenedChestState(int group, OracleRoomData room)
    {
        if (!_rooms.SaveData.HasRoomFlag(group, room.Id, OracleSaveData.RoomFlagItem))
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

    private static int MakeChestKey(int group, int room, int position) =>
        (group << 16) | (room << 8) | position;
}
