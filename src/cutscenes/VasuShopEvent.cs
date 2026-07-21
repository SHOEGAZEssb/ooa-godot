using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Native interaction controller for the five actors in Vasu Jewelers. The
/// ordinary tutorials, rewards, exact linked-script predicate, appraisal/list
/// menus, and no-cable serial timeout are supported. The linked-secret menu
/// remains an explicit Game Link subsystem boundary.
/// </summary>
internal sealed class VasuShopEvent : IRoomEvent
{
    internal enum EventStage
    {
        Inactive,
        VasuFirstExplanation,
        VasuRingBoxOffer,
        VasuRingBoxReward,
        VasuFriendshipOffer,
        VasuFriendshipReward,
        VasuAppraisalHandoff,
        VasuFirstAppraisalMenu,
        VasuFirstAppraisalWait,
        VasuFirstListIntroduction,
        VasuFirstListMenu,
        VasuFirstListWait,
        VasuFirstFinalText,
        VasuLinkedGreeting,
        VasuLinkedRingBoxOffer,
        VasuLinkedRingBoxReward,
        VasuNormalMenu,
        VasuNormalMenuActive,
        VasuNormalMenuWait,
        VasuFinalText,
        VasuHundredthText,
        VasuSpecialText,
        VasuSpecialReward,
        RedInitial,
        RedDelay,
        RedTopic,
        RedMore,
        RedLinkedMenu,
        BlueInitial,
        BlueLinkedMenu,
        BlueFortuneSetup,
        BlueLinkSetup,
        BlueCableTimeout,
        SnakeFinalText,
        SnakeRetreat,
        BookSecretsInitial,
        BookSecretsText,
        BookBasicsInitial,
        BookBasicsTopic,
        BookBasicsFortune,
        BookBasicsLink
    }

    private readonly RoomEventContext _context;
    private readonly VasuShopDatabase _database = new();
    private NpcCharacter? _npc;
    private GroundTreasurePickup? _reward;
    private EventStage _stage;
    private int _counter;
    private int _pendingRing = -1;
    private Func<RingMenuMode, Action, bool>? _openRingMenu;

    public VasuShopEvent(RoomEventContext context) => _context = context;

    public bool HasState => _stage != EventStage.Inactive;
    public bool BlocksGameplay => HasState && _stage != EventStage.SnakeRetreat;
    internal EventStage Stage => _stage;
    internal int Counter => _counter;
    internal VasuShopDatabase Database => _database;

