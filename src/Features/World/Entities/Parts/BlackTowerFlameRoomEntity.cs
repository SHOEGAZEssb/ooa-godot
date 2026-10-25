using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// PART_BLACK_TOWER_MOVING_FLAMES $59. Collision is enabled by partData,
// including the invisible ignition delay; partCode59 ignores hit/death status.
internal sealed partial class BlackTowerFlameRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IPostObjectLinkContactRoomEntity,
    IObjectCollisionHeightRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, INativePartHealthRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly BlackTowerFlameDatabase _data;
    private bool _collision = true;
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int RouteIndex { get; private set; }
    internal int Angle { get; private set; }
    internal Vector2I Target { get; private set; }
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public int CollisionZ => 0;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    internal BlackTowerFlameRoomEntity(RoomEntityManager owner, BlackTowerFlameDatabase data, int subid, Vector2 position)
    {
        if ((uint)subid >= 4) throw new NotSupportedException($"blackTowerMovingFlames.s: unsupported PART $59:${subid:x2}.");
        _owner = owner; _data = data; SubId = subid;
        InitializeVisual(data.Visual, position, positionedOam: true);
        Visible = false; ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (State)
        {
            case 0: State = 1; Counter = _data.Delay(SubId); return;
            case 1:
                if (!Elapsed()) return;
                State = 2; Counter = _data.Delay(SubId ^ 3); Visible = true;
                _owner.OnSoundRequested(SoundId.SndLightTorch); return;
            case 2: if (Elapsed()) { State = 3; Counter = 20; } break;
            case 3:
                if (!Elapsed()) break;
                int packed = _data.Target(SubId, RouteIndex);
                Target = new((packed & 15) * 16 + 8, (packed & 0xf0) + 8);
                State = packed == 0 ? 5 : 4;
                if (State == 5) Counter = 16;
                break;
            case 4:
                if (Position.Floor() == Target)
                { State = 3; Counter = 16; RouteIndex = (RouteIndex + 1) & 255; }
                else
                {
                    Angle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), Target);
                    Position = NativeObjectMovement.ApplySpeed(_owner.RuntimeState, OracleObjectPosition.FromPixels(Position), 0x14, Angle).PrecisePosition;
                }
                break;
            case 5:
                if (Elapsed()) { _owner.TryCreateVirePuff(Position.Floor(), 0, 0); Finished = true; Visible = false; return; }
                break;
            default: throw new NotSupportedException($"blackTowerMovingFlames.s: PART $59:${SubId:x2} state ${State:x2}.");
        }
        AdvanceAnimation(); QueueRedraw();
    }
    private bool Elapsed() { if (Counter != 0) Counter--; return Counter == 0; }
    public void HandleLinkContact(Player player)
    {
        if (State == 0 || Finished || !_collision || !player.EnemyContactHeightOverlaps(0)) return;
        var bounds = new Rect2(Position - Vector2.One * _data.Radius, Vector2.One * _data.Radius * 2);
        if (player.OverlapsEnemyCollision(bounds, 0)) player.ApplyEnemyContactDamage(Position, _data.Damage, RingDamageSource.Generic, 34, 15);
    }
    public void ClearHealthAndCollision() => _collision = false;
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) { State = 1; Counter = _data.Delay(SubId); }
        return ScreenTransitionPresentation.Hidden;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
