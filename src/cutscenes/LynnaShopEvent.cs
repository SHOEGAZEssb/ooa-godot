using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Native $2:$5e shop flow: lift/return stock, $46:$00 purchase scripts,
/// already-full and rupee checks, item grants, and theft prevention.
/// </summary>
internal sealed class LynnaShopEvent : IRoomEvent
{
    internal enum EventStage
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
        TheftUp
    }

    private readonly RoomEventContext _context;
    private readonly LynnaShopDatabase _database = new();
    private LynnaShopItem? _item;
    private NpcCharacter? _shopkeeper;
    private EventStage _stage;
    private int _counter;
    private bool _cannotBuy;

    public LynnaShopEvent(RoomEventContext context) => _context = context;

    public bool HasState => _stage != EventStage.Inactive;
    public bool BlocksGameplay => HasState && _stage != EventStage.Holding;
    internal EventStage Stage => _stage;
    internal LynnaShopDatabase Database => _database;
    internal LynnaShopItem? HeldItem => _item;
    internal int Counter => _counter;

    public bool TryInteractPlayer(Player player)
    {
        if (!MatchesCurrentRoom())
            return false;
        if (_stage == EventStage.Holding)
        {
            if (_item is null)
                throw new InvalidOperationException("Lynna shop lost its held item.");
            if (CanReturnHeldItem(player, _item))
                ReturnHeldItem();
            // The face button belongs to wLinkGrabState while carrying stock;
            // consume it even away from the shelf so Link cannot use an item.
            return true;
        }
        if (_stage != EventStage.Inactive)
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
        _stage = EventStage.Holding;
        return true;
    }

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (!MatchesCurrentRoom() || npc.Record is not { Id: 0x46, SubId: 0x00 } ||
            _stage is not (EventStage.Inactive or EventStage.Holding))
        {
            return false;
        }

        _shopkeeper = npc;
        npc.SetScriptButtonSensitive(false);
        FaceShopkeeperTowardPlayer();
        if (_stage == EventStage.Holding)
        {
            if (_item is null)
                throw new InvalidOperationException("Lynna shop lost its held product.");
            _cannotBuy = CannotBuy(_item.Record);
            ShowChoice(_item.Record.PromptTextId, _item.Record.Price);
            _stage = EventStage.PurchasePrompt;
        }
        else
        {
            ShowText(HasAvailableStock() ? 0x0e00 : 0x0e26);
            _stage = EventStage.ShopkeeperText;
        }
        return true;
    }

    public void UpdateFrame()
    {
        switch (_stage)
        {
            case EventStage.Holding:
                if (_item is null)
                    throw new InvalidOperationException("Lynna shop lost its held product.");
                if (_context.Player.Position.Y > _database.TheftLinkY)
                    BeginTheftPrevention();
                break;

            case EventStage.ShopkeeperText:
                if (DialogueClosed())
                    FinishShopkeeperTalk();
                break;

            case EventStage.PurchasePrompt:
                if (DialogueClosed())
                    ResolvePurchase(TakeChoice());
                break;

            case EventStage.PurchaseRejected:
                if (DialogueClosed())
                    ReturnHeldItem();
                break;

            case EventStage.ItemText:
                if (DialogueClosed())
                    FinishPurchase();
                break;

            case EventStage.TheftDown:
                if (MoveShopkeeper(Vector2.Down, 2))
                    BeginTheftMove(EventStage.TheftLeft, Vector2I.Left, 12);
                break;

            case EventStage.TheftLeft:
                if (MoveShopkeeper(Vector2.Left, 2))
                {
                    ShowText(0x0e07);
                    _stage = EventStage.TheftText;
                }
                break;

            case EventStage.TheftText:
                if (DialogueClosed())
                    BeginTheftMove(EventStage.TheftRight, Vector2I.Right, 12);
                break;

            case EventStage.TheftRight:
                if (MoveShopkeeper(Vector2.Right, 2))
                    BeginTheftMove(EventStage.TheftUp, Vector2I.Up, 4);
                break;

            case EventStage.TheftUp:
                if (MoveShopkeeper(Vector2.Up, 2))
                    FinishTheftPrevention();
                break;
        }
    }

    public void Cancel()
    {
        if (_item?.Held == true)
            _item.ReturnToShelf(_context.Player);
        if (_shopkeeper is not null)
        {
            _shopkeeper.SetCollisionRadii(
                _database.ShopkeeperRadiusY, _database.ShopkeeperRadiusX);
            _shopkeeper.SetScriptAnimation(_database.Animation(0x46, 3));
            _shopkeeper.SetScriptButtonSensitive(true);
        }
        _context.Player.EndCutsceneControl();
        _item = null;
        _shopkeeper = null;
        _stage = EventStage.Inactive;
        _counter = 0;
        _cannotBuy = false;
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
            _stage = EventStage.PurchaseRejected;
            return;
        }
        if (_context.Inventory.Rupees < _item.Record.Price)
        {
            ShowText(0x0e06);
            _stage = EventStage.PurchaseRejected;
            return;
        }

        LynnaShopDatabase.ItemRecord item = _item.Record;
        _context.Inventory.AddRupees(-item.Price);
        var treasure = new TreasureDatabase.TreasureObjectRecord(
            $"SHOP_ITEM_{item.SubId:x2}",
            item.TreasureId,
            item.SubId,
            item.Parameter,
            item.ItemTextId,
            0,
            _database.Text(item.ItemTextId));
        _context.Inventory.GiveTreasure(treasure);
        if (item.SubId == 0x13)
            SetBoughtItems1Mask(_database.NormalGashaBoughtMask);

        int sound = _context.Treasures.GetBehaviour(item.TreasureId).Sound;
        if (sound != 0)
            _context.Sound.PlaySound(sound);
        _context.ShowDialogue(treasure.Message, _database.TextboxPosition);
        _stage = EventStage.ItemText;
    }

    private bool CannotBuy(LynnaShopDatabase.ItemRecord item) => item.SubId switch
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
            _shopkeeper.SetScriptAnimation(_database.Animation(0x46, 3));
            _shopkeeper.SetScriptButtonSensitive(true);
        }
        _shopkeeper = null;
        _stage = EventStage.Inactive;
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
            _database.Group, _database.Room, 0x46, 0x00, "Lynna shopkeeper");
        _shopkeeper.SetScriptButtonSensitive(false);
        _shopkeeper.SetCollisionRadii(
            _database.ShopkeeperRadiusY, _database.ShopkeeperRadiusY);
        _context.Player.SetScriptedCoordinateHigh(
            horizontal: false, coordinate: _database.TheftLinkY);
        _context.Player.BeginCutsceneControl();
        _context.Sound.PlaySound(OracleSoundEngine.SndClink);
        BeginTheftMove(EventStage.TheftDown, Vector2I.Down, 4);
    }

    private void BeginTheftMove(
        EventStage stage,
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
        _shopkeeper.SetScriptAnimation(_database.Animation(0x46, 3));
        _shopkeeper.SetScriptButtonSensitive(true);
        _shopkeeper = null;
        _context.Player.EndCutsceneControl();
        _stage = EventStage.Holding;
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

    private int TakeChoice()
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
            throw new InvalidOperationException(
                "Lynna shop prompt closed without a text-option result.");
        return choice;
    }

    private void ShowText(int textId) =>
        _context.ShowDialogue(_database.Text(textId), _database.TextboxPosition);

    private void ShowChoice(int textId, int price) =>
        _context.ShowChoiceDialogue(
            _database.Text(textId).Replace(
                "\\num1", price.ToString(), StringComparison.Ordinal),
            textboxPosition: _database.TextboxPosition);
}
