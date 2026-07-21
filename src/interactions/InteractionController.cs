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

    private enum HardhatShovelState
    {
        None,
        AwaitOpeningClose,
        PreRewardWait,
        AwaitRewardClose,
        PostRewardWait,
        AwaitFinalClose,
        AwaitSimpleClose
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
    private readonly Action<int> _playSound;
    private readonly BipinBlossomFamilyInteractionDatabase _familyInteractions = new();
    private readonly BlackTowerWorkerDatabase _blackTower = new();
    private readonly KidNameEntryController _kidNameEntry;
    private readonly Dictionary<int, ChestDatabase.ChestRecord> _debugChestOverrides = new();
    private ChestTreasureEffect? _chestTreasure;
    private GroundTreasurePickup? _groundTreasure;
    private Player? _groundTreasurePlayer;
    private bool _groundTreasureCompletesHeartContainer;
    private bool _groundTreasureShowingHeartContainer;
    private ChestDatabase.ChestRecord _pendingChest;
    private FamilyNamingState _familyNamingState;
    private string _pendingChildName = string.Empty;
    private float _familyLinkScreenY;
    private double _familyWaitTicks;
    private NpcCharacter? _activeNpcTalkLifecycle;
    private HardhatShovelState _hardhatShovelState;
    private NpcCharacter? _hardhatNpc;
    private Player? _hardhatPlayer;
    private GroundTreasurePickup? _hardhatTreasure;
    private double _hardhatWaitTicks;

    public Func<NpcCharacter, bool>? NpcInteractionOverride { get; set; }

    public bool DialogueOpen => _dialogue.BlocksPlayerInput ||
        _chestTreasure is not null ||
        _groundTreasure is not null ||
        _hardhatShovelState != HardhatShovelState.None ||
        _familyNamingState != FamilyNamingState.None ||
        _kidNameEntry.Active;
    public bool GameplayMenuActive => _kidNameEntry.Active;
    internal bool ChestRewardActive => _chestTreasure is not null;
    internal ChestTreasureEffect? ChestReward => _chestTreasure;
    internal GroundTreasurePickup? GroundTreasureForValidation => _groundTreasure;

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
        _playSound = playSound ?? (static _ => { });
        _kidNameEntry = new KidNameEntryController(interfaceLayer, playSound);
        _dialogue.SetHeartPieceCountProvider(() => _inventory.HeartPieces);
        _dialogue.HeartPieceSetFilled += OnHeartPieceSetFilled;
        _dialogue.HeartPieceSetAccepted += OnHeartPieceSetAccepted;
        _rooms.RoomChanged += OnRoomChanged;
        _entities.GroundTreasureCollected += OnGroundTreasureCollected;
        _entities.GroundTreasureCollectionAllowed = () => !DialogueOpen;
        ApplyOpenedChestState(_rooms.ActiveGroup, _rooms.CurrentRoom);
    }

    public void Update(double delta, Player player)
    {
        if (_activeNpcTalkLifecycle is not null && !_dialogue.IsOpen &&
            _hardhatShovelState == HardhatShovelState.None)
        {
            _entities.EndNpcTalk(_activeNpcTalkLifecycle);
            _activeNpcTalkLifecycle = null;
        }
        _kidNameEntry.Update();
        UpdateFamilyNaming(delta);
        UpdateHardhatShovel(delta);

        if (_groundTreasure is not null)
        {
            if (_dialogue.IsOpen || !_groundTreasure.Held)
                return;
            if (_groundTreasureCompletesHeartContainer &&
                !_groundTreasureShowingHeartContainer)
                return;
            _groundTreasure.Finish(_groundTreasurePlayer!);
            _groundTreasure = null;
            _groundTreasurePlayer = null;
            _groundTreasureCompletesHeartContainer = false;
            _groundTreasureShowingHeartContainer = false;
        }

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
            var treasureObject = new TreasureDatabase.TreasureObjectRecord(
                _pendingChest.TreasureObject,
                _pendingChest.TreasureId,
                _pendingChest.SubId,
                _pendingChest.Parameter,
                _pendingChest.TextId,
                _pendingChest.Graphic,
                _pendingChest.Message);
            _inventory.GiveTreasure(treasureObject);
            int collectionSound = _treasures.GetBehaviour(
                treasureObject.TreasureId).Sound;
            if (collectionSound != 0)
                _playSound(collectionSound);
            if (!string.IsNullOrEmpty(_pendingChest.Message))
                _dialogue.ShowGameplayMessage(
                    _pendingChest.Message, _worldToScreen(player.Position).Y);
            _playSound(OracleSoundEngine.SndGetItem);
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

    private void OnHeartPieceSetFilled()
    {
        if (_groundTreasure is null || !_groundTreasureCompletesHeartContainer ||
            _groundTreasureShowingHeartContainer)
            return;
        _inventory.ResetCompletedHeartPieceSet();
    }

    private void OnHeartPieceSetAccepted()
    {
        if (_groundTreasure is null || !_groundTreasureCompletesHeartContainer ||
            _groundTreasureShowingHeartContainer)
            return;
        _inventory.GiveCompletedHeartContainer(
            _treasures.GetObject("TREASURE_OBJECT_HEART_CONTAINER_00"));
        _playSound(OracleSoundEngine.SndFilledHeartContainer);
        _dialogue.ShowGameplayMessage(
            _groundTreasure.Record.CompletionMessage,
            _worldToScreen(_groundTreasurePlayer!.Position).Y);
        _groundTreasureShowingHeartContainer = true;
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
            if (npc.Record is { Id: 0x58, SubId: 0x00 })
                return TryStartHardhatShovel(npc, player);
            if (_entities.BeginNpcTalk(npc))
                _activeNpcTalkLifecycle = npc;
            _dialogue.ShowGameplayMessage(
                npc.Message,
                _worldToScreen(player.Position).Y,
                npc.TextPosition);
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

        _dialogue.ShowGameplayMessage(message, _worldToScreen(player.Position).Y);
        return true;
    }

    private bool TryStartHardhatShovel(NpcCharacter npc, Player player)
    {
        if (_hardhatShovelState != HardhatShovelState.None)
            return false;

        _entities.BeginNpcTalk(npc);
        _activeNpcTalkLifecycle = npc;
        _hardhatNpc = npc;
        _hardhatPlayer = player;

        int textId;
        if (npc.Record.Var03 != 0)
        {
            textId = 0x1000;
            _hardhatShovelState = HardhatShovelState.AwaitSimpleClose;
        }
        else if (_rooms.SaveData.HasRoomFlag(
            _rooms.ActiveGroup, _rooms.CurrentRoom.Id, OracleSaveData.RoomFlagItem))
        {
            textId = 0x1002;
            _hardhatShovelState = HardhatShovelState.AwaitSimpleClose;
        }
        else
        {
            textId = 0x1001;
            _hardhatShovelState = HardhatShovelState.AwaitOpeningClose;
        }
        npc.SetDialogue(textId, _blackTower.Text(textId), canFace: true);
        _dialogue.ShowGameplayMessage(
            npc.Message, _worldToScreen(player.Position).Y, npc.TextPosition);
        return true;
    }

    private void UpdateHardhatShovel(double delta)
    {
        switch (_hardhatShovelState)
        {
            case HardhatShovelState.None:
                return;

            case HardhatShovelState.AwaitSimpleClose:
                if (!_dialogue.IsOpen)
                    FinishHardhatShovel();
                return;

            case HardhatShovelState.AwaitOpeningClose:
                if (_dialogue.IsOpen)
                    return;
                _hardhatWaitTicks = 0.0;
                _hardhatShovelState = HardhatShovelState.PreRewardWait;
                return;

            case HardhatShovelState.PreRewardWait:
                _hardhatWaitTicks += delta * 60.0;
                if (_hardhatWaitTicks < _blackTower.TalkWait)
                    return;
                GiveHardhatShovel();
                _hardhatShovelState = HardhatShovelState.AwaitRewardClose;
                return;

            case HardhatShovelState.AwaitRewardClose:
                if (_dialogue.IsOpen)
                    return;
                RemoveHardhatTreasure();
                _hardhatWaitTicks = 0.0;
                _hardhatShovelState = HardhatShovelState.PostRewardWait;
                return;

            case HardhatShovelState.PostRewardWait:
                _hardhatWaitTicks += delta * 60.0;
                if (_hardhatWaitTicks < _blackTower.TalkWait)
                    return;
                _dialogue.ShowGameplayMessage(
                    _blackTower.Text(0x1002),
                    _worldToScreen(_hardhatPlayer!.Position).Y);
                _hardhatShovelState = HardhatShovelState.AwaitFinalClose;
                return;

            case HardhatShovelState.AwaitFinalClose:
                if (!_dialogue.IsOpen)
                    FinishHardhatShovel();
                return;
        }
    }

    private void GiveHardhatShovel()
    {
        TreasureDatabase.TreasureObjectRecord shovel =
            _treasures.GetObject("TREASURE_OBJECT_SHOVEL_00");
        if (shovel.TreasureId != TreasureDatabase.TreasureShovel ||
            shovel.TextId != 0x25)
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_SHOVEL_00 no longer matches giveitem in the hardhat script.");
        }

        _inventory.GiveTreasure(shovel);
        _rooms.SaveData.SetRoomFlag(
            _rooms.ActiveGroup, _rooms.CurrentRoom.Id, OracleSaveData.RoomFlagItem);
        // giveTreasure's behavior sound precedes grab-mode $02's own sound.
        _playSound(OracleSoundEngine.SndGetItem);

        BlackTowerWorkerDatabase.VisualRecord visual = _blackTower.Visual("shovel");
        Vector2 position = _hardhatPlayer!.Position;
        var record = new GroundTreasureDatabase.Record(
            _rooms.ActiveGroup,
            _rooms.CurrentRoom.Id,
            0,
            Mathf.FloorToInt(position.Y),
            Mathf.FloorToInt(position.X),
            shovel.Name,
            visual.Sprite,
            visual.TileBase,
            visual.Palette,
            visual.Animation,
            0x0049,
            string.Empty,
            "hardhatWorkerSubid00Script:giveitem TREASURE_SHOVEL,$00");
        _hardhatTreasure = new GroundTreasurePickup
        {
            Name = "HardhatShovel",
            ZIndex = 12
        };
        _hardhatTreasure.Initialize(record, _playSound);
        _worldRoot.AddChild(_hardhatTreasure);
        _hardhatTreasure.BeginGranted(_hardhatPlayer);
        _dialogue.ShowGameplayMessage(
            shovel.Message, _worldToScreen(_hardhatPlayer.Position).Y);
    }

    private void RemoveHardhatTreasure()
    {
        if (_hardhatTreasure is null)
            return;
        _hardhatTreasure.Finish(_hardhatPlayer!);
        if (_hardhatTreasure.GetParent() == _worldRoot)
            _worldRoot.RemoveChild(_hardhatTreasure);
        _hardhatTreasure.QueueFree();
        _hardhatTreasure = null;
    }

    private void FinishHardhatShovel()
    {
        RemoveHardhatTreasure();
        if (_hardhatNpc is not null)
            _entities.EndNpcTalk(_hardhatNpc);
        _activeNpcTalkLifecycle = null;
        _hardhatNpc = null;
        _hardhatPlayer = null;
        _hardhatWaitTicks = 0.0;
        _hardhatShovelState = HardhatShovelState.None;
    }

    internal void ShowRoomInteractionMessage(string message, Player player) =>
        _dialogue.ShowGameplayMessage(message, _worldToScreen(player.Position).Y);

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
        _dialogue.ShowGameplayMessage(npc.Message, _familyLinkScreenY);
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
                    _dialogue.ShowGameplayMessage(invalid.Message, _familyLinkScreenY);
                    _familyNamingState = FamilyNamingState.AwaitInvalidClose;
                    return;
                }
                _pendingChildName = name;
                var confirmation = _familyInteractions.Text(
                    0x4407, _rooms.SaveData, _pendingChildName);
                _dialogue.ShowGameplayChoiceMessage(
                    confirmation.Message, _familyLinkScreenY);
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
                _dialogue.ShowGameplayMessage(thanks.Message, _familyLinkScreenY);
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
            _dialogue.ShowGameplayMessage(
                "It won't open\nfrom this side!", _worldToScreen(player.Position).Y); // TX_510d
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
        _playSound(OracleSoundEngine.SndOpenChest);
        _pendingChest = chest;
        _chestTreasure = new ChestTreasureEffect { ZIndex = 12 };
        _chestTreasure.Initialize(
            PointForPackedPosition(position) + new Vector2(0, -8),
            _treasures.GetObjectVisual(chest.Graphic));
        _worldRoot.AddChild(_chestTreasure);
        return true;
    }

    private void ApplyOpenedChestState(int group, OracleRoomData room)
    {
        if (!_rooms.SaveData.HasRoomFlag(group, room.Id, OracleSaveData.RoomFlagItem))
            return;

        foreach (ChestDatabase.ChestRecord chest in _chests.GetRoomRecords(group, room.Id))
        {
            // loadChestData places the opened chest at every imported chest
            // position when ROOMFLAG_ITEM is set. Trigger-created chests such
            // as room 4:08 start over a floor tile, so this cannot require a
            // closed $f1 tile in the source layout.
            room.SetPositionTileAndCollision(
                PointForPackedPosition(chest.Position), 0xf0, null, _animationTick());
        }
    }

    private void OnGroundTreasureCollected(
        GroundTreasurePickup treasure,
        Player player)
    {
        if (_groundTreasure is not null || _chestTreasure is not null)
            throw new InvalidOperationException(
                "A ground treasure was collected while another reward was active.");

        TreasureDatabase.TreasureObjectRecord treasureObject =
            _treasures.GetObject(treasure.Record.TreasureObject);
        _inventory.GiveTreasure(treasureObject);
        _groundTreasureCompletesHeartContainer =
            treasureObject.TreasureId == 0x2b && _inventory.HeartPieces == 4;
        _groundTreasureShowingHeartContainer = false;
        _rooms.SaveData.SetRoomFlag(
            treasure.Record.Group,
            treasure.Record.Room,
            OracleSaveData.RoomFlagItem);
        _playSound(OracleSoundEngine.SndGetItem);
        _groundTreasure = treasure;
        _groundTreasurePlayer = player;
        if (!string.IsNullOrEmpty(treasureObject.Message))
        {
            _dialogue.ShowGameplayMessage(
                treasureObject.Message,
                _worldToScreen(player.Position).Y);
        }
    }

    private void OnRoomChanged(int group, OracleRoomData room)
    {
        if (_hardhatShovelState != HardhatShovelState.None)
            FinishHardhatShovel();
        if (_groundTreasure is not null && _groundTreasurePlayer is not null)
            _groundTreasure.Finish(_groundTreasurePlayer);
        _groundTreasure = null;
        _groundTreasurePlayer = null;
        _groundTreasureCompletesHeartContainer = false;
        _groundTreasureShowingHeartContainer = false;
        ApplyOpenedChestState(group, room);
    }

    private static Vector2 PointForPackedPosition(int position) => new(
        (position & 0x0f) * OracleRoomData.MetatileSize + 8,
        (position >> 4) * OracleRoomData.MetatileSize + 8);

    private static int MakeChestKey(int group, int room, int position) =>
        (group << 16) | (room << 8) | position;
}
