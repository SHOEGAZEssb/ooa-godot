using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Shared $46:$00/$01 and $47 shop flow: retail purchases, stock carrying,
/// theft prevention, and the hidden shop's chest-choice game.
/// </summary>
internal sealed partial class LynnaShopEvent : IRoomEntryEvent, IUpdatesDuringDialogueRoomEvent
{

    private readonly RoomEventContext _context;
    private RoomEventResources? _resources;
    private RoomEventResources EventResources => _resources ??= new(_context, this);
    private readonly LynnaShopDatabase _normalDatabase = new();
    private readonly LynnaShopDatabase _hiddenDatabase = new(hidden: true);
    private LynnaShopDatabase _database => _context.Rooms.CurrentRoom.Id == _hiddenDatabase.Room
        ? _hiddenDatabase : _normalDatabase;
    private LynnaShopItem? _item;
    private NpcCharacter? _shopkeeper;
    private LynnaShopEventStage _stage;
    private int _counter;
    private bool _cannotBuy;
    private bool _completesHeartContainer;

    public LynnaShopEvent(RoomEventContext context)
    {
        _context = context;
        context.RegisterHeartPiecePresentation(
            () => { if (_completesHeartContainer) context.Inventory.ResetCompletedHeartPieceSet(); },
            () =>
            {
                if (!_completesHeartContainer) return;
                _completesHeartContainer = false;
                context.Inventory.GiveCompletedHeartContainer(
                    context.Treasures.GetObject("TREASURE_OBJECT_HEART_CONTAINER_00"));
                context.Sound.PlaySound(OracleSoundEngine.SndFilledHeartContainer);
                ShowText(0x0049);
            });
    }

    public bool HasState => _stage != LynnaShopEventStage.Inactive;
    public bool BlocksGameplay => HasState && _stage is not
        (LynnaShopEventStage.Holding or LynnaShopEventStage.ChestSelection);
    public bool FreezesNonInteractionObjects => BlocksGameplay;
    public bool MenusDisabled => BlocksGameplay;
    internal LynnaShopEventStage Stage => _stage;

    public bool TryInteractPlayer(Player player)
    {
        if (!MatchesCurrentRoom())
            return false;
        if (TryInteractShopChest(player))
            return true;
        if (_stage == LynnaShopEventStage.Holding)
        {
            if (_item is null)
                throw new InvalidOperationException("Lynna shop lost its held item.");
            if (CanReturnHeldItem(player, _item))
                ReturnHeldItem();
            // The face button belongs to wLinkGrabState while carrying stock;
            // consume it even away from the shelf so Link cannot use an item.
            return true;
        }
        if (_stage != LynnaShopEventStage.Inactive)
            return true;

        LynnaShopItem? candidate = null;
        foreach (LynnaShopItem item in _context.Entities.Entities<LynnaShopItem>())
        {
            if (!item.CanPickup(
                player, _database.ItemCollisionRadius,
                _database.LinkCollisionRadius,
                _database.GrabNegativePointOffset,
                _database.GrabPositivePointOffset))
            {
                continue;
            }
            if (candidate is null || item.Order < candidate.Order)
                candidate = item;
        }
        if (candidate is null)
            return false;

        _item = candidate;
        candidate.Pickup(player);
        _stage = LynnaShopEventStage.Holding;
        return true;
    }

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (!MatchesCurrentRoom() || npc.Record.Id != 0x46 ||
            npc.Record.SubId != _database.ShopkeeperSubId ||
            _stage is not (LynnaShopEventStage.Inactive or LynnaShopEventStage.Holding or LynnaShopEventStage.ChestSelection))
        {
            return false;
        }