    internal void SetRingMenuOpener(Func<RingMenuMode, Action, bool> opener)
    {
        ArgumentNullException.ThrowIfNull(opener);
        _openRingMenu = opener;
    }

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || _context.Rooms.ActiveGroup != _database.Group ||
            _context.Rooms.CurrentRoom.Id != _database.Room)
        {
            return false;
        }

        if (npc.Record is { Id: 0x89, SubId: 0x00 })
            BeginVasu(npc);
        else if (npc.Record is { Id: 0x89, SubId: 0x01 })
            BeginBlueSnake(npc);
        else if (npc.Record is { Id: 0x89, SubId: 0x06 })
            BeginRedSnake(npc);
        else if (npc.Record is { Id: 0xe5, SubId: 0x00 })
            BeginBasicsBook(npc);
        else if (npc.Record is { Id: 0xe5, SubId: 0x01 })
            BeginSecretsBook(npc);
        else
            return false;
        return true;
    }

    public void UpdateFrame()
    {
        switch (_stage)
        {
            case EventStage.VasuFirstExplanation:
                if (DialogueClosed())
                    ContinueVasuExplanation(TakeChoice());
                break;
            case EventStage.VasuRingBoxOffer:
                if (DialogueClosed())
                {
                    GiveTreasure("TREASURE_OBJECT_RING_BOX_00");
                    _stage = EventStage.VasuRingBoxReward;
                }
                break;
            case EventStage.VasuRingBoxReward:
                if (DialogueClosed())
                {
                    RemoveReward();
                    ShowText(0x303f);
                    _stage = EventStage.VasuFriendshipOffer;
                }
                break;
            case EventStage.VasuFriendshipOffer:
                if (DialogueClosed())
                {
                    GiveRing(_database.RingFriendship);
                    _stage = EventStage.VasuFriendshipReward;
                }
                break;
            case EventStage.VasuFriendshipReward:
                if (DialogueClosed())
                {
                    RemoveReward();
                    ShowText(0x3033);
                    _stage = EventStage.VasuAppraisalHandoff;
                }
                break;
            case EventStage.VasuAppraisalHandoff:
                if (DialogueClosed())
                {
                    OpenRingMenu(
                        RingMenuMode.Appraisal,
                        EventStage.VasuFirstAppraisalMenu,
                        () =>
                        {
                            _counter = _database.MenuCloseWait;
                            _stage = EventStage.VasuFirstAppraisalWait;
                        });
                }
                break;
            case EventStage.VasuFirstAppraisalMenu:
            case EventStage.VasuFirstListMenu:
            case EventStage.VasuNormalMenuActive:
                // GameRoot services the owning ring menu before room events;
                // these states are retained for validation and cancellation.
                break;
            case EventStage.VasuFirstAppraisalWait:
                if (--_counter <= 0)
                {
                    ShowText(0x3013);
                    _stage = EventStage.VasuFirstListIntroduction;
                }
                break;
            case EventStage.VasuFirstListIntroduction:
                if (DialogueClosed())
                {
                    OpenRingMenu(
                        RingMenuMode.List,
                        EventStage.VasuFirstListMenu,
                        () =>
                        {
                            _counter = _database.MenuCloseWait;
                            _stage = EventStage.VasuFirstListWait;
                        });
                }
                break;
            case EventStage.VasuFirstListWait:
                if (--_counter <= 0)
                {
                    ShowText(0x3008);
                    _stage = EventStage.VasuFirstFinalText;
                }
                break;
            case EventStage.VasuFirstFinalText:
                if (DialogueClosed())
                {
                    MarkRingBoxIntroductionComplete();
                    Finish();
                }
                break;
            case EventStage.VasuLinkedGreeting:
                if (DialogueClosed())
                {
                    if (_context.Inventory.HasTreasure(TreasureDatabase.TreasureRingBox))
                        CompleteLinkedIntroduction();
                    else
                    {
                        ShowText(0x303b);
                        _stage = EventStage.VasuLinkedRingBoxOffer;
                    }
                }
                break;
            case EventStage.VasuLinkedRingBoxOffer:
                if (DialogueClosed())
                {
                    GiveTreasure("TREASURE_OBJECT_RING_BOX_00");
                    _stage = EventStage.VasuLinkedRingBoxReward;
                }
                break;
            case EventStage.VasuLinkedRingBoxReward:
                if (DialogueClosed())
                {
                    RemoveReward();
                    CompleteLinkedIntroduction();
                }
                break;
            case EventStage.VasuNormalMenu:
                if (DialogueClosed())
                    ResolveVasuNormalChoice(TakeChoice());
                break;
            case EventStage.VasuNormalMenuWait:
                if (--_counter <= 0)
                    FinishNormalRingMenu();
                break;
            case EventStage.VasuFinalText:
                if (DialogueClosed())
                    Finish();
                break;
            case EventStage.VasuHundredthText:
                if (DialogueClosed())
                {
                    _context.Rooms.SaveData.SetGlobalFlag(
                        _database.GlobalAppraisedHundredth, value: false);
                    GiveRing(_database.RingHundredth);
                    _stage = EventStage.VasuSpecialReward;
                }
                break;
            case EventStage.VasuSpecialText:
                if (DialogueClosed())
                {
                    GiveRing(_pendingRing);
                    _stage = EventStage.VasuSpecialReward;
                }
                break;
            case EventStage.VasuSpecialReward:
                if (DialogueClosed())
                {
                    RemoveReward();
                    Finish();
                }
                break;
            case EventStage.RedInitial:
                if (DialogueClosed())
                {
                    if (TakeChoice() == 0)
                    {
                        _counter = _database.RedSnakeWait;
                        _stage = EventStage.RedDelay;
                    }
                    else
                        BeginSnakeRetreat();
                }
                break;
            case EventStage.RedDelay:
                if (--_counter <= 0)
                {
                    ShowChoice(0x300a);
                    _stage = EventStage.RedTopic;
                }
                break;
            case EventStage.RedTopic:
                if (DialogueClosed())
                {
                    ShowChoice(TakeChoice() == 1 ? 0x300c : 0x300b);
                    _stage = EventStage.RedMore;
                }
                break;
            case EventStage.RedMore:
                if (DialogueClosed())
                {
                    if (TakeChoice() == 0)
                    {
                        _counter = _database.RedSnakeWait;
                        _stage = EventStage.RedDelay;
                    }
                    else
                        BeginSnakeRetreat();
                }
                break;
            case EventStage.RedLinkedMenu:
                if (DialogueClosed())
                {
                    int choice = TakeChoice();
                    if (choice == 2)
                    {
                        ShowText(0x3010);
                        _stage = EventStage.SnakeFinalText;
                    }
                    else
                    {
                        DeferredMenu(choice == 0
                            ? "redSnake_openSecretInputMenu"
                            : "redSnake_generateRingSecret");
                        BeginSnakeRetreat();
                    }
                }
                break;
            case EventStage.BlueInitial:
                if (DialogueClosed())
                {
                    if (TakeChoice() == 0)
                        BeginBlueFortune();
                    else
                    {
                        ShowText(0x302e);
                        _stage = EventStage.SnakeFinalText;
                    }
                }
                break;
            case EventStage.BlueLinkedMenu:
                if (DialogueClosed())
                {
                    switch (TakeChoice())
                    {
                        case 0:
                            BeginBlueFortune();
                            break;
                        case 1:
                            ShowChoice(0x3028);
                            _stage = EventStage.BlueLinkSetup;
                            break;
                        default:
                            ShowText(0x302e);
                            _stage = EventStage.SnakeFinalText;
                            break;
                    }
                }
                break;
            case EventStage.BlueFortuneSetup:
                if (DialogueClosed())
                {
                    _ = TakeChoice();
                    _counter = _database.BlueSnakeCableTimeout;
                    _stage = EventStage.BlueCableTimeout;
                }
                break;
            case EventStage.BlueLinkSetup:
                if (DialogueClosed())
                {
                    _ = TakeChoice();
                    DeferredMenu("blueSnake_linkOrFortune: Game Link transfer");
                    BeginSnakeRetreat();
                }
                break;
            case EventStage.BlueCableTimeout:
                if (--_counter <= 0)
                {
                    ShowText(0x300f);
                    _stage = EventStage.SnakeFinalText;
                }
                break;
            case EventStage.SnakeFinalText:
                if (DialogueClosed())
                    BeginSnakeRetreat();
                break;
            case EventStage.SnakeRetreat:
                if (_npc?.CurrentAnimationParameter != 0)
                    FinishSnakeRetreat();
                break;
            case EventStage.BookSecretsInitial:
                if (DialogueClosed())
                {
                    if (TakeChoice() == 0)
                    {
                        ShowText(0x301a);
                        _stage = EventStage.BookSecretsText;
                    }
                    else
                        Finish();
                }
                break;
            case EventStage.BookSecretsText:
                if (DialogueClosed())
                    Finish();
                break;
            case EventStage.BookBasicsInitial:
                if (DialogueClosed())
                {
                    if (TakeChoice() == 0)
                    {
                        ShowChoice(0x3025);
                        _stage = EventStage.BookBasicsTopic;
                    }
                    else
                        Finish();
                }
                break;
            case EventStage.BookBasicsTopic:
                if (DialogueClosed())
                    ResolveBookTopic(TakeChoice());
                break;
            case EventStage.BookBasicsFortune:
            case EventStage.BookBasicsLink:
                if (DialogueClosed())
                {
                    if (TakeChoice() == 0)
                    {
                        ShowChoice(0x3025);
                        _stage = EventStage.BookBasicsTopic;
                    }
                    else
                        Finish();
                }
                break;
        }
    }

    public void Cancel()
    {
        RemoveReward();
        if (_npc is not null)
        {
            if (_npc.Record.Id == 0x89 && _npc.Record.SubId != 0)
                _npc.SetScriptAnimation(_database.Animation(0x89, _npc.Record.SubId));
            _npc.SetScriptButtonSensitive(true);
        }
        _npc = null;
        _stage = EventStage.Inactive;
        _counter = 0;
        _pendingRing = -1;
    }

    private void BeginVasu(NpcCharacter npc)
    {
        Start(npc);
        OracleSaveData save = _context.Rooms.SaveData;
        if (!save.HasGlobalFlag(_database.GlobalObtainedRingBox))
        {
            bool linkedFirst = save.IsLinkedGame &&
                (save.ReadWramByte(_database.ObtainedRingBoxAddress) &
                    _database.LinkedFirstMask) == 0;
            if (linkedFirst)
            {
                ShowText(0x303e);
                _stage = EventStage.VasuLinkedGreeting;
            }
            else
            {
                ShowChoice(0x3000);
                _stage = EventStage.VasuFirstExplanation;
            }
            return;
        }

        if (TryStartSpecialRing())
            return;
        ShowChoice(0x3003);
        _stage = EventStage.VasuNormalMenu;
    }

    private void BeginRedSnake(NpcCharacter npc)
    {
        StartSnake(npc);
        if (UseLinkedSnakeScript())
        {
            ShowChoice(0x3018);
            _stage = EventStage.RedLinkedMenu;
        }
        else
        {
            ShowChoice(0x3009);
            _stage = EventStage.RedInitial;
        }
    }

    private void BeginBlueSnake(NpcCharacter npc)
    {
        StartSnake(npc);
        if (UseLinkedSnakeScript())
        {
            ShowChoice(0x3024);
            _stage = EventStage.BlueLinkedMenu;
        }
        else
        {
            ShowChoice(0x301f);
            _stage = EventStage.BlueInitial;
        }
    }

    private void BeginSecretsBook(NpcCharacter npc)
    {
        Start(npc);
        ShowChoice(0x3019);
        _stage = EventStage.BookSecretsInitial;
    }

    private void BeginBasicsBook(NpcCharacter npc)
    {
        Start(npc);
        ShowChoice(0x3020);
        _stage = EventStage.BookBasicsInitial;
    }

    private void Start(NpcCharacter npc)
    {
        _npc = npc;
        _npc.SetScriptButtonSensitive(false);
        _pendingRing = -1;
    }

    private void StartSnake(NpcCharacter npc)
    {
        Start(npc);
        npc.SetScriptAnimation(_database.Animation(0x89, npc.Record.SubId + 1));
    }

    private void ContinueVasuExplanation(int choice)
    {
        if (choice != 0)
        {
            ShowChoice(0x303a);
            return;
        }
        if (_context.Inventory.HasTreasure(TreasureDatabase.TreasureRingBox))
        {
            ShowText(0x303f);
            _stage = EventStage.VasuFriendshipOffer;
        }
        else
        {
            ShowText(0x303b);
            _stage = EventStage.VasuRingBoxOffer;
        }
    }

    private void CompleteLinkedIntroduction()
    {
        OracleSaveData save = _context.Rooms.SaveData;
        save.SetGlobalFlag(_database.GlobalObtainedRingBox);
        bool changed = save.WriteWramByte(
            _database.ObtainedRingBoxAddress,
            (byte)(save.ReadWramByte(_database.ObtainedRingBoxAddress) |
                _database.LinkedFirstMask));
        if (changed)
            save.CommitInventoryChange();
        Finish();
    }

    private bool TryStartSpecialRing()
    {
        OracleSaveData save = _context.Rooms.SaveData;
        (int Earned, int Got, int Ring, int Text)[] rewards =
        [
            (_database.GlobalEarnedSlayer, _database.GlobalGotSlayer,
                _database.RingSlayer, 0x3036),
            (_database.GlobalEarnedWealth, _database.GlobalGotWealth,
                _database.RingWealth, 0x3037),
            (_database.GlobalEarnedVictory, _database.GlobalGotVictory,
                _database.RingVictory, 0x3039)
        ];
        foreach ((int earned, int got, int ring, int text) in rewards)
        {
            if (!save.HasGlobalFlag(earned) || save.HasGlobalFlag(got))
                continue;
            // vasu_checkEarnedSpecialRing sets the received flag before the
            // script displays the congratulatory text.
            save.SetGlobalFlag(got);
            _pendingRing = ring;
            ShowText(text);
            _stage = EventStage.VasuSpecialText;
            return true;
        }
        return false;
    }

    private void ResolveVasuNormalChoice(int choice)
    {
        if (choice == 2)
        {
            ShowText(0x3008);
            _stage = EventStage.VasuFinalText;
            return;
        }
        if (choice == 0 && _context.Inventory.UnappraisedRingCount == 0)
        {
            ShowText(0x3014);
            _stage = EventStage.VasuFinalText;
            return;
        }
        if (choice == 1 && !HasAppraisedRings())
        {
            ShowText(0x3015);
            _stage = EventStage.VasuFinalText;
            return;
        }

        OpenRingMenu(
            choice == 0 ? RingMenuMode.Appraisal : RingMenuMode.List,
            EventStage.VasuNormalMenuActive,
            () =>
            {
                _counter = _database.MenuCloseWait;
                _stage = EventStage.VasuNormalMenuWait;
            });
    }

    private bool HasAppraisedRings()
    {
        for (int ring = 0; ring < 0x40; ring++)
            if (_context.Inventory.HasAppraisedRing(ring))
                return true;
        return false;
    }

    private bool UseLinkedSnakeScript() =>
        _context.Inventory.HasTreasure(TreasureDatabase.TreasureRingBox) &&
        (_context.Rooms.SaveData.HasGlobalFlag(OracleSaveData.GlobalFlagFinishedGame) ||
         _context.Rooms.SaveData.IsLinkedGame);

    private void BeginBlueFortune()
    {
        ShowChoice(0x300e);
        _stage = EventStage.BlueFortuneSetup;
    }

    private void ResolveBookTopic(int choice)
    {
        switch (choice)
        {
            case 0:
                ShowChoice(0x303d);
                _stage = EventStage.BookBasicsFortune;
                break;
            case 1:
                ShowChoice(0x3026);
                _stage = EventStage.BookBasicsLink;
                break;
            default:
                Finish();
                break;
        }
    }

    private void BeginSnakeRetreat()
    {
        if (_npc is null || _npc.Record.Id != 0x89 || _npc.Record.SubId == 0)
            throw new InvalidOperationException("Only a Vasu snake can enter retreat state.");
        _npc.SetScriptAnimation(_database.Animation(0x89, _npc.Record.SubId + 2));
        _stage = EventStage.SnakeRetreat;
    }

    private void FinishSnakeRetreat()
    {
        _npc!.SetScriptAnimation(_database.Animation(0x89, _npc.Record.SubId));
        Finish();
    }

    private void GiveTreasure(string objectName)
    {
        TreasureDatabase.TreasureObjectRecord treasure =
            _context.Treasures.GetObject(objectName);
        _context.Inventory.GiveTreasure(treasure);
        CreateReward(treasure, _database.RingBoxGrabMode);
    }

    private void GiveRing(int ring)
    {
        TreasureDatabase.TreasureObjectRecord treasure =
            _context.Treasures.GetObject("TREASURE_OBJECT_RING_00");
        _context.Inventory.GiveUnappraisedRing(ring);
        CreateReward(treasure, _database.RingGrabMode);
    }

    private void CreateReward(
        TreasureDatabase.TreasureObjectRecord treasure,
        int grabMode)
    {
        if (_reward is not null)
            throw new InvalidOperationException("Vasu already has an active reward.");
        TreasureDatabase.TreasureObjectVisualRecord visual =
            _context.Treasures.GetObjectVisual(treasure.Graphic);
        Vector2 position = _context.Player.Position;
        var record = new GroundTreasureDatabase.Record(
            _database.Group,
            _database.Room,
            0,
            Mathf.FloorToInt(position.Y),
            Mathf.FloorToInt(position.X),
            treasure.Name,
            visual.Sprite,
            visual.TileBase,
            visual.Palette,
            visual.Animation,
            treasure.TextId,
            treasure.Message,
            $"scriptHelper.s:{(treasure.TreasureId == TreasureDatabase.TreasureRingBox ? "vasu_giveRingBox" : "vasu_giveRingInVar3a")}",
            SpawnMode: 0,
            GrabMode: grabMode);
        _reward = _context.Entities.Spawn<GroundTreasurePickup>(
            new GroundTreasureSpawn(record));
        int collectionSound = _context.Treasures.GetBehaviour(treasure.TreasureId).Sound;
        if (collectionSound != 0)
            _context.Sound.PlaySound(collectionSound);
        _reward.BeginGranted(_context.Player);
        _context.ShowDialogue(treasure.Message, _database.TextboxPosition);
    }

    private void RemoveReward()
    {
        if (_reward is null)
            return;
        _reward.Finish(_context.Player);
        _reward = null;
    }

    private void Finish(bool restoreSensitivity = true)
    {
        RemoveReward();
        if (_npc is not null && restoreSensitivity)
            _npc.SetScriptButtonSensitive(true);
        _npc = null;
        _stage = EventStage.Inactive;
        _counter = 0;
        _pendingRing = -1;
    }

    private bool DialogueClosed() => !_context.DialogueOpen;

    private int TakeChoice()
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
            throw new InvalidOperationException(
                $"Vasu Jewelers stage {_stage} closed without a text-option result.");
        return choice;
    }

    private void ShowText(int textId) =>
        _context.ShowDialogue(_database.Text(textId), _database.TextboxPosition);

    private void ShowChoice(int textId) =>
        _context.ShowChoiceDialogue(
            _database.Text(textId), textboxPosition: _database.TextboxPosition);

    private void OpenRingMenu(
        RingMenuMode mode, EventStage activeStage, Action completion)
    {
        Func<RingMenuMode, Action, bool> opener = _openRingMenu ??
            throw new InvalidOperationException(
                "Vasu Jewelers has no ring-menu controller.");
        _stage = activeStage;
        if (!opener(mode, completion))
            throw new InvalidOperationException(
                $"Vasu Jewelers could not open {mode}; another modal owns the menu lifecycle.");
    }

    private void FinishNormalRingMenu()
    {
        if (_context.Rooms.SaveData.HasGlobalFlag(
            _database.GlobalAppraisedHundredth))
        {
            ShowText(0x3038);
            _stage = EventStage.VasuHundredthText;
            return;
        }
        ShowText(0x3008);
        _stage = EventStage.VasuFinalText;
    }

    private void MarkRingBoxIntroductionComplete()
    {
        OracleSaveData save = _context.Rooms.SaveData;
        save.SetGlobalFlag(_database.GlobalObtainedRingBox);
        bool changed = save.WriteWramByte(
            _database.ObtainedRingBoxAddress,
            (byte)(save.ReadWramByte(_database.ObtainedRingBoxAddress) |
                _database.LinkedFirstMask));
        if (changed)
            save.CommitInventoryChange();
    }

    private static void DeferredMenu(string source) =>
        GD.PushWarning(
            $"Vasu Jewelers reached deferred source boundary '{source}'; " +
            "the ring-menu/Game Link subsystem is not implemented.");
}
