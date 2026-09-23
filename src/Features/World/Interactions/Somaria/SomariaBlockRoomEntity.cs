using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SomariaBlockRoomEntity : IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, INativeBraceletRoomEntity
{
    private readonly OracleRoomData _room;
    private readonly int _group;
    private readonly Func<long> _tick;
    private readonly BraceletGrabGeometry _grab = new();
    private Action<INativeBraceletRoomEntity>? _publish;
    private Player? _holder;
    private Player _player;
    internal SomariaBlock Block { get; }
    public Node2D Node => Block;
    public bool Finished => Block.Finished;

    internal SomariaBlockRoomEntity(OracleRoomData room, int group, Player player,
        Vector2 position, int z, Func<long> tick, Action<int> sound,
        Action<Vector2,int> puff, Action<int,Vector2,int> hazard)
    {
        _room=room; _group=group; _player=player; _tick=tick;
        Block=new(room,position,z,new(),new(),new(),new(sound,
            _=>PreventLinkPassing(), _=>_publish?.Invoke(this), puff,
            _=>EndHold(), hazard));
        Block.TreeExiting+=EndHold;
    }

    public void SetTransitionDrawOffset(Vector2 offset) => Block.SetTransitionDrawOffset(offset);
    public void BindGrabbablePublisher(Action<INativeBraceletRoomEntity> publish) => _publish=publish;
    internal void ClearPhysicalItem()
    {
        if (_holder is not null)
        {
            // clearAllParentItems precedes dropLinkHeldItem; the latter
            // writes substate3/angle$ff before ITEM$18 is hidden and marked.
            _holder.ClearNativeItemParents();
            if (Block.IsHeld) Block.Release(0xff,dropped:true);
            EndHold();
        }
        Block.ClearPhysicalItem();
    }
    private void EndHold()
    {
        _holder?.EndCarriedObjectPose();
        _holder=null;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _player=frame.Player;
        if (_holder is not null && !_holder.IsCarryingObject && Block.IsHeld)
        {
            Block.Release(0xff, dropped:true);
            EndHold();
        }
        Block.Update(_group,_room.GetPackedPosition(_player.Position),
            _player.Inventory.BraceletLevel,_tick(),CarriedObjectMotion.DirectionIndex(_player.FacingVector),
            RingEffects.UsesStrongThrow(_player.Inventory));
    }

    public bool TryUseBracelet(Player player, Vector2I releaseDirection)
    {
        if (_holder==player)
        {
            Block.Release(releaseDirection==Vector2I.Zero ? 0xff : CarriedObjectMotion.DirectionIndex(releaseDirection)*8);
            EndHold();
            return true;
        }
        if (Finished || Block.State!=3 || player.IsCarryingObject || !player.IsOnGroundForBracelet) return false;
        Vector2 radius=Block.Radius;
        if (!_grab.Overlaps(new(player.EnemyContactPosition-new Vector2(6,6),new(12,12)),
            player.EnemyContactZ,CarriedObjectMotion.DirectionIndex(player.FacingVector),
            new(Block.Position-radius,radius*2),Block.ZHigh,false)) return false;
        Block.BeginPickup(); _holder=player; player.BeginCarriedObjectPose();
        return true;
    }

    public void UpdateHeldPosition(Player player)
    {
        if (_holder!=player || !Block.IsHeld) return;
        if (player.BraceletEntityOffset is Vector2I offset)
            Block.CopyHeldOffset(player.Position,player.EnemyContactZ,offset);
        else
            Block.CopyHeldPosition(player.Position,player.EnemyContactZ,
                player.CarriedObjectAnimationFrame==0 ? 2 : 3,CarriedObjectMotion.DirectionIndex(player.FacingVector));
    }

    private void PreventLinkPassing()
    {
        // preventObjectHFromPassingObjectD: raw high-byte XY and Link's 6x6
        // radii; no NPC-pass, airborne, collision-enable or input-mask gate.
        Vector2 link=_player.Position.Floor(), obstacle=Block.Position.Floor();
        int rx=Block.Radius.X+6, ry=Block.Radius.Y+6;
        if ((byte)((int)link.X-(int)obstacle.X+rx)>=rx*2 ||
            (byte)((int)link.Y-(int)obstacle.Y+ry)>=ry*2) return;
        int ox=(byte)(rx-Math.Abs((int)obstacle.X-(int)link.X));
        int oy=(byte)(ry-Math.Abs((int)obstacle.Y-(int)link.Y));
        bool horizontal=oy>=ox;
        int target=(int)(horizontal?obstacle.X:obstacle.Y);
        int current=(int)(horizontal?link.X:link.Y);
        _player.SetScriptedCoordinateHigh(horizontal,(byte)(target+(target<current?1:-1)*(horizontal?rx:ry)));
    }
}