        _shopkeeper = npc;
        npc.SetScriptButtonSensitive(false);
        FaceShopkeeperTowardPlayer();
        if (_chestGame)
        {
            BeginChestConversation();
            return true;
        }
        if (_stage == LynnaShopEventStage.Holding)
        {
            if (_item is null)
                throw new InvalidOperationException("Lynna shop lost its held product.");
            if (_item.Record.SubId is 0x00 or 0x14 && !_context.Inventory.HasTreasure(0x2c))
            {
                ShowText(0x0e0b);
                _stage = LynnaShopEventStage.PurchaseRejected;
                return true;
            }
            _cannotBuy = !_database.Hidden && CannotBuy(_item.Record);
            ShowChoice(_item.Record.PromptTextId, _item.Record.Price);
            _stage = LynnaShopEventStage.PurchasePrompt;
        }
        else
        {
            ShowText(HasAvailableStock() ? 0x0e00 : 0x0e26);
            _stage = LynnaShopEventStage.ShopkeeperText;
        }
        return true;
    }

    public void UpdateFrame()
    {
        UpdateChestPrizeVisual();
        if (UpdateHiddenShop())
            return;
        switch (_stage)
        {
            case LynnaShopEventStage.Holding:
                if (_item is null)
                    throw new InvalidOperationException("Lynna shop lost its held product.");
                if (_database.Hidden ? _context.Player.Position.Y < _database.TheftLinkY
                    : _context.Player.Position.Y > _database.TheftLinkY)
                    BeginTheftPrevention();
                break;

            case LynnaShopEventStage.ShopkeeperText:
                if (DialogueClosed())
                    FinishShopkeeperTalk();
                break;

            case LynnaShopEventStage.PurchasePrompt:
                if (DialogueClosed())
                    ResolvePurchase(TakeChoice());
                break;

            case LynnaShopEventStage.PurchaseRejected:
                if (DialogueClosed())
                    ReturnHeldItem();
                break;

            case LynnaShopEventStage.ItemText:
                if (DialogueClosed())
                    FinishPurchase();
                break;

            case LynnaShopEventStage.TheftDown:
                if (MoveShopkeeper(Vector2.Down, 2))
                    BeginTheftMove(LynnaShopEventStage.TheftLeft, Vector2I.Left, 12);
                break;

            case LynnaShopEventStage.TheftLeft:
                if (MoveShopkeeper(Vector2.Left, 2))
                {
                    ShowText(0x0e07);
                    _stage = LynnaShopEventStage.TheftText;
                }
                break;

            case LynnaShopEventStage.TheftText:
                if (DialogueClosed())
                    BeginTheftMove(LynnaShopEventStage.TheftRight, Vector2I.Right, 12);
                break;

            case LynnaShopEventStage.TheftRight:
                if (MoveShopkeeper(Vector2.Right, 2))
                    BeginTheftMove(LynnaShopEventStage.TheftUp, Vector2I.Up, 4);
                break;

            case LynnaShopEventStage.TheftUp:
                if (MoveShopkeeper(Vector2.Up, 2))
                    FinishTheftPrevention();
                break;
        }
    }

    public void Cancel()
    {
        if (_item?.Purchasing == true)
        {
            _item.FinishPurchase(_context.Player);
            _context.Player.CancelGetItemState();
        }
        if (_item?.Held == true)
            _item.ReturnToShelf(_context.Player);
        if (_shopkeeper is not null)
        {
            _shopkeeper.SetCollisionRadii(
                _database.ShopkeeperRadiusY, _database.ShopkeeperRadiusX);
            _shopkeeper.SetScriptAnimation(_database.Animation(0x46, _database.IdleAnimation));
            _shopkeeper.SetScriptButtonSensitive(true);
        }
        _context.Player.EndCutsceneControl(this);
        _item = null;
        _shopkeeper = null;
        _stage = LynnaShopEventStage.Inactive;
        _counter = 0;
        _cannotBuy = false;
        CancelChestGame();
        _completesHeartContainer = false;
        _context.Player.EndCutsceneControl(this);
    }

    private void ResolvePurchase(int choice)
    {
        if (_item is null)
            throw new InvalidOperationException("Lynna shop purchase has no product.");
        if (choice != 0)
        {
            ReturnHeldItem();
            return;
        }
        if (_cannotBuy)
        {
            ShowText(0x0e05);
            _stage = LynnaShopEventStage.PurchaseRejected;
            return;
        }
        if (_context.Inventory.Rupees < _item.Record.Price)
        {
            ShowText(0x0e06);
            _stage = LynnaShopEventStage.PurchaseRejected;
            return;
        }

        ItemRecord item = _item.Record;
        _completesHeartContainer = item.TreasureId == 0x2b && _context.Inventory.HeartPieces == 3;
        _context.Inventory.AddRupees(-item.Price);
        TreasureObjectRecord treasure = new TreasureObjectRecord(
            $"SHOP_ITEM_{item.SubId:x2}",
            item.TreasureId,
            item.SubId,
            item.Parameter,
            item.ItemTextId,
            0,
            _database.Text(item.ItemTextId));
        if (item.TreasureId == 0)
            GiveRandomRing(item.Parameter);
        else
            _context.Inventory.GiveTreasure(treasure);
        if (_database.Hidden)
            MarkHiddenPurchase(item.SubId);
        if (item.SubId == 0x13)
            SetBoughtItems1Mask(_database.NormalGashaBoughtMask);

        int sound = _context.Treasures.GetBehaviour(item.TreasureId == 0 ? 0x2d : item.TreasureId).Sound;
        if (sound != 0)
            _context.Sound.PlaySound(sound);
        _context.ShowDialogue(treasure.Message, _database.TextboxPosition);
        _stage = LynnaShopEventStage.ItemText;
        if (_database.Hidden)
        {
            _item.BeginPurchase(_context.Player);
            _context.Player.RequestGetItemState(0x01, () => _stage == LynnaShopEventStage.ItemText);
        }
    }

    private bool CannotBuy(ItemRecord item) => item.SubId switch
    {
        0x01 => _context.Inventory.HealthQuarters ==
            _context.Inventory.MaxHealthQuarters,
        0x04 => _context.Inventory.Bombs == _context.Inventory.MaxBombs,
        0x03 or 0x11 or 0x12 =>
            _context.Inventory.HasTreasure(0x01),
        0x0d => _context.Inventory.HasTreasure(0x0e),
        0x13 => false,
        _ => throw new InvalidOperationException(
            $"Unsupported normal-shop stock $47:${item.SubId:x2}.")
    };

    private void SetBoughtItems1Mask(int mask)
    {
        OracleSaveData save = _context.Rooms.SaveData;
        byte previous = save.ReadWramByte(_database.BoughtItems1Address);
        if (save.WriteWramByte(
            _database.BoughtItems1Address, (byte)(previous | mask)))
        {
            save.CommitInventoryChange();
        }
    }

    private void FinishPurchase()
    {
        _item?.FinishPurchase(_context.Player);
        _item = null;
        FinishShopkeeperTalk();
    }

    private void ReturnHeldItem()
    {
        _item?.ReturnToShelf(_context.Player);
        _item = null;
        FinishShopkeeperTalk();
    }

    private void FinishShopkeeperTalk()
    {
        if (_shopkeeper is not null)
        {
            _shopkeeper.SetCollisionRadii(
                _database.ShopkeeperRadiusY, _database.ShopkeeperRadiusX);
            _shopkeeper.SetScriptAnimation(_database.Animation(0x46, _database.IdleAnimation));
            _shopkeeper.SetScriptButtonSensitive(true);
        }
        _shopkeeper = null;
        _stage = LynnaShopEventStage.Inactive;
        _cannotBuy = false;
    }

    private bool CanReturnHeldItem(Player player, LynnaShopItem item) =>
        player.FacingVector == Vector2I.Up &&
        player.Position.Y < _database.SelectionLinkYLimit &&
        player.Position.X > item.ShelfPosition.X - _database.SelectionXRadius &&
        player.Position.X < item.ShelfPosition.X + _database.SelectionXRadius;

    private bool HasAvailableStock()
    {
        foreach (LynnaShopItem item in _context.Entities.Entities<LynnaShopItem>())
            if (!item.Removed)
                return true;
        return false;
    }

    private void BeginTheftPrevention()
    {
        _shopkeeper = _context.RequireNpc(
            _database.Group, _database.Room, 0x46, _database.ShopkeeperSubId, "Lynna shopkeeper");
        _shopkeeper.SetScriptButtonSensitive(false);
        _shopkeeper.SetCollisionRadii(
            _database.ShopkeeperRadiusY, _database.ShopkeeperRadiusY);
        _context.Player.SetScriptedCoordinateHigh(
            horizontal: false, coordinate: _database.TheftLinkY);
        _context.Player.BeginCutsceneControl(owner: this);
        if (_database.Hidden)
        {
            BeginHiddenTheft();
            return;
        }
        _context.Sound.PlaySound(OracleSoundEngine.SndClink);
        BeginTheftMove(LynnaShopEventStage.TheftDown, Vector2I.Down, 4);
    }

    private void BeginTheftMove(
        LynnaShopEventStage stage,
        Vector2I direction,
        int counter)
    {
        _stage = stage;
        _counter = counter;
        _shopkeeper!.SetScriptAnimation(
            _database.Animation(0x46, AnimationForDirection(direction)));
    }

    private bool MoveShopkeeper(Vector2 direction, int pixels)
    {
        if (_shopkeeper is null)
            throw new InvalidOperationException("Lynna theft script has no shopkeeper.");
        _shopkeeper.SetStatePosition(
            _shopkeeper.Position + direction * pixels);
        _counter--;
        return _counter == 0;
    }

    private void FinishTheftPrevention()
    {
        _shopkeeper!.SetCollisionRadii(
            _database.ShopkeeperRadiusY, _database.ShopkeeperRadiusX);
        _shopkeeper.SetScriptAnimation(_database.Animation(0x46, _database.IdleAnimation));
        _shopkeeper.SetScriptButtonSensitive(true);
        _shopkeeper = null;
        _context.Player.EndCutsceneControl(this);
        _stage = LynnaShopEventStage.Holding;
    }

    private void FaceShopkeeperTowardPlayer()
    {
        if (_shopkeeper is null)
            return;
        Vector2 delta = _context.Player.Position - _shopkeeper.Position;
        Vector2I direction = Mathf.Abs(delta.X) > Mathf.Abs(delta.Y)
            ? (delta.X >= 0 ? Vector2I.Right : Vector2I.Left)
            : (delta.Y >= 0 ? Vector2I.Down : Vector2I.Up);
        _shopkeeper.SetScriptAnimation(
            _database.Animation(0x46, AnimationForDirection(direction)));
    }

    private static int AnimationForDirection(Vector2I direction) =>
        direction == Vector2I.Up ? 0
        : direction == Vector2I.Right ? 1
        : direction == Vector2I.Down ? 2
        : 3;

    private bool MatchesCurrentRoom() =>
        _context.Rooms.ActiveGroup == _database.Group &&
        _context.Rooms.CurrentRoom.Id == _database.Room;

    private bool DialogueClosed() => !_context.DialogueOpen;

    private int TakeChoice() =>
        EventResources.RequireDialogueChoice("Lynna shop prompt closed without a text-option result.");

    private void ShowText(int textId) =>
        _context.ShowDialogue(_database.Text(textId), _database.TextboxPosition);

    private void ShowChoice(int textId, int price) =>
        _context.ShowChoiceDialogue(
            _database.Text(textId).Replace(
                "\\num1", price.ToString(), StringComparison.Ordinal),
            textboxPosition: _database.TextboxPosition);
}

internal enum LynnaShopEventStage
{
    Inactive,
    Holding,
    ShopkeeperText,
    PurchasePrompt,
    PurchaseRejected,
    ItemText,
    TheftDown,
    TheftLeft,
    TheftText,
    TheftRight,
    TheftUp,
    HiddenMove, HiddenTheftText, ChestPrompt, ChestDeclined, ChestPrepareRight,
    ChestPrepareLeft, ChestInstructions, ChestSelection, ChestTalk, ChestWrong,
    ChestCorrect, ChestPrize, ChestRingText
}
