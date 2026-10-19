using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_PIRATE_SHIP $c2; subid $00 occupies w1ReservedInteraction1 ($d140).</summary>
internal sealed class PirateShipRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IPlayerRestriction, IRoomEntityUpdateFreeze, IScreenTransitionPreloadRoomEntity, IDisposable
{
    // object_code/ages/interactions/pirateShip.s native operands.
    internal const byte DepartureCompletedRoomFlag = 0x40;
    private const int CollisionRadius = 6;
    private const int DepartureWaitUpdates = 60;
    private const int DepartureMovementUpdates = 0x80;
    private const int DepartureSpeed = 0x28; // SPEED_100
    private readonly RoomEntityManager _owner;
    private readonly OracleSaveData _save;
    private readonly PirateShipDatabase _data;
    private readonly int _subid, _group, _room;
    private Player? _controlledPlayer;
    private int _state, _counter, _direction = -1;
    private Vector2 _position;
    internal bool Reserved => _subid == 0;
    internal int State => _state;
    internal int Counter => _counter;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => _state == 0;
    public bool FreezesRoomEntities => _controlledPlayer is not null && !Finished;
    public bool FreezesInteractions => false;
    public bool DisablesSword => FreezesRoomEntities;
    public bool DisablesItems => FreezesRoomEntities;
    public bool FreezesPlayerUpdates => FreezesRoomEntities;
    public bool DisablesMenus => FreezesRoomEntities;
    public bool DisablesScreenTransitions => FreezesRoomEntities;
    public bool DisablesWarpTiles => FreezesRoomEntities;

    internal PirateShipRoomEntity(RoomEntityManager owner, NpcRecord record)
        : this(owner, NpcCharacter.CreateFromRecord(record)) { }
    private PirateShipRoomEntity(RoomEntityManager owner, NpcCharacter actor)
        : base(actor, actor.SetTransitionDrawOffset)
    {
        _owner = owner; _save = owner.SaveData!; _data = PirateShipDatabase.Shared;
        _subid = actor.Record.SubId; _group = actor.Record.Group; _room = actor.Record.Room;
        _position = actor.Position;
        actor.SetBlocksLink(false); actor.TimeWarpSolidMarked = false;
        actor.SetScriptVisible(false);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        var player = frame.Player;
        if (_state == 0)
        {
            Initialize(player);
            return;
        }
        if (_subid == 0)
        {
            if (_state == 1)
            {
                if (_save.ReadWramByte(WramAddress.wPirateShipRoom) != _room)
                { Finished = true; return; }
                int y = _save.ReadWramByte(WramAddress.wPirateShipY), x = _save.ReadWramByte(WramAddress.wPirateShipX);
                Entity.SetStatePosition(new(x >= 0xf8 ? x - 256 : x, y >= 0xf8 ? y - 256 : y));
                SetDirection(_save.ReadWramByte(WramAddress.wPirateShipAngle) & 3);
                // objectCheckCollidedWithLink_notDead then checkLinkVulnerable:
                // ordinary Feather Z does not reject this collision.
                Vector2 delta = player.Position.Floor() - Entity.Position;
                if (player.NativeObjectVulnerable && Math.Abs(delta.X) < CollisionRadius + NpcCharacter.LinkCollisionRadius &&
                    Math.Abs(delta.Y) < CollisionRadius + NpcCharacter.LinkCollisionRadius)
                {
                    _owner.OnRoomWarpRequested(_data.Warp("board", _group, _room));
                    _state = 2;
                }
            }
            Entity.AdvanceAnimationUpdates(1);
            return;
        }
        if (_state == 1)
        {
            Entity.AdvanceAnimationUpdates(1);
            if (--_counter != 0) return;
            _counter = DepartureMovementUpdates;
            _owner.OnRoomEntityDialogueRequested(_data.DepartureTextId, _data.DepartureText, player.Position);
            _state = 2;
            return;
        }
        Vector2 position = NativeObjectMovement.ApplySpeed(_owner.RuntimeState, ref _position, DepartureSpeed,
            _subid == 1 ? ObjectAngle.Left : ObjectAngle.Up);
        // Source high bytes wrap through $ff while OAM remains partly visible
        // above/left of the viewport. Retain native motion in _position.
        Entity.SetStatePosition(new(position.X >= 0xe0 ? position.X - 256 : position.X,
            position.Y >= 0xe0 ? position.Y - 256 : position.Y));
        Entity.AdvanceAnimationUpdates(1);
        if (--_counter != 0) return;
        _save.SetRoomFlag(_group, _room, DepartureCompletedRoomFlag);
        Finished = true;
        Dispose();
    }

    private void Initialize(Player? player)
    {
        if (_state != 0 || Finished) return;
        if (_subid != 0 && (_save.IsLinkedGame != (_subid == 2) ||
            !_save.HasGlobalFlag(GlobalFlag.PiratesGone) || _save.HasRoomFlag(_group, _room, DepartureCompletedRoomFlag)))
        { Finished = true; return; }
        SetDirection(_subid == 0 ? _save.ReadWramByte(WramAddress.wPirateShipAngle) & 3 : _subid == 1 ? 3 : 0);
        Entity.SetCollisionRadii(CollisionRadius, CollisionRadius);
        Entity.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        Entity.SetScriptVisible(true);
        if (_subid != 0)
        {
            if (player is null) throw new InvalidOperationException("pirateShip.s:@subid1And2State0Common requires live Link during preload.");
            player.Face(_subid == 1 ? Vector2I.Up : Vector2I.Right);
            player.BeginCutsceneControl(interruptBracelet: false, owner: this);
            _controlledPlayer = player; _counter = DepartureWaitUpdates;
        }
        _state = 1;
    }

    ScreenTransitionPresentation IScreenTransitionPreloadRoomEntity.PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) => PreparePreload(null);
    ScreenTransitionPresentation IScreenTransitionPreloadRoomEntity.PrepareForScreenTransition(
        Player? player, ICollection<RoomEntitySpawn> spawns) => PreparePreload(player);
    private ScreenTransitionPresentation PreparePreload(Player? player)
    {
        Initialize(player);
        return !Finished && Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    private void SetDirection(int direction)
    {
        if (_direction == direction) return;
        _direction = direction;
        var visual = _data.Ship(_group, _room, _subid, 0, 0);
        Entity.SetScriptAnimation(direction switch {
            0 => visual.UpAnimation, 1 => visual.RightAnimation, 2 => visual.DownAnimation, _ => visual.LeftAnimation });
    }
    public void Dispose()
    {
        _controlledPlayer?.EndCutsceneControl(this);
        _controlledPlayer = null;
    }
}
