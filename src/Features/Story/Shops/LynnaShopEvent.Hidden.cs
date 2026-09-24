using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class LynnaShopEvent
{
    private bool _chestGame;
    private int _round;
    private int _correctChest;
    private int _prizeTier;
    private int _chestWait;
    private readonly Queue<(Vector2I Direction, int Counter)> _hiddenMoves = new();
    private Vector2I _hiddenDirection;
    private Action? _afterHiddenMove;
    private readonly GashaSpotDatabase _ringTiers = new();
    private ChestTreasureEffect? _chestPrizeVisual;
    private GroundTreasurePickup? _ringReward;
    private OracleRuntimeState ShopMemory => _context.Entities.RuntimeState;
    internal int ChestRound => _round;
    internal int ChestWait => _chestWait;
    internal bool ChestGame => _chestGame;

    public void UpdateDuringDialogueFrame() => UpdateChestPrizeVisual();

    private void UpdateChestPrizeVisual()
    {
        if (_chestPrizeVisual is null) return;
        if (!_chestPrizeVisual.Finished)
        {
            _chestPrizeVisual.Advance(1.0 / 60);
            if (_chestPrizeVisual.Finished)
                _context.Sound.PlaySound(OracleSoundEngine.SndGetItem);
        }
        if (_chestPrizeVisual.Finished && !_context.DialogueOpen)
            ClearChestPrizeVisual();
    }

    private void ClearChestPrizeVisual()
    {
        if (_chestPrizeVisual is null) return;
        _chestPrizeVisual.GetParent()?.RemoveChild(_chestPrizeVisual);
        _chestPrizeVisual.QueueFree();
        _chestPrizeVisual = null;
    }

    public bool Matches(int group, OracleRoomData room) =>
        group == 2 && (room.Id == _normalDatabase.Room || room.Id == _hiddenDatabase.Room);

    public void Start(OracleRoomData room)
    {
        // shopkeeperState0 samples this once, before any purchase in this visit.
        _chestGame = _database.Hidden &&
            (_context.Rooms.SaveData.ReadWramByte(_database.BoughtItems1Address) & 0x0f) == 0x0f;
        ShopMemory.SetWramByte(0xcca2, 0x80);
    }

    private void MarkHiddenPurchase(int subId)
    {
        // Each hidden-shop purchase script sets the same sold bit that its
        // shopItemReplacementTable row tests on the next initialization.
        ItemRecord item = _database.Item(subId);
        int mask = item.ReplacementMask;
        int address = item.ReplacementAddress;
        if (mask == 0 || address is not (0xc642 or 0xc643))
            throw new InvalidOperationException($"Unsupported hidden-shop $47:${subId:x2} purchase flag.");
        OracleSaveData save = _context.Rooms.SaveData;
        save.WriteWramByte(address, (byte)(save.ReadWramByte(address) | mask));
        save.CommitInventoryChange();
    }

    private void GiveRandomRing(int tier) => _context.Inventory.GiveUnappraisedRing(
        _ringTiers.SelectRing(tier, _context.Entities.NextRandomValue()));

    private void CancelChestGame()
    {
        _hiddenMoves.Clear();
        _afterHiddenMove = null;
        ClearChestPrizeVisual();
        if (_ringReward is not null)
        {
            _ringReward.Finish(_context.Player);
            _context.Player.CancelGetItemState();
        }
        _ringReward = null;
        _chestGame = false;
        _round = _prizeTier = _chestWait = 0;
    }

    private void BeginHiddenTheft() => MoveHidden(
        () => { ShowText(0x0e07); _stage = LynnaShopEventStage.HiddenTheftText; },
        (Vector2I.Up, 0x10));

    private void MoveHidden(Action completed, params (Vector2I Direction, int Counter)[] moves)
    {
        _hiddenMoves.Clear();
        foreach (var move in moves)
            _hiddenMoves.Enqueue(move);
        _afterHiddenMove = completed;
        NextHiddenMove();
    }

    private void NextHiddenMove()
    {
        if (_hiddenMoves.TryDequeue(out var move))
        {
            _hiddenDirection = move.Direction;
            _counter = move.Counter;
            _shopkeeper!.SetScriptAnimation(_database.Animation(0x46, AnimationForDirection(move.Direction)));
            _stage = LynnaShopEventStage.HiddenMove;
            return;
        }
        Action? completed = _afterHiddenMove;
        _afterHiddenMove = null;
        completed?.Invoke();
    }

    private void BeginChestConversation()
    {
        if (_stage == LynnaShopEventStage.ChestSelection)
        {
            ShowText(0x0e1a);
            _stage = LynnaShopEventStage.ChestTalk;
            return;
        }
        bool introduced = (_context.Rooms.SaveData.ReadWramByte(_database.BoughtItems1Address) & 0x80) != 0;
        ShowChoice(introduced ? 0x0e0e : 0x0e0d, 10);
        _stage = LynnaShopEventStage.ChestPrompt;
    }

    private bool TryInteractShopChest(Player player)
    {
        if (!_database.Hidden || _item is not null)
            return false;
        Vector2 point = player.Position + (Vector2)player.FacingVector * 8;
        OracleRoomData room = _context.Rooms.CurrentRoom;
        if (room.GetMetatile(point) != 0xf1 || player.FacingVector != Vector2I.Up)
            return false;
        // nextToChestTile: wcca2=$80 locks both chests before a game; any
        // nonzero position locks the second chest until state5 consumes it.
        if (ShopMemory.ReadWramByte(0xcca2) != 0)
            return true;
        ShopMemory.SetWramByte(0xcca2, (byte)room.GetPackedPosition(point));
        room.ReplaceMetatile(point, 0xf1, 0xf0, _context.AnimationTick());
        _context.RoomView.QueueRedraw();
        _context.Sound.PlaySound(OracleSoundEngine.SndOpenChest);
        return true;
    }

    private void CloseShopChest()
    {
        int position = ShopMemory.ReadWramByte(0xcca2);
        if ((position & 0x80) != 0)
            return;
        Vector2 point = new((position & 15) * 16 + 8, (position >> 4) * 16 + 8);
        _context.Rooms.CurrentRoom.SetPositionTileAndCollision(point, 0xf1, null, _context.AnimationTick());
        _context.RoomView.QueueRedraw();
    }

    private void BeginChestPreparation()
    {
        _shopkeeper!.SetScriptButtonSensitive(true);
        _shopkeeper.SetScriptAnimation(_database.Animation(0x46, 1));
        if (_correctChest == 0)
            CloseShopChest();
        _chestWait = 60;
        _stage = LynnaShopEventStage.ChestPrepareRight;
    }

    private void SelectNextChest()
    {
        // State5/substate0: exactly one shared RNG call, followed by closing
        // the previous chest and clearing the shared tile handshake.
        _correctChest = _context.Entities.NextRandomValue() & 1;
        CloseShopChest();
        ShopMemory.SetWramByte(0xcca2, 0);
        _prizeTier = 0;
        _shopkeeper!.SetScriptButtonSensitive(true);
        _stage = LynnaShopEventStage.ChestSelection;
    }

    private void ReturnFromChestGame(Action completed) => MoveHidden(completed,
        (Vector2I.Up, 0x08), (Vector2I.Left, 0x11), (Vector2I.Down, 0x1a),
        (Vector2I.Left, 0x19), (Vector2I.Down, 0x08));

    private void FinishChestGame()
    {
        if (_prizeTier == 0)
        {
            FinishShopkeeperTalk();
            return;
        }
        int ring = _ringTiers.SelectRing(_prizeTier, _context.Entities.NextRandomValue());
        _prizeTier = 0;
        // shopkeeperState4 calls giveRingToLink with c=$00. Its $60:$2d
        // treasure owns the held graphic, grab pose, sound and text lifecycle.
        if (!_context.Entities.InteractionSlotAvailable)
        {
            FinishShopkeeperTalk();
            return;
        }
        _ringReward = _context.Entities.GrantGroundTreasure(new GroundTreasureGrantRequest(
            _database.Group, _database.Room, 0,
            (int)_context.Player.Position.Y, (int)_context.Player.Position.X,
            "TREASURE_OBJECT_RING_00", "shopkeeper.s:shopkeeperState4/giveRingToLink")
        {
            SpawnMode = 0,
            GrabMode = 1,
            InventoryWrite = GroundTreasureInventoryWrite.UnappraisedRing,
            InventoryParameter = ring,
            RoomFlagTiming = GroundTreasureRoomFlagTiming.Never,
            DialogueTiming = GroundTreasureDialogueTiming.AfterGrab,
            CompletionOwner = GroundTreasureCompletionOwner.Caller,
            TextboxPosition = _database.TextboxPosition
        }, _context.Player);
        _stage = LynnaShopEventStage.ChestRingText;
    }

    private bool UpdateHiddenShop()
    {
        switch (_stage)
        {
            case LynnaShopEventStage.HiddenMove:
                // interactionRunScript decrements counter2, skips motion on
                // its zero update, and resumes commands on the following tick.
                if (_counter == 0) NextHiddenMove();
                else if (--_counter != 0)
                    _shopkeeper!.SetStatePosition(_shopkeeper.Position + (Vector2)_hiddenDirection * 2);
                return true;
            case LynnaShopEventStage.HiddenTheftText:
                if (DialogueClosed())
                    MoveHidden(FinishTheftPrevention, (Vector2I.Down, 0x10));
                return true;
            case LynnaShopEventStage.ChestPrompt:
                if (!DialogueClosed()) return true;
                SetBoughtItems1Mask(0x80);
                if (TakeChoice() != 0)
                {
                    ShowText(0x0e11);
                    _stage = LynnaShopEventStage.ChestDeclined;
                }
                else if (_context.Inventory.Rupees < 10)
                {
                    ShowText(0x0e06);
                    _stage = LynnaShopEventStage.ChestDeclined;
                }
                else
                {
                    _context.Inventory.AddRupees(-10);
                    _round = 0;
                    _shopkeeper!.SetCollisionRadii(6, 6);
                    MoveHidden(BeginChestPreparation,
                        (Vector2I.Up, 8), (Vector2I.Right, 0x19), (Vector2I.Up, 0x1a),
                        (Vector2I.Right, 0x11), (Vector2I.Down, 8));
                }
                return true;
            case LynnaShopEventStage.ChestDeclined:
                if (DialogueClosed()) FinishShopkeeperTalk();
                return true;
            case LynnaShopEventStage.ChestRingText:
                if (DialogueClosed())
                {
                    _ringReward?.Finish(_context.Player);
                    _ringReward = null;
                    FinishShopkeeperTalk();
                }
                return true;
            case LynnaShopEventStage.ChestPrepareRight:
                if (--_chestWait == 0)
                {
                    _shopkeeper!.SetScriptAnimation(_database.Animation(0x46, 3));
                    if (_correctChest == 1) CloseShopChest();
                    _chestWait = 60;
                    _stage = LynnaShopEventStage.ChestPrepareLeft;
                }
                return true;
            case LynnaShopEventStage.ChestPrepareLeft:
                if (--_chestWait == 0)
                {
                    _shopkeeper!.SetScriptAnimation(_database.Animation(0x46, 2));
                    ShowText(_round == 0 ? 0x0e10 : 0x0e18);
                    _stage = LynnaShopEventStage.ChestInstructions;
                }
                return true;
            case LynnaShopEventStage.ChestInstructions:
                if (DialogueClosed()) SelectNextChest();
                return true;
            case LynnaShopEventStage.ChestTalk:
                if (DialogueClosed())
                {
                    _shopkeeper!.SetScriptButtonSensitive(true);
                    _stage = LynnaShopEventStage.ChestSelection;
                }
                return true;
            case LynnaShopEventStage.ChestSelection:
                int opened = ShopMemory.ReadWramByte(0xcca2);
                if (opened == 0) return true;
                _shopkeeper ??= _context.RequireNpc(2, 0x7e, 0x46, 1, "hidden shopkeeper");
                if ((opened & 15) != (_correctChest == 0 ? 7 : 5))
                {
                    _round = 0;
                    ShowChoice(0x0e17, 10);
                    _stage = LynnaShopEventStage.ChestWrong;
                }
                else
                {
                    _round++;
                    _chestPrizeVisual = new ChestTreasureEffect { ZIndex = 12 };
                    _chestPrizeVisual.Initialize(new Vector2(_correctChest == 0 ? 0x78 : 0x58, 0x20),
                        _context.Treasures.GetObjectVisual(_context.Treasures.GetObject("TREASURE_OBJECT_RUPEES_08").Graphic));
                    _context.RoomView.AddChild(_chestPrizeVisual);
                    if (_round is 3 or 4) ShowChoice(_round == 3 ? 0x0e12 : 0x0e15, 10);
                    else ShowText(_round == 5 ? 0x0e16 : 0x0e13);
                    _stage = LynnaShopEventStage.ChestCorrect;
                }
                return true;
            case LynnaShopEventStage.ChestWrong:
                if (!DialogueClosed()) return true;
                if (TakeChoice() == 0)
                {
                    if (_context.Inventory.Rupees >= 10)
                    {
                        _context.Inventory.AddRupees(-10);
                        BeginChestPreparation();
                    }
                    else ReturnFromChestGame(() =>
                    {
                        ShowText(0x0e06);
                        _stage = LynnaShopEventStage.ChestDeclined;
                    });
                }
                else ReturnFromChestGame(FinishChestGame);
                return true;
            case LynnaShopEventStage.ChestCorrect:
                if (!DialogueClosed()) return true;
                bool stop = _round == 5 || (_round is 3 or 4 && TakeChoice() != 0);
                if (!stop) BeginChestPreparation();
                else
                {
                    _prizeTier = 6 - _round;
                    if (_round == 5) ReturnFromChestGame(FinishChestGame);
                    else { ShowText(0x0e14); _stage = LynnaShopEventStage.ChestPrize; }
                }
                return true;
            case LynnaShopEventStage.ChestPrize:
                if (DialogueClosed()) ReturnFromChestGame(FinishChestGame);
                return true;
            default:
                return false;
        }
    }
}
