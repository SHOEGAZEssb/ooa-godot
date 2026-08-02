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
    private readonly TreasureDatabase _treasures;
    private readonly TileInteractionFallbackDatabase _tileFallbacks;
    private readonly DialogueBox _dialogue;
    private readonly Node _worldRoot;
    private readonly RoomView _roomView;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly InventoryState _inventory;
    private readonly Action<int> _playSound;
    private readonly Func<bool> _gashaCountersCaughtUp;
    private readonly BipinBlossomFamilyStateResolver _familyState;
    private readonly NpcInteractionScriptController _npcScripts;
    private readonly NpcInteractionRouter _npcInteractionRouter;
    private readonly KidNameEntryController _kidNameEntry;
    private readonly Dictionary<int, ChestRecord> _debugChestOverrides = new();
    private ChestTreasureEffect? _chestTreasure;
    private GroundTreasurePickup? _groundTreasure;
    private Player? _groundTreasurePlayer;
    private bool _groundTreasureCompletesHeartContainer;
    private bool _groundTreasureShowingHeartContainer;
    private ChestRecord _pendingChest;
    private FamilyNamingState _familyNamingState;
    private string _pendingChildName = string.Empty;
    private float _familyLinkScreenY;
    private double _familyWaitTicks;
    private NpcInteractionTarget? _activeNpcTalkTarget;
    private GashaState _gashaState;
    private GashaSpotInteraction? _gashaSpot;
    private Player? _gashaPlayer;
    private bool _gashaCompletesHeartContainer;
    private bool _gashaShowingHeartContainer;

    public bool DialogueOpen => _dialogue.BlocksPlayerInput ||
        _chestTreasure is not null ||
        _groundTreasure is not null ||
        _gashaState != GashaState.None ||
        _npcScripts.BlocksGameplay ||
        _familyNamingState != FamilyNamingState.None ||
        _kidNameEntry.Active;
    public bool GameplayMenuActive => _kidNameEntry.Active;
    internal bool ChestRewardActive => _chestTreasure is not null;
    internal ChestTreasureEffect? ChestReward => _chestTreasure;
    internal GroundTreasurePickup? GroundTreasureForValidation => _groundTreasure;
    internal GroundTreasurePickup? PastBipinTreasureForValidation =>
        _npcScripts.PastBipinTreasure;
    internal GroundTreasurePickup? PostmanTreasureForValidation =>
        _npcScripts.PostmanTreasure;
    internal NpcInteractionScriptController NpcScriptsForValidation =>
        _npcScripts;

    internal InteractionController(
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
        Action<int>? playSound = null,
        Func<bool>? gashaCountersCaughtUp = null,
        IReadOnlyList<NpcInteractionHandler>? roomInteractionHandlers = null)
    {
        _rooms = rooms;
        _entities = entities;
        _familyState = entities.FamilyStateResolver;
        _signs = signs;
        _chests = chests;
        _treasures = treasures;
        _tileFallbacks = new TileInteractionFallbackDatabase(treasures);
        _dialogue = dialogue;
        _worldRoot = worldRoot;
        _roomView = roomView;
        _worldToScreen = worldToScreen;
        _animationTick = animationTick;
        _inventory = inventory;
        _playSound = playSound ?? (static _ => { });
        _gashaCountersCaughtUp = gashaCountersCaughtUp ?? (static () => true);
        _kidNameEntry = new KidNameEntryController(interfaceLayer, playSound);
        _npcScripts = new NpcInteractionScriptController(
            rooms,
            entities,
            dialogue,
            treasures,
            _familyState,
            inventory);
        var interactionHandlers = new List<NpcInteractionHandler>
        {
            NpcInteractionHandler.ForNpc(
                "blossom.s:MENU_KIDNAME",
                (target, player) =>
                    TryStartFamilyNaming(target.Npc, player))
        };
        if (roomInteractionHandlers is not null)
        {
            foreach (NpcInteractionHandler handler in roomInteractionHandlers)
            {
                if (handler.TargetKind == NpcInteractionTargetKind.Npc)
                    interactionHandlers.Add(handler);
            }
        }
        foreach (NpcInteractionHandler handler in _npcScripts.Handlers)
            interactionHandlers.Add(handler);
        interactionHandlers.Add(NpcInteractionHandler.ForNpc(
            "linkInteractWithAButtonSensitiveObjects:ordinaryNpcDialogue",
            TryStartOrdinaryNpcInteraction));
        if (roomInteractionHandlers is not null)
        {
            foreach (NpcInteractionHandler handler in roomInteractionHandlers)
            {
                if (handler.TargetKind == NpcInteractionTargetKind.Player)
                    interactionHandlers.Add(handler);
            }
        }
        _npcInteractionRouter = new NpcInteractionRouter(
            interactionHandlers);
        _dialogue.SetHeartPieceCountProvider(() => _inventory.HeartPieces);
        _dialogue.HeartPieceSetFilled += OnHeartPieceSetFilled;
        _dialogue.HeartPieceSetAccepted += OnHeartPieceSetAccepted;
        _rooms.RoomChanged += OnRoomChanged;
        _entities.GroundTreasureCollected += OnGroundTreasureCollected;
        _entities.GroundTreasureDialogueRequested +=
            OnGroundTreasureDialogueRequested;
        _entities.GashaInteractionRequested += OnGashaInteractionRequested;
        _entities.GashaNutCaught += OnGashaNutCaught;
        _entities.MapleDialogueRequested += OnMapleDialogueRequested;
        _entities.RoomEntityDialogueRequested +=
            OnRoomEntityDialogueRequested;
        _entities.MapleItemCollected += OnMapleItemCollected;
        _entities.SeedTreeMessageRequested += OnSeedTreeMessageRequested;
        _entities.OwlStatueMessageRequested += OnOwlStatueMessageRequested;
        _entities.GroundTreasureCollectionAllowed = () => !DialogueOpen;
        // wTextIsActive is narrower than this controller's aggregate
        // DialogueOpen gameplay lease: held rewards and naming/menu states do
        // not select the original reduced object dispatcher by themselves.
        _entities.TextActiveSource = () => _dialogue.IsOpen;
        ApplyOpenedChestState(_rooms.ActiveGroup, _rooms.CurrentRoom);
    }

    public void Update(double delta, Player player)
    {
        if (_activeNpcTalkTarget is not null && !_dialogue.IsOpen)
        {
            _activeNpcTalkTarget.End();
            _activeNpcTalkTarget = null;
        }
        _kidNameEntry.Update();
        UpdateFamilyNaming(delta);
        _npcScripts.Update(delta);
        UpdateGasha();

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
            TreasureObjectRecord treasureObject = new TreasureObjectRecord(
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
        if (_gashaSpot is not null && _gashaCompletesHeartContainer &&
            !_gashaShowingHeartContainer)
        {
            _inventory.ResetCompletedHeartPieceSet();
            return;
        }
        if (_groundTreasure is null || !_groundTreasureCompletesHeartContainer ||
            _groundTreasureShowingHeartContainer)
            return;
        _inventory.ResetCompletedHeartPieceSet();
    }

    private void OnSeedTreeMessageRequested(
        int textId,
        string message,
        Vector2 position)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _dialogue.ShowGameplayMessage(
                message, _worldToScreen(position).Y);
        }
    }

    private void OnOwlStatueMessageRequested(
        int textId,
        string message,
        Vector2 position)
    {
        _ = textId;
        if (!string.IsNullOrEmpty(message))
        {
            _dialogue.ShowGameplayMessage(
                message, _worldToScreen(position).Y);
        }
    }

    private void OnHeartPieceSetAccepted()
    {
        if (_gashaSpot is not null && _gashaCompletesHeartContainer &&
            !_gashaShowingHeartContainer)
        {
            _inventory.GiveCompletedHeartContainer(
                _treasures.GetObject("TREASURE_OBJECT_HEART_CONTAINER_00"));
            _playSound(OracleSoundEngine.SndFilledHeartContainer);
            _dialogue.ShowGameplayMessage(
                _gashaSpot.Database.Text(0x0049),
                _worldToScreen(_gashaPlayer!.Position).Y);
            _gashaShowingHeartContainer = true;
            return;
        }
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
        NpcInteractionTarget? target =
            _entities.FindNpcInteractionTarget(player);
        if (_npcInteractionRouter.TryBegin(target, player))
            return true;

        if (_entities.TryInteract(player))
            return true;

        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 tilePoint = player.Position + (Vector2)player.FacingVector * 8.0f;
        byte tile = room.GetMetatile(tilePoint);
        if (tile == 0xf1)
            return TryOpenChest(player, tilePoint);
        if (tile != 0xf2)
            return false;

        string message;
        if (player.FacingVector != Vector2I.Up)
            message = _tileFallbacks.SignWrongSide.Message;
        else if (!_signs.TryGetMessage(
            _rooms.ActiveGroup, room.Id, room.GetPackedPosition(tilePoint), out message!))
            message = _tileFallbacks.SignNoMatch.Message;

        _dialogue.ShowGameplayMessage(message, _worldToScreen(player.Position).Y);
        return true;
    }

    public bool TrySecondaryInteract(Player player) =>
        _npcInteractionRouter.TryBegin(target: null, player);

    private bool TryStartOrdinaryNpcInteraction(
        NpcInteractionTarget target,
        Player player)
    {
        NpcCharacter npc = target.Npc;
        npc.FaceToward(player.Position);
        target.Begin();
        _activeNpcTalkTarget = target;
        _dialogue.ShowGameplayMessage(
            npc.Message,
            _worldToScreen(player.Position).Y,
            npc.TextPosition);
        return true;
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
                    Dialogue invalid = _familyState.Text(0x440a, _rooms.SaveData);
                    _dialogue.ShowGameplayMessage(invalid.Message, _familyLinkScreenY);
                    _familyNamingState = FamilyNamingState.AwaitInvalidClose;
                    return;
                }
                _pendingChildName = name;
                Dialogue confirmation = _familyState.Text(
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
                Dialogue thanks = _familyState.Text(0x4408, _rooms.SaveData);
                _dialogue.ShowGameplayMessage(thanks.Message, _familyLinkScreenY);
                _familyNamingState = FamilyNamingState.AwaitThanksClose;
                return;

            case FamilyNamingState.AwaitThanksClose:
                if (!_dialogue.IsOpen)
                    _familyNamingState = FamilyNamingState.None;
                return;
        }
    }

    internal bool FamilyNamingActive =>
        _familyNamingState != FamilyNamingState.None || _kidNameEntry.Active;
    internal IReadOnlyList<string> NpcInteractionHandlerSources =>
        _npcInteractionRouter.Sources;
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
            TreasureObjectRecord treasure = _treasures.GetObject(treasureObjectName);
            _debugChestOverrides[MakeChestKey(group, roomId, position)] = new ChestRecord(
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
                _tileFallbacks.ChestWrongSide.Message,
                _worldToScreen(player.Position).Y);
            return true;
        }

        OracleRoomData room = _rooms.CurrentRoom;
        int position = room.GetPackedPosition(tilePoint);
        if (!_debugChestOverrides.TryGetValue(MakeChestKey(_rooms.ActiveGroup, room.Id, position),
                out ChestRecord chest) &&
            !_chests.TryGet(_rooms.ActiveGroup, room.Id, position, out chest))
        {
            chest = _tileFallbacks.ChestNoMatch.At(
                _rooms.ActiveGroup,
                room.Id,
                position);
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

        foreach (ChestRecord chest in _chests.GetRoomRecords(group, room.Id))
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
        if (treasure.Record.CompletionOwner ==
            GroundTreasureCompletionOwner.Caller)
        {
            return;
        }
        if (_groundTreasure is not null || _chestTreasure is not null)
            throw new InvalidOperationException(
                "A ground treasure was collected while another reward was active.");

        TreasureObjectRecord treasureObject =
            _treasures.GetObject(treasure.Record.TreasureObject);
        _groundTreasureCompletesHeartContainer =
            treasureObject.TreasureId == 0x2b && _inventory.HeartPieces == 4;
        _groundTreasureShowingHeartContainer = false;
        _groundTreasure = treasure;
        _groundTreasurePlayer = player;
    }

    private void OnGroundTreasureDialogueRequested(
        GroundTreasurePickup treasure,
        TreasureObjectRecord treasureObject,
        Player player)
    {
        if (treasure.Record.TextboxFlags != 0)
        {
            _dialogue.ShowGameplayMessageWithFlags(
                treasureObject.Message,
                _worldToScreen(player.Position).Y,
                treasure.Record.TextboxFlags,
                treasure.Record.TextboxPosition);
            return;
        }
        if (treasure.Record.TextboxPosition.HasValue)
        {
            _dialogue.ShowGameplayMessage(
                treasureObject.Message,
                _worldToScreen(player.Position).Y,
                treasure.Record.TextboxPosition.Value);
            return;
        }
        _dialogue.ShowGameplayMessage(
            treasureObject.Message,
            _worldToScreen(player.Position).Y);
    }

    private void OnRoomChanged(int group, OracleRoomData room)
    {
        _activeNpcTalkTarget?.Cancel();
        _activeNpcTalkTarget = null;
        _kidNameEntry.Cancel();
        _familyNamingState = FamilyNamingState.None;
        _pendingChildName = string.Empty;
        _familyWaitTicks = 0.0;
        if (_groundTreasure is not null && _groundTreasurePlayer is not null)
            _groundTreasure.Finish(_groundTreasurePlayer);
        _groundTreasure = null;
        _groundTreasurePlayer = null;
        _groundTreasureCompletesHeartContainer = false;
        _groundTreasureShowingHeartContainer = false;
        ResetGashaInteraction();
        ApplyOpenedChestState(group, room);
    }

    private void OnMapleDialogueRequested(
        int textId,
        string message,
        Player player)
    {
        _ = textId;
        _dialogue.ShowGameplayMessage(
            message, _worldToScreen(player.Position).Y);
    }

    private void OnRoomEntityDialogueRequested(
        int textId,
        string message,
        Vector2 position)
    {
        _ = textId;
        _dialogue.ShowGameplayMessage(
            message, _worldToScreen(position).Y);
    }

    private void OnMapleItemCollected(
        MapleItemRecord item,
        Player player)
    {
        if (item.Index == 0)
        {
            BeginMapleHeartPiece(item, player);
            return;
        }

        int parameter =
            _inventory.IsRingActive(RingId.GoldJoy) ||
            item.BoostRing >= 0 &&
                _inventory.ActiveRing == item.BoostRing
                ? item.BoostedParameter
                : item.NormalParameter;
        if (item.Treasure == TreasureDatabase.TreasureRing)
        {
            int ring = new GashaSpotDatabase().SelectRing(
                parameter, _entities.NextRandomValue());
            _inventory.GiveUnappraisedRing(ring);
        }
        else
        {
            _inventory.GiveTreasure(item.Treasure, parameter);
        }

        int collectionSound = item.Treasure == TreasureDatabase.TreasurePotion
            ? OracleSoundEngine.SndGetSeed
            : _treasures.GetBehaviour(item.Treasure).Sound;
        if (collectionSound != 0)
            _playSound(collectionSound);
    }

    private void BeginMapleHeartPiece(
        MapleItemRecord item,
        Player player)
    {
        if (_groundTreasure is not null || _chestTreasure is not null)
        {
            throw new InvalidOperationException(
                "Maple's heart piece was collected while another reward was active.");
        }

        TreasureObjectRecord treasureObject =
            _treasures.GetObject("TREASURE_OBJECT_HEART_PIECE_02");
        TreasureObjectVisualRecord visual =
            _treasures.GetObjectVisual(treasureObject.Graphic);
        var record = new GroundTreasureDatabaseRecord(
            _rooms.ActiveGroup,
            _rooms.CurrentRoom.Id,
            item.Index,
            Mathf.FloorToInt(player.Position.Y),
            Mathf.FloorToInt(player.Position.X),
            treasureObject.Name,
            visual.Sprite,
            visual.TileBase,
            visual.Palette,
            visual.Animation,
            treasureObject.TextId,
            treasureObject.Message,
            "itemFromMaple.s:@func_4e6e");
        GroundTreasurePickup pickup =
            _entities.Spawn<GroundTreasurePickup>(
                new GroundTreasureSpawn(record));

        _inventory.GiveTreasure(treasureObject);
        _rooms.SaveData.SetMapleState(
            _rooms.SaveData.MapleState | 0x80);
        _groundTreasureCompletesHeartContainer =
            _inventory.HeartPieces == 4;
        _groundTreasureShowingHeartContainer = false;
        _groundTreasure = pickup;
        _groundTreasurePlayer = player;
        pickup.BeginGranted(player);
        int collectionSound =
            _treasures.GetBehaviour(treasureObject.TreasureId).Sound;
        if (collectionSound != 0)
            _playSound(collectionSound);
        _dialogue.ShowGameplayMessage(
            treasureObject.Message,
            _worldToScreen(player.Position).Y);
    }

    private void OnGashaInteractionRequested(
        GashaSpotInteraction interaction,
        Player player)
    {
        if (_gashaState != GashaState.None)
            return;
        _gashaSpot = interaction;
        _gashaPlayer = player;
        if (_inventory.GashaSeeds == 0)
        {
            _gashaState = GashaState.AwaitNoSeedsClose;
            _dialogue.ShowGameplayMessage(
                interaction.Database.Text(0x3509),
                _worldToScreen(player.Position).Y);
            return;
        }
        _gashaState = GashaState.AwaitPlantChoice;
        _dialogue.ShowGameplayChoiceMessage(
            interaction.Database.Text(0x3500),
            _worldToScreen(player.Position).Y);
    }

    private void OnGashaNutCaught(
        GashaSpotInteraction interaction,
        Player player)
    {
        if (_gashaState != GashaState.None)
            return;
        _gashaSpot = interaction;
        _gashaPlayer = player;
        _gashaState = GashaState.AwaitNutIntroClose;
        _dialogue.ShowGameplayMessage(
            interaction.Database.Text(0x3501),
            _worldToScreen(player.Position).Y);
    }

    private void UpdateGasha()
    {
        if (_gashaState == GashaState.None || _gashaSpot is null ||
            _gashaPlayer is null)
        {
            return;
        }

        switch (_gashaState)
        {
            case GashaState.AwaitNoSeedsClose:
                if (!_dialogue.IsOpen)
                    ResetGashaInteraction();
                return;

            case GashaState.AwaitPlantChoice:
                if (!_dialogue.TryTakeChoiceResult(out int choice))
                    return;
                if (choice == 0)
                    _gashaSpot.Plant();
                ResetGashaInteraction();
                return;

            case GashaState.AwaitNutIntroClose:
                if (_dialogue.IsOpen)
                    return;
                GiveGashaReward();
                return;

            case GashaState.AwaitRewardClose:
                if (_dialogue.IsOpen)
                    return;
                _gashaState = GashaState.AwaitDisplayedCounters;
                return;

            case GashaState.AwaitDisplayedCounters:
                if (!_gashaCountersCaughtUp())
                    return;
                _gashaSpot.BeginDisappearance();
                _gashaState = GashaState.AwaitDisappearance;
                return;

            case GashaState.AwaitDisappearance:
                if (_gashaSpot.Finished)
                    ResetGashaInteraction();
                return;
        }
    }

    private void GiveGashaReward()
    {
        GashaSpotDatabase database = _gashaSpot!.Database;
        Result result = GashaRewardResolver.Give(
            database, _gashaSpot.Spot, _gashaSpot.Save, _inventory,
            _entities.NextRandomValue);
        RewardRecord reward = result.Reward;
        _gashaCompletesHeartContainer = result.CompletesHeartContainer;
        _gashaShowingHeartContainer = false;
        _gashaSpot.BeginReward(result.RewardType, reward, _gashaPlayer!);
        _dialogue.ShowGameplayMessage(
            database.Text(reward.TextId),
            _worldToScreen(_gashaPlayer!.Position).Y);
        _gashaState = GashaState.AwaitRewardClose;
    }

    private void ResetGashaInteraction()
    {
        _gashaState = GashaState.None;
        _gashaSpot = null;
        _gashaPlayer = null;
        _gashaCompletesHeartContainer = false;
        _gashaShowingHeartContainer = false;
    }

    private static Vector2 PointForPackedPosition(int position) => new(
        (position & 0x0f) * OracleRoomData.MetatileSize + 8,
        (position >> 4) * OracleRoomData.MetatileSize + 8);

    private static int MakeChestKey(int group, int room, int position) =>
        (group << 16) | (room << 8) | position;
}

internal enum GashaState
{
    None,
    AwaitNoSeedsClose,
    AwaitPlantChoice,
    AwaitNutIntroClose,
    AwaitRewardClose,
    AwaitDisplayedCounters,
    AwaitDisappearance
}

internal enum FamilyNamingState
{
    None,
    AwaitOpeningClose,
    NameEntry,
    AwaitConfirmation,
    AwaitInvalidClose,
    ThanksDelay,
    AwaitThanksClose
}
