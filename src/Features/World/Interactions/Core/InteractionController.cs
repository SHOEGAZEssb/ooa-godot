
namespace Oracles;

public sealed class InteractionController : IDisposable
{
    private string AssetRoot => _rooms.AssetRoot;


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
    private readonly INpcInteractionScripts? _npcScripts;
    private readonly Func<bool> _priorGroundCollection, _priorTextActive;
    private readonly Func<bool> _groundCollection, _textActive;
    private bool _disposed;
    private readonly NpcInteractionRouter _npcInteractionRouter;
    private readonly Dictionary<int, ChestRecord> _debugChestOverrides = new();
    private ChestTreasureEffect? _chestTreasure;
    private double _chestSetupTicks;
    private ChestRecord _pendingChest;
    private NpcInteractionTarget? _activeNpcTalkTarget;
    private bool _activeNpcDialoguePending;
    private GashaState _gashaState;
    private GashaSpotInteraction? _gashaSpot;
    private Player? _gashaPlayer;
    private bool _gashaCompletesHeartContainer;
    private bool _gashaShowingHeartContainer;

    public bool DialogueOpen => !_disposed && (_dialogue.BlocksPlayerInput ||
        _chestTreasure is not null ||
        GroundTreasures.Active is not null ||
        _gashaState != GashaState.None ||
        _npcScripts?.BlocksGameplay == true);
    internal bool ChestRewardActive => _chestTreasure is not null;
    internal ChestTreasureEffect? ChestReward => _chestTreasure;
    internal GroundTreasureController GroundTreasures { get; }

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
        INpcInteractionScripts? npcScripts,
        GroundTreasureController groundTreasures,
        Action<int>? playSound = null,
        Func<bool>? gashaCountersCaughtUp = null,
        IReadOnlyList<NpcInteractionHandler>? roomInteractionHandlers = null)
    {
        _rooms = rooms;
        _entities = entities;
        _signs = signs;
        _chests = chests;
        _treasures = treasures;
        _tileFallbacks = new TileInteractionFallbackDatabase(AssetRoot, treasures);
        _dialogue = dialogue;
        _worldRoot = worldRoot;
        _roomView = roomView;
        _worldToScreen = worldToScreen;
        _animationTick = animationTick;
        _inventory = inventory;
        GroundTreasures = groundTreasures;
        _playSound = playSound ?? (static _ => { });
        _gashaCountersCaughtUp = gashaCountersCaughtUp ?? (static () => true);
        _npcScripts = npcScripts;
        var interactionHandlers = new List<NpcInteractionHandler>();
        if (roomInteractionHandlers is not null)
        {
            foreach (NpcInteractionHandler handler in roomInteractionHandlers)
            {
                if (handler.TargetKind == NpcInteractionTargetKind.Npc)
                    interactionHandlers.Add(handler);
            }
        }
        if (_npcScripts is not null)
            foreach (NpcInteractionHandler handler in _npcScripts.Handlers)
                interactionHandlers.Add(handler);
        else interactionHandlers.Add(NpcInteractionHandler.ForEntityScripts(_entities));
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
        _dialogue.HeartPieceSetFilled += OnHeartPieceSetFilled;
        _dialogue.HeartPieceSetAccepted += OnHeartPieceSetAccepted;
        _rooms.RoomChanged += OnRoomChanged;
        _entities.GroundTreasureCollected += OnGroundTreasureCollected;
        _entities.GashaInteractionRequested += OnGashaInteractionRequested;
        _entities.GashaNutCaught += OnGashaNutCaught;
        _entities.MapleDialogueRequested += OnMapleDialogueRequested;
        _entities.RoomEntityDialogueRequested +=
            OnRoomEntityDialogueRequested;
        _entities.MapleItemCollected += OnMapleItemCollected;
        _entities.SeedTreeMessageRequested += OnSeedTreeMessageRequested;
        _entities.OwlStatueMessageRequested += OnOwlStatueMessageRequested;
        _priorGroundCollection = _entities.GroundTreasureCollectionAllowed;
        _groundCollection = () => !DialogueOpen;
        _entities.GroundTreasureCollectionAllowed = _groundCollection;
        // wTextIsActive is narrower than this controller's aggregate
        // DialogueOpen gameplay lease: held rewards and naming/menu states do
        // not select the original reduced object dispatcher by themselves.
        _priorTextActive = _entities.TextActiveSource;
        _textActive = () => _dialogue.IsOpen ||
            _entities.RuntimeState.ReadField("wTextIsActive") != 0;
        _entities.TextActiveSource = _textActive;
    }

    public void Update(double delta, Player player)
    {
        if (_disposed) return;
        if (_activeNpcTalkTarget is not null && !_activeNpcDialoguePending && !_dialogue.IsOpen)
        {
            _activeNpcTalkTarget.End();
            _activeNpcTalkTarget = null;
        }
        _npcScripts?.Update(delta);
        UpdateGasha();

        if (GroundTreasures.Active is not null) return;

        if (_chestTreasure is null)
            return;

        // treasure.s spawnMode3 writes this on every visit, including text
        // and deletion; state0's record-loading visit does not enter that mode.
        if (_chestSetupTicks < 2) player.SetNativePushAnimationOverride(0x80);
        if (!_chestTreasure.Finished)
        {
            // INTERAC$60 state0 loads the record on the opening update;
            // spawnMode3 substate0 arms counter32 on the next update. Neither
            // dispatch moves the treasure or decrements its rise counter.
            if (_chestSetupTicks > 0)
            {
                double setup = Math.Min(_chestSetupTicks, delta * 60.0);
                _chestSetupTicks -= setup;
                delta -= setup / 60.0;
                if (_chestSetupTicks > 0) return;
                _chestTreasure.Visible = true;
                _entities.RuntimeState.SetField("wDisableLinkCollisionsAndMenu",1);
                if (delta <= 0) return;
            }
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
            if (collectionSound != SoundId.MusNone)
                _playSound(collectionSound);
            if (!string.IsNullOrEmpty(_pendingChest.Message))
                _dialogue.ShowGameplayMessage(
                    _pendingChest.Message, _worldToScreen(player.Position).Y);
            _playSound(SoundId.SndGetItem);
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
        _entities.RuntimeState.SetField("wDisabledObjects",0);
        _entities.RuntimeState.SetField("wDisableLinkCollisionsAndMenu",0);
    }

    private void OnHeartPieceSetFilled()
    {
        if (_gashaSpot is not null && _gashaCompletesHeartContainer &&
            !_gashaShowingHeartContainer)
        {
            _inventory.ResetCompletedHeartPieceSet();
            return;
        }
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
            _playSound(SoundId.SndFilledHeartContainer);
            _dialogue.ShowGameplayMessage(
                _gashaSpot.Database.Text(0x0049),
                _worldToScreen(_gashaPlayer!.Position).Y);
            _gashaShowingHeartContainer = true;
            return;
        }
    }

    public bool TryInteract(Player player)
    {
        if (_disposed) return false;
        NpcInteractionTarget? target =
            _entities.FindNpcInteractionTarget(player);
        // Native A-sensitive selection grants invincibility before the
        // selected object's later script runs, including shopkeepers.
        if (target is not null)
        {
            player.ApplyObjectInteractionGrace();
            if (_entities.TryInteractNativeSpecialObject(target.Npc, player)) return true;
        }
        if (_npcInteractionRouter.TryBegin(target, player))
            return true;

        if (_entities.TryInteract(player))
            return true;

        // Tile dispatch rejects wLinkGrabState even in shops, where the
        // earlier A-sensitive object selection is allowed to accept held stock.
        if (player.GrabStateActive) return false;
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 tilePoint = player.Position + (Vector2)player.FacingVector * 8.0f;
        byte tile = room.GetMetatile(tilePoint);
        if (tile is 0xf1 or 0xf2 && !player.FacesTileWallForInteraction)
            return false;
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
        !_disposed && _npcInteractionRouter.TryBegin(target: null, player);

    private bool TryStartOrdinaryNpcInteraction(
        NpcInteractionTarget target,
        Player player)
    {
        NpcCharacter npc = target.Npc;
        target.Begin();
        _activeNpcTalkTarget = target;
        _activeNpcDialoguePending = true;
        void ShowDialogue()
        {
            _activeNpcDialoguePending = false;
            npc.FaceToward(player.Position);
            _dialogue.ShowGameplayMessage(npc.Message, _worldToScreen(player.Position).Y, npc.TextPosition);
        }
        bool TryShowDialogue()
        {
            // interactionRunScript samples live text/death state even when
            // the interaction pass was admitted before an earlier slot opened
            // text. Retain pressed A until this actor's script can consume it.
            if (_entities.IsScriptTextActive() || player.IsDying)
                return false;
            ShowDialogue();
            return true;
        }
        if (!_entities.TryQueueOrdinaryNpcDialogue(npc, TryShowDialogue))
            ShowDialogue();
        return true;
    }

    internal void ShowRoomInteractionMessage(string message, Player player) =>
        _dialogue.ShowGameplayMessage(message, _worldToScreen(player.Position).Y);

    internal IReadOnlyList<string> NpcInteractionHandlerSources =>
        _npcInteractionRouter.Sources;
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
        OracleRoomData room = _rooms.ActiveGroup == group && _rooms.CurrentRoom.Id == roomId
            ? _rooms.CurrentRoom : _rooms.GetRoom(group, roomId);
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
        var runtime = _entities.RuntimeState;
        bool inShop = runtime.ReadField("wInShop") != 0;
        int scriptedChest = runtime.ReadField("wcca1");
        if (inShop && scriptedChest == 0 && runtime.ReadField("wcca2") != 0)
            return true;
        if (!room.ReplaceMetatile(tilePoint, ChestDatabase.ClosedTile, ChestDatabase.OpenedTile, _animationTick()))
            return true;
        runtime.SetField("wcca2", (byte)position);
        _roomView.QueueRedraw();
        _playSound(SoundId.SndOpenChest);
        // Shop/minigame and script owners consume the opened tile and position;
        // nextToChestTile returns before reading contents or granting a reward.
        if (inShop || scriptedChest != 0) return true;
        if (!_debugChestOverrides.TryGetValue(MakeChestKey(_rooms.ActiveGroup, room.Id, position),
                out ChestRecord chest) &&
            !_chests.TryGetRoomChest(_rooms.ActiveGroup, room.Id, out chest))
        {
            chest = _tileFallbacks.ChestNoMatch.At(
                _rooms.ActiveGroup,
                room.Id,
                position);
        }
        // nextToChestTile reads the shared override before getChestData.
        // Its first byte is the treasure ID, rather than a little-endian word.
        int overrideId = runtime.ReadField("wChestContentsOverride");
        if (overrideId != 0)
        {
            var reward = _treasures.GetObject(overrideId, runtime.ReadField("wChestContentsOverride", 1));
            chest = chest with { TreasureObject = reward.Name, TreasureId = reward.TreasureId,
                SubId = reward.SubId, Parameter = reward.Parameter, TextId = reward.TextId,
                Graphic = reward.Graphic, Message = reward.Message,
                Amount = reward.TreasureId == TreasureId.Rupees ? _treasures.RupeeValue(reward.Parameter) : 0 };
        }
        _rooms.SaveData.SetRoomFlag(
            _rooms.ActiveGroup, room.Id, OracleSaveData.RoomFlagItem);
        runtime.SetField("wChestContentsOverride", 0);
        runtime.SetField("wChestContentsOverride", 0, 1);
        // Native NONE contents still open the tile/set ROOMFLAG_ITEM, but do
        // not create an INTERAC_TREASURE or acquire the chest input masks.
        if (chest.TreasureId == TreasureId.None) return true;
        _pendingChest = chest;
        _entities.RuntimeState.SetField("wDisabledObjects",0x83);
        _entities.RuntimeState.SetField("wDisableLinkCollisionsAndMenu",0x83);
        _chestSetupTicks = 2;
        _chestTreasure = new ChestTreasureEffect { Visible = false, ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex };
        _chestTreasure.Initialize(
            PointForPackedPosition(position) + new Vector2(0, -8),
            _treasures.GetObjectVisual(chest.Graphic));
        _worldRoot.AddChild(_chestTreasure);
        return true;
    }

    private void OnGroundTreasureCollected(
        GroundTreasurePickup treasure,
        Player player)
    {
        if (treasure.Record.GrabMode == TreasureGrabMode.NoChange || treasure.Record.CompletionOwner ==
            GroundTreasureCompletionOwner.Caller)
        {
            return;
        }
        if (_chestTreasure is not null)
            throw new InvalidOperationException(
                "A ground treasure was collected while another reward was active.");

    }

    private void OnRoomChanged(int group, OracleRoomData room)
    {
        _activeNpcTalkTarget?.Cancel();
        _activeNpcTalkTarget = null;
        _activeNpcDialoguePending = false;
        ResetGashaInteraction();
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
        _dialogue.ShowGameplayMessage(
            message, _worldToScreen(position).Y, sourceTextId: textId);
    }

    private void OnMapleItemCollected(
        MapleItemRecord item,
        Player player)
    {
        int parameter =
            _inventory.IsRingActive(RingId.GoldJoy) ||
            item.BoostRing >= 0 &&
                _inventory.ActiveRing == item.BoostRing
                ? item.BoostedParameter
                : item.NormalParameter;
        if (item.Treasure == TreasureId.Ring)
        {
            int ring = new GashaSpotDatabase(AssetRoot).SelectRing(
                parameter, _entities.NextRandomValue());
            _inventory.GiveUnappraisedRing(ring);
        }
        else
        {
            _inventory.GiveTreasure(item.Treasure, parameter);
        }

        int collectionSound = item.Treasure == TreasureId.Potion
            ? SoundId.SndGetSeed
            : _treasures.GetBehaviour(item.Treasure).Sound;
        if (collectionSound != SoundId.MusNone)
            _playSound(collectionSound);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dialogue.HeartPieceSetFilled -= OnHeartPieceSetFilled;
        _dialogue.HeartPieceSetAccepted -= OnHeartPieceSetAccepted;
        _rooms.RoomChanged -= OnRoomChanged;
        _entities.GroundTreasureCollected -= OnGroundTreasureCollected;
        _entities.GashaInteractionRequested -= OnGashaInteractionRequested;
        _entities.GashaNutCaught -= OnGashaNutCaught;
        _entities.MapleDialogueRequested -= OnMapleDialogueRequested;
        _entities.RoomEntityDialogueRequested -= OnRoomEntityDialogueRequested;
        _entities.MapleItemCollected -= OnMapleItemCollected;
        _entities.SeedTreeMessageRequested -= OnSeedTreeMessageRequested;
        _entities.OwlStatueMessageRequested -= OnOwlStatueMessageRequested;
        if (ReferenceEquals(_entities.GroundTreasureCollectionAllowed,_groundCollection))
            _entities.GroundTreasureCollectionAllowed = _priorGroundCollection;
        if (ReferenceEquals(_entities.TextActiveSource,_textActive)) _entities.TextActiveSource = _priorTextActive;
        // Retirement discards presentation without executing gameplay completion
        // or cancellation writes against state retained by a replacement session.
        _activeNpcTalkTarget = null; _activeNpcDialoguePending = false;
        ResetGashaInteraction();
        if (GodotObject.IsInstanceValid(_chestTreasure)) _chestTreasure!.Free();
        _chestTreasure = null;
    }
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
