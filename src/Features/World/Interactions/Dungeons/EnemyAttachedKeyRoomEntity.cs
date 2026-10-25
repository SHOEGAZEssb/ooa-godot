using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// smallKeyOnEnemy.s, including the clean-US @nextEnemy carry bug. The
// related pointer remains the native slot, so a reused slot is followed.
internal sealed partial class EnemyAttachedKeyRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly Func<bool> _itemCollected;
    private readonly Func<NativeEnemyAttachment?> _enemySlotZero;
    private readonly Func<Vector2, bool> _createKey;
    private int _z, _speedZ;
    internal int State { get; private set; }
    internal int ZFixed => _z;
    internal int SpeedZ => _speedZ;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;

    internal EnemyAttachedKeyRoomEntity(DungeonObjectRecord record, DungeonInteractionVisual visual,
        Func<bool> itemCollected, Func<NativeEnemyAttachment?> enemySlotZero, Func<Vector2, bool> createKey)
    {
        _record = record; _itemCollected = itemCollected; _enemySlotZero = enemySlotZero; _createKey = createKey;
        InitializeVisual(visual, record.Position); Visible = false;
        ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0)
        {
            InitializeAttachment(); return;
        }
        if (State == 1)
        {
            if (_enemySlotZero() is not NativeEnemyAttachment enemy) { State = 2; return; }
            CopyPosition(enemy); Visible = (frame.Counter & 1) != 0; QueueRedraw(); return;
        }
        if (State != 2) throw new InvalidOperationException($"{_record.Source}: INTERAC$77 state${State:x2} is not represented.");
        Visible = true;
        if (!OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x20)) { QueueRedraw(); return; }
        if (!_createKey(Position))
            throw new NotSupportedException($"{_record.Source}: INTERAC$77 createTreasure failure falls through objectCopyPosition with an invalid allocation; this source overflow is not represented.");
        Finished = true; Visible = false;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // bank0.updateInteractions admits state $00 under wScrollMode=$08.
        // smallKeyOnEnemy.s initializes visibility and XYZ, then state $01
        // remains frozen; preload must not run its flicker or falling motion.
        if (!Finished && State == 0) InitializeAttachment();
        return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    private void InitializeAttachment()
    {
        if (_itemCollected() || _enemySlotZero() is not NativeEnemyAttachment enemy || enemy.Id != _record.SubId)
        { Finished = true; return; }
        State = 1; Visible = true; CopyPosition(enemy);
    }
    private void CopyPosition(NativeEnemyAttachment enemy)
    {
        // objectTakePosition writes only the high bytes, unlike objectCopyPosition.
        Position = enemy.Position.Floor(); _z = (enemy.ZHigh << 8) | (_z & 0xff); QueueRedraw();
    }
    public override void _Draw()
    {
        if (Visible && !Finished)
            DrawTexture(CurrentTexture, CurrentDrawOffset + new Vector2(0, _z >> 8) + TransitionDrawOffset);
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}

internal readonly record struct NativeEnemyAttachment(int Id, Vector2 Position, int ZHigh);
