using Godot;
using System;
using System.Linq;

namespace oracleofages;

// common/interactions/syrup.s, syrupCucco.s and shopItem.s. Keep the placed
// Syrup/cucco slots ahead of the shop items allocated by Syrup's entry script.
internal sealed class SyrupShopEvent(RoomEventContext context)
    : IRoomEntryEvent, IUpdatesDuringDialogueRoomEvent
{
    private readonly LynnaShopDatabase _data = new(syrup: true);
    private NpcCharacter? _syrup;
    private NpcCharacter? _cucco;
    private LynnaShopItem? _held;
    private SyrupShopStage _stage;
    private int _cuccoState = 1;
    private int _direction = -1;
    private int _z;
    private int _speedZ;
    private bool _buy;
    private bool _enoughRupees;
    private bool _cannotBuy;
    private bool _theftTextPending;
    private RoomEventResources? _resources;
    private int C(string name) => _data.Constant("syrup-" + name);

    public bool HasState => _syrup is not null;
    public bool BlocksGameplay => HasState && (_cuccoState != 1 || _stage != SyrupShopStage.Idle);
    public bool FreezesNonInteractionObjects => BlocksGameplay;
    public bool MenusDisabled => BlocksGameplay;
    internal SyrupShopStage Stage => _stage;
    internal int CuccoState => _cuccoState;
    internal int CuccoZ => _z;
    internal int CuccoSpeedZ => _speedZ;
    public bool Matches(int group, OracleRoomData room) => group == _data.Group && room.Id == _data.Room;

    public void Start(OracleRoomData room)
    {
        _syrup = context.RequireNpc(_data.Group, room.Id, 0x5f, 0x80, "Syrup");
        _cucco = context.RequireNpc(_data.Group, room.Id, 0xc9, 0x80, "Syrup's cucco");
        _stage = SyrupShopStage.Idle;
        _cuccoState = 1;
        _theftTextPending = false;
        _direction = -1;
        _z = 0;
        _speedZ = C("cucco-hop-speed");
        // interaction5f's upper OAM cells start at Y=$f8. The native
        // animation needs positioned bounds; the fixed NPC canvas clips them.
        _syrup.SetScriptAnimation(_data.Animation(0x5f, 0));
        _cucco.SetScriptAnimation(_data.Animation(0xc9, 0));
        _cucco.ZIndex = NpcCharacter.InFrontOfLinkZIndex;
    }

    public bool TryInteractPlayer(Player player)
    {
        if (!HasState || !Matches(context.Rooms.ActiveGroup, context.Rooms.CurrentRoom)) return false;
        if (BlocksGameplay) return true;
        if (_held is not null)
        {
            // shopItemCheckGrabbed: lower X is exclusive, upper X inclusive.
            int x = (int)player.Position.X;
            if (player.FacingVector == Vector2I.Up && (int)player.Position.Y < _data.SelectionLinkYLimit &&
                x > _held.ShelfPosition.X - _data.SelectionXRadius &&
                x <= _held.ShelfPosition.X + _data.SelectionXRadius)
                ReturnItem();
            return true;
        }
        foreach (LynnaShopItem item in context.Entities.Entities<LynnaShopItem>().OrderBy(i => i.Order))
        {
            if (!item.CanPickup(player, _data.ItemCollisionRadius, _data.LinkCollisionRadius,
                _data.GrabNegativePointOffset, _data.GrabPositivePointOffset)) continue;
            _held = item;
            item.Pickup(player);
            return true;
        }
        return false;
    }

    public bool TryInteractNpc(NpcCharacter npc)
    {
        // $c9 is A-sensitive, but state1 never reads pressedAButton.
        if (npc == _cucco) return true;
        if (npc != _syrup || BlocksGameplay) return false;
        if (_held is null)
        {
            Show(context.Entities.Entities<LynnaShopItem>().Any(i => !i.Removed) ? 0x0d00 : 0x0d0b);
            _stage = SyrupShopStage.Talk;
        }
        else
        {
            // These are sampled in state1 before the prompt, in this order.
            _enoughRupees = context.Inventory.Rupees >= _held.Record.Price;
            _cannotBuy = _held.Record.SubId switch
            {
                0x07 or 0x09 => context.Inventory.HasTreasure(TreasureId.Potion),
                0x0b => context.Inventory.Bombchus >= 0x99,
                0x08 or 0x0a => context.Inventory.GashaSeeds >= 0x99,
                _ => throw new InvalidOperationException($"Syrup cannot sell $47:${_held.Record.SubId:x2}.")
            };
            context.ShowChoiceDialogue(_data.Text(_held.Record.PromptTextId), textboxPosition: 2);
            _stage = SyrupShopStage.Prompt;
        }
        return true;
    }

    public void UpdateDuringDialogueFrame()
    {
        // updateInteractions only runs enabled bit7 actors while wTextIsActive
        // is nonzero. Syrup sets that bit; her cucco and stock do not.
        AnimateSyrup();
    }

    public void UpdateFrame()
    {
        if (!HasState) return;
        switch (_stage)
        {
            case SyrupShopStage.Talk:
                _stage = SyrupShopStage.Idle;
                break;
            case SyrupShopStage.Prompt:
                int choice = (_resources ??= new(context, this)).RequireDialogueChoice(
                    "Syrup $5f:$80 prompt closed without a text option.");
                _buy = choice == 0 && _enoughRupees && !_cannotBuy;
                int subid = _held!.Record.SubId;
                // textbox.s @index0d/@index0e/@index11, indexed by $cbad.
                int text = choice != 0 ? 0x0d03 : !_enoughRupees ? 0x0d08 : _cannotBuy
                    ? (subid is 7 or 9 ? 0x0d04 : 0x0d07)
                    : subid is 7 or 9 ? 0x0d02 : subid == 0x0b ? 0x0d0c : 0x0d06;
                // Syrup drops wLinkGrabState as the script ends. The stock's
                // state3/state4 waits for the continuation text to finish.
                _held.BeginPurchase(context.Player);
                Show(text);
                _stage = SyrupShopStage.Response;
                break;
            case SyrupShopStage.Response:
                if (_buy) GiveItem();
                else { ReturnItem(); _stage = SyrupShopStage.Idle; }
                break;
            case SyrupShopStage.ItemText:
                _held!.FinishPurchase(context.Player);
                _held = null;
                _stage = SyrupShopStage.Idle;
                break;
        }
        AnimateSyrup();
        if (!context.DialogueOpen) UpdateCucco();
    }

    private void AnimateSyrup()
    {
        if (_syrup is null) return;
        _syrup.AdvanceAnimationUpdates(1);
        _syrup.PreventPlayerPassing(context.Player);
        _syrup.UpdateDrawPriority(context.Player.Position);
    }

    private void UpdateCucco()
    {
        NpcCharacter cucco = _cucco!;
        if (_cuccoState == 4)
        {
            if (_theftTextPending)
            {
                _theftTextPending = false;
                Show(0x0d09);
                return;
            }
            BeginCuccoMove(3, 1);
        }
        else
        {
            // objectUpdateSpeedZ_paramC adds speed first; gravity only above ground.
            _z = (short)(_z + _speedZ);
            if (_z >= 0) { _z = 0; _speedZ = C("cucco-hop-speed"); }
            else _speedZ = (short)(_speedZ + C("cucco-gravity"));
            int speed = C(_cuccoState == 1 ? "cucco-patrol-speed" : "cucco-chase-speed");
            cucco.SetStatePosition(cucco.Position + new Vector2(_direction * speed / 256f, 0));
            int x = (int)cucco.Position.X;
            if (_cuccoState == 1)
            {
                if (((x - C("cucco-left")) & 0xff) >= C("cucco-width"))
                {
                    _direction = -_direction;
                    cucco.SetScriptAnimation(_data.Animation(0xc9, _direction > 0 ? 1 : 0));
                }
                if ((int)context.Player.Position.Y > _data.TheftLinkY && _held?.Held == true)
                {
                    context.Player.SetScriptedCoordinateHigh(false, _data.TheftLinkY);
                    context.Player.BeginCutsceneControl(owner: this);
                    BeginCuccoMove(2, -1);
                }
            }
            else if (_cuccoState == 2 && ((x - C("cucco-link-distance")) & 0xff) < (int)context.Player.Position.X)
            {
                _cuccoState = 4;
                _theftTextPending = true;
            }
            else if (_cuccoState == 3 && x >= C("cucco-home-x"))
            {
                context.Player.EndCutsceneControl(this);
                BeginCuccoMove(1, -1);
            }
        }
        cucco.SetScriptDrawOffset(new Vector2(0, _z >> 8));
        if (_cuccoState != 4) cucco.AdvanceAnimationUpdates(1);
    }

    private void BeginCuccoMove(int state, int direction)
    {
        _cuccoState = state;
        _direction = direction;
        _z = 0; // Native initializer leaves speedZ untouched.
        _cucco!.SetScriptAnimation(_data.Animation(0xc9, direction > 0 ? 1 : 0));
    }

    private void GiveItem()
    {
        ItemRecord item = _held!.Record;
        context.Inventory.AddRupees(-item.Price);
        var treasure = new TreasureObjectRecord($"SHOP_ITEM_{item.SubId:x2}", item.TreasureId,
            item.SubId, item.Parameter, item.ItemTextId, 0, _data.Text(item.ItemTextId));
        context.Inventory.GiveTreasure(treasure);
        int sound = context.Treasures.GetBehaviour(item.TreasureId).Sound;
        if (sound != SoundId.MusNone) context.Sound.PlaySound(sound);
        _held.BeginPurchase(context.Player);
        _stage = SyrupShopStage.ItemText;
        context.Player.RequestGetItemState(1, () => _stage == SyrupShopStage.ItemText);
        Show(item.ItemTextId);
    }

    private void ReturnItem()
    {
        _held?.ReturnToShelf(context.Player);
        _held = null;
    }

    private void Show(int id) => context.ShowDialogue(_data.Text(id), id == 0x0d09 ? 1 : 2);

    public void Cancel()
    {
        if (_held?.Purchasing == true)
        {
            _held.FinishPurchase(context.Player);
            context.Player.CancelGetItemState();
        }
        ReturnItem();
        context.Player.EndCutsceneControl(this);
        _syrup = null;
        _cucco = null;
        _stage = SyrupShopStage.Idle;
        _cuccoState = 1;
    }
}

internal enum SyrupShopStage { Idle, Talk, Prompt, Response, ItemText }
