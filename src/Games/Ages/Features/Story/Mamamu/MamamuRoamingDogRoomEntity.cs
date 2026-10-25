using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// mamamuDog.s:dog_subid01. All four placed variants use the source's map0
// pointer because interactionSetScript leaves A=0 before dog_setTargetPositionIndex.
internal sealed class MamamuRoamingDogRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomEntityLifetime, INativeBraceletRoomEntity,
    IBraceletChildRoomEntity
{
    private readonly NpcCharacter _actor;
    private readonly MamamuDogDatabase _data = new();
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<byte> _random;
    private readonly Action<int,string,Vector2> _showText;
    private readonly Action<int> _sound;
    private readonly Action<Warp> _warp;
    private readonly Func<bool> _textActive;
    private readonly BraceletGrabGeometry _grab = new();
    private Action<INativeBraceletRoomEntity>? _publish;
    private OracleObjectPosition _position;
    private Player? _holder;
    private int _state, _substate, _waypoint, _direction = 0xff, _animation;
    private int _z, _speedZ, _counter;
    private bool _initialized;
    public bool Finished => !_actor.Active;
    public bool ReservedBraceletChildActive => false;
    internal int State => _state;
    internal int Waypoint => _waypoint;
    internal int ZFixed => _z;
    internal int Counter => _counter;
    internal OracleObjectPosition PositionFixed => _position;

    internal MamamuRoamingDogRoomEntity(NpcCharacter actor, OracleSaveData save,
        OracleRuntimeState runtime, Func<byte> random,
        Action<int,string,Vector2> showText, Action<int> sound, Action<Warp> warp,
        Func<bool> textActive) : base(actor, actor.SetTransitionDrawOffset)
    {
        _actor=actor; _save=save; _runtime=runtime; _random=random;
        _showText=showText; _sound=sound; _warp=warp; _textActive=textActive;
        _position=OracleObjectPosition.FromPixels(actor.Position);
        actor.SetScriptVisible(false);
        actor.SetAnimationRate(0);
        actor.SetBlocksLink(false);
    }
    public void BindGrabbablePublisher(Action<INativeBraceletRoomEntity> publish) => _publish=publish;
    public void WriteReservedBraceletAngle(int angle) =>
        throw new InvalidOperationException("INTERAC_MAMAMU_DOG $54:$01 has no reserved Bracelet item.");
    public void UpdateBraceletChild(Player player)
    {
        if (_holder is not null && !_holder.IsCarryingObject)
            _holder=null;
    }
    public void UpdateHeldPosition(Player player)
    {
        if (_holder != player || _state!=2) return;
        Vector2I offset=player.BraceletEntityOffset ?? CarriedObjectMotion.HeldOffset(player);
        _actor.Position=new((byte)((int)player.Position.X+offset.X),
            (byte)((int)player.Position.Y+offset.Y));
    }
    public bool TryUseBracelet(Player player, Vector2I releaseDirection)
    {
        if (_state!=1 || !_actor.Active || player.IsCarryingObject || !player.IsOnGroundForBracelet ||
            player.Inventory.BraceletLevel==0 ||
            !_grab.Overlaps(new(player.EnemyContactPosition-new Vector2(6,6),new(12,12)),
                player.EnemyContactZ,CarriedObjectMotion.DirectionIndex(player.FacingVector),
                _actor.ObjectCollisionBounds,_z>>8,false)) return false;
        _holder=player; player.BeginCarriedObjectPose(); _state=2; _substate=0;
        return true;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_actor.Active) return;
        if (!_initialized) { Initialize(); if (!_actor.Active) return; }
        if (_state==1)
        {
            MoveTowardWaypoint();
            if (CloseToWaypoint() && NextWaypoint()) return;
            MoveTowardWaypoint();
            int direction=((_animationAngle>>4)&1)^1;
            if (direction!=_direction)
            { _direction=direction; _animation=direction+2; _actor.SetScriptAnimation(_data.Animations[_animation]); }
            if (CloseToWaypoint() && NextWaypoint()) return;
            if (!_actor.Active) return;
            if (OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)) _speedZ=-0xc0;
            _actor.SetScriptDrawOffset(new(0,_z>>8));
            _actor.AnimateAndUpdateDrawPriorityOneUpdate(frame.Player);
            _publish?.Invoke(this);
            return;
        }
        if (_state!=2) return;
        if (_substate==0)
        {
            _save.SetGlobalFlag(AgesGlobalFlag.ReturnedDog);
            _runtime.SetWramByte(AgesWramAddress.wMenuDisabled,0x81);
            _runtime.SetWramByte(AgesWramAddress.wDisableScreenTransitions,0x81);
            _actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);
            _substate=1;
        }
        if (_substate==1 && _holder==frame.Player && frame.Player.BraceletEntityOffset is null &&
            frame.Player.IsCarryingObject)
        {
            _runtime.SetWramByte(AgesWramAddress.wDisabledObjects,0x81);
            _runtime.SetWramByte(AgesWramAddress.wMenuDisabled,0x80);
            _counter=40; _substate=2;
        }
        if (_substate==2 && --_counter==0)
        {
            _substate=3;
            _showText(0x007f,_data.RoamingText,frame.Player.Position);
        }
        else if (_substate==3 && !_textActive())
        {
            _substate=4;
            _warp(new(_actor.Record.Group,_actor.Record.Room,-1,0,0,2,0xe7,0x25,0,
                WarpDestinationTransition.Basic,NativeFadeDelay:4));
            _sound(SoundId.SndTeleport);
        }
    }
    private int _animationAngle;
    private void Initialize()
    {
        _initialized=true;
        if (!new NpcVisibilityRuleDatabase().ShouldShow(_actor.Record,_save,_runtime))
        { _actor.SetActive(false); return; }
        _actor.InitializeCollisionRadii();
        _actor.SetScriptVisible(true);
        _state=1;
        int old=_runtime.ReadWramByte(AgesGameContent.MamamuDogLocationAddress);
        byte next;
        do { next=(byte)(_random()&3); } while (next==old);
        _runtime.SetWramByte(AgesGameContent.MamamuDogLocationAddress,next);
    }
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Initialize();
        return _actor.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    private void MoveTowardWaypoint()
    {
        int[] points=_data.RoamingMap0;
        Vector2 target=new(points[_waypoint*2+1],points[_waypoint*2]);
        _animationAngle=OracleObjectMovement.Shared.RelativeAngle(_position.PixelPosition,target);
        _position=NativeObjectMovement.ApplySpeed(_runtime,_position,0x14,_animationAngle);
        _actor.Position=_position.PixelPosition;
    }
    private bool CloseToWaypoint()
    {
        int[] points=_data.RoamingMap0;
        return unchecked((byte)(points[_waypoint*2]-(_position.YFixed>>8)+1))<5 &&
            unchecked((byte)(points[_waypoint*2+1]-(_position.XFixed>>8)+1))<5;
    }
    private bool NextWaypoint()
    {
        int[] points=_data.RoamingMap0;
        _position=OracleObjectPosition.FromPixels(new(points[_waypoint*2+1],points[_waypoint*2]));
        _actor.Position=_position.PixelPosition;
        if (++_waypoint<=6) return false;
        _waypoint=0; _actor.SetActive(false); return true;
    }
}
