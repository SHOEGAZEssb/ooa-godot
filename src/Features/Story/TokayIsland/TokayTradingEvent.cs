using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Owns the Tokay trading-hut conversation, selected stock item, and exchange
/// reward lifecycle. The physical stock remains owned by room entities.
/// </summary>
internal sealed class TokayTradingEvent :
    IRoomEvent, IUpdatesDuringDialogueRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayInteractionDatabase _interactions;
    private readonly TokayShopDatabase _shop;
    private TokayTradingStage _stage;
    private TokayShopItem? _shopItem;
    private GroundTreasurePickup? _reward;
    private bool _refreshShopItemsNextUpdate;
    private bool _inputLocked;

    internal TokayTradingEvent(
        RoomEventContext context,
        TokayInteractionDatabase interactions,
        TokayShopDatabase shop)
    {
        _context = context;
        _interactions = interactions;
        _shop = shop;
    }

    public bool HasState => _stage != TokayTradingStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal TokayTradingStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || npc.Record is not { Id: 0x48, SubId: 0x0e })
            return false;

        ShowDialogueOnly(
            _context.Entities.Entities<TokayShopItem>().Any(item => !item.Removed)
                ? 0x0a37
                : 0x0a38);
        return true;
    }

    internal bool TryInteractPlayer(Player player)
    {
        if (HasState ||
            _context.Rooms.ActiveGroup != _shop.Group ||
            _context.Rooms.CurrentRoom.Id != _shop.Room)
        {
            return false;
        }

        TokayShopItem? candidate = null;
        foreach (TokayShopItem item in _context.Entities.Entities<TokayShopItem>())
        {
            if (!item.CanInteract(player))
                continue;
            if (candidate is null || item.Placement.Order < candidate.Placement.Order)
                candidate = item;
        }
        if (candidate is null)
            return false;

        _shopItem = candidate;
        LockInput();
        BeginShopItem(candidate);
        return true;
    }

    public void UpdateFrame()
    {
        if (_stage == TokayTradingStage.Inactive)
            return;
        RefreshShopItemsIfPending();
        if (_stage == TokayTradingStage.ShopReward)
        {
            if (_reward is { Finished: true })
            {
                _reward = null;
                FinishInteraction();
            }
            return;
        }
        if (_context.DialogueOpen)
            return;

        switch (_stage)
        {
            case TokayTradingStage.DialogueOnly:
            case TokayTradingStage.ShopResultText:
                FinishInteraction();
                break;
            case TokayTradingStage.ShopPrompt:
                ResolveShopChoice();
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay trading stage {_stage} closed an unexpected dialogue.");
        }
    }

    public void UpdateDuringDialogueFrame() => RefreshShopItemsIfPending();

    public void Cancel()
    {
        _reward?.Finish(_context.Player);
        _reward = null;
        _refreshShopItemsNextUpdate = false;
        UnlockInput();
        _shopItem = null;
        _stage = TokayTradingStage.Inactive;
    }

    private void BeginShopItem(TokayShopItem item)
    {
        switch (item.SubId)
        {
            case 0:
                if (_context.Inventory.MysterySeeds != 0)
                    ShowChoice(0x0a2b);
                else if (_context.Inventory.HasTreasure(TreasureDatabase.TreasureShovel))
                    ShowChoice(0x0a2c);
                else
                    ShowChoice(0x0a27);
                break;
            case 1:
                if (_context.Inventory.ScentSeeds != 0)
                    ShowChoice(0x0a32);
                else if (_context.Inventory.HasTreasure(TreasureDatabase.TreasureShovel))
                    ShowChoice(0x0a33);
                else
                    ShowChoice(0x0a30);
                break;
            case 2:
                ShowChoice(0x0a36);
                break;
            case 3:
                ShowChoice(0x0a35);
                break;
            case >= 4 and <= 6:
                ShowChoice(0x0a39);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Tokay shop item $81:${item.SubId:x2}.");
        }
        _stage = TokayTradingStage.ShopPrompt;
    }

    private void ResolveShopChoice()
    {
        TokayShopItem item = _shopItem ??
            throw new InvalidOperationException("Tokay shop lost its selected item.");
        if (TakeChoice() != 0)
        {
            bool firstDecline = item.SubId switch
            {
                0 => _context.Inventory.MysterySeeds != 0 ||
                    _context.Inventory.HasTreasure(TreasureDatabase.TreasureShovel),
                1 => _context.Inventory.ScentSeeds != 0 ||
                    _context.Inventory.HasTreasure(TreasureDatabase.TreasureShovel),
                >= 4 => true,
                _ => false
            };
            Show(firstDecline ? 0x0a2d : 0x0a29);
            _stage = TokayTradingStage.ShopResultText;
            return;
        }

        InventoryState inventory = _context.Inventory;
        int giveTreasure;
        int parameter;
        string objectName;
        int loseTreasure = -1;
        int globalFlag = -1;
        int seedTreasure = -1;
        int insufficientSeedsText = 0;
        switch (item.SubId)
        {
            case 0 when inventory.MysterySeeds != 0:
                giveTreasure = TreasureDatabase.TreasureFeather;
                parameter = 2;
                objectName = "TREASURE_OBJECT_FEATHER_02";
                globalFlag = _shop.BoughtFeatherFlag;
                seedTreasure = 0x24;
                insufficientSeedsText = 0x0a2e;
                break;
            case 0 when inventory.HasTreasure(TreasureDatabase.TreasureShovel):
                giveTreasure = TreasureDatabase.TreasureFeather;
                parameter = 2;
                objectName = "TREASURE_OBJECT_FEATHER_02";
                loseTreasure = TreasureDatabase.TreasureShovel;
                break;
            case 0:
                Show(0x0a28);
                giveTreasure = TreasureDatabase.TreasureShovel;
                parameter = 2;
                objectName = "TREASURE_OBJECT_SHOVEL_02";
                loseTreasure = TreasureDatabase.TreasureBracelet;
                break;
            case 1 when inventory.ScentSeeds != 0:
                giveTreasure = TreasureDatabase.TreasureBracelet;
                parameter = 3;
                objectName = "TREASURE_OBJECT_BRACELET_03";
                globalFlag = _shop.BoughtBraceletFlag;
                seedTreasure = 0x21;
                insufficientSeedsText = 0x0a34;
                break;
            case 1 when inventory.HasTreasure(TreasureDatabase.TreasureShovel):
                giveTreasure = TreasureDatabase.TreasureBracelet;
                parameter = 3;
                objectName = "TREASURE_OBJECT_BRACELET_03";
                loseTreasure = TreasureDatabase.TreasureShovel;
                break;
            case 1:
            case 2:
                Show(0x0a28);
                giveTreasure = TreasureDatabase.TreasureShovel;
                parameter = 2;
                objectName = "TREASURE_OBJECT_SHOVEL_02";
                loseTreasure = TreasureDatabase.TreasureFeather;
                break;
            case 3:
                Show(0x0a28);
                giveTreasure = TreasureDatabase.TreasureShovel;
                parameter = 2;
                objectName = "TREASURE_OBJECT_SHOVEL_02";
                loseTreasure = TreasureDatabase.TreasureBracelet;
                break;
            default:
                if (inventory.ShieldLevel != 0)
                {
                    Show(0x0a3a);
                    _stage = TokayTradingStage.ShopResultText;
                    return;
                }
                giveTreasure = TreasureDatabase.TreasureShield;
                parameter = item.SubId - 4;
                objectName = $"TREASURE_OBJECT_SHIELD_{parameter:x2}";
                seedTreasure = 0x21;
                insufficientSeedsText = 0x0a34;
                break;
        }

        int objectParameter = ShopRewardObjectParameter(giveTreasure, parameter);
        TreasureObjectRecord rewardObject =
            _context.Treasures.GetObject(objectName);
        if (rewardObject.TreasureId != giveTreasure ||
            rewardObject.SubId != parameter ||
            rewardObject.Parameter != objectParameter)
        {
            throw new InvalidOperationException(
                $"{objectName} no longer matches tokayShopItemScript's " +
                $"expected treasure ${giveTreasure:x2}:${parameter:x2}, " +
                $"parameter ${objectParameter:x2}.");
        }
        if (seedTreasure >= 0 &&
            !inventory.TryConsumeSeedsFromScript(seedTreasure, 10))
        {
            Show(insufficientSeedsText);
            _stage = TokayTradingStage.ShopResultText;
            return;
        }
        if (loseTreasure >= 0)
            inventory.LoseTreasure(loseTreasure);
        if (globalFlag >= 0)
        {
            _context.Rooms.SaveData.SetGlobalFlag(globalFlag);
            item.Remove();
        }
        _reward = _context.GrantScriptTreasure(
            _context.Rooms.ActiveGroup,
            _context.Rooms.CurrentRoom.Id,
            giveTreasure,
            parameter,
            objectName,
            "scripts/ages:tokayShopItemScript",
            objectParameter: objectParameter);
        _refreshShopItemsNextUpdate = true;
        _stage = TokayTradingStage.ShopReward;
    }

    internal static int ShopRewardObjectParameter(
        int treasure,
        int subId) => (treasure, subId) switch
    {
        (TreasureDatabase.TreasureFeather, 0x02) => 0x01,
        (TreasureDatabase.TreasureBracelet, 0x03) => 0x01,
        (TreasureDatabase.TreasureShovel, 0x02) => 0x00,
        (TreasureDatabase.TreasureShield, >= 0x00 and <= 0x02) => subId + 1,
        _ => throw new InvalidOperationException(
            $"tokayShopItemScript has no reward contract for " +
            $"${treasure:x2}:${subId:x2}.")
    };

    private void RefreshShopItemsIfPending()
    {
        if (!_refreshShopItemsNextUpdate)
            return;

        // Every tokayShopItem.s:@state1 object checks @checkTransformItem
        // before interactionRunScript. An accepted exchange therefore changes
        // inventory in the selected object's script pass, then every surviving
        // stock object transforms on the following update while reward text is
        // still active.
        _refreshShopItemsNextUpdate = false;
        foreach (TokayShopItem item in
            _context.Entities.Entities<TokayShopItem>())
        {
            RefreshShopItem(item);
        }
    }

    private void RefreshShopItem(TokayShopItem item)
    {
        if (item.Removed)
            return;

        int subId;
        int treasure;
        if (item.OriginalSubId == 0)
        {
            subId = _context.Inventory.HasTreasure(TreasureDatabase.TreasureFeather)
                ? 2
                : 0;
            treasure = subId == 2
                ? TreasureDatabase.TreasureShovel
                : TreasureDatabase.TreasureFeather;
        }
        else if (item.OriginalSubId == 1)
        {
            subId = _context.Inventory.HasTreasure(TreasureDatabase.TreasureBracelet)
                ? 3
                : 1;
            treasure = subId == 3
                ? TreasureDatabase.TreasureShovel
                : TreasureDatabase.TreasureBracelet;
        }
        else
        {
            subId = Math.Clamp(
                4 + Math.Max(0, _context.Inventory.ShieldLevel - 1), 4, 6);
            treasure = TreasureDatabase.TreasureShield;
        }

        TokayShopPlacementRecord visual = _shop.Visual(subId) with
        {
            Order = item.Placement.Order,
            Y = item.Placement.Y,
            X = item.Placement.X
        };
        item.Initialize(
            visual, item.OriginalSubId, subId, treasure,
            _shop.ItemCollisionRadius);
    }

    private void ShowDialogueOnly(int textId)
    {
        Show(textId);
        _stage = TokayTradingStage.DialogueOnly;
    }

    private void Show(int textId) =>
        _context.ShowDialogue(_interactions.Text(textId));

    private void ShowChoice(int textId) =>
        _context.ShowChoiceDialogue(_interactions.Text(textId));

    private int TakeChoice()
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
            throw new InvalidOperationException(
                "Tokay trading prompt closed without a text-option result.");
        return choice;
    }

    private void LockInput()
    {
        if (_inputLocked)
            return;
        _context.Player.BeginCutsceneControl();
        _inputLocked = true;
    }

    private void UnlockInput()
    {
        if (!_inputLocked)
            return;
        _context.Player.EndCutsceneControl();
        _inputLocked = false;
    }

    private void FinishInteraction()
    {
        UnlockInput();
        _shopItem = null;
        _stage = TokayTradingStage.Inactive;
    }
}

internal enum TokayTradingStage
{
    Inactive,
    DialogueOnly,
    ShopPrompt,
    ShopResultText,
    ShopReward
}
