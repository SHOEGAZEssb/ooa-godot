using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// PART_GROTTO_CRYSTAL $24. Its parameter is one retained wSwitchState bit;
/// its part-ID collision bitmap accepts sword-family attacks, sword beams,
/// boomerangs, and seeds, but rejects bombs and thrown objects. The part's own
/// room flag selects the already-broken frame on later visits.
/// </summary>
internal sealed partial class MoonlitGrottoCrystalRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, ISwordHittableRoomEntity,
    IItemCollisionHittableRoomEntity, ISeedHittableRoomEntity,
    IObjectCollisionHeightRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly EnemyAnimationPlayer _crystal;
    private readonly EnemyAnimationPlayer _breakEffect;
    private bool _broken;
    private bool _breakActive;
    private int _breakSoundCounter;

    public Node2D Node => this;
    public int CollisionZ => 0;
    internal bool Broken => _broken;
    internal bool BreakEffectActive => _breakActive;
    internal int CrystalAnimation => _crystal.AnimationIndex;
    internal Texture2D CrystalTexture => _crystal.CurrentTexture;
    internal int SwitchMask => _record.SubId;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(
            _data.MoonlitCrystalRadiusX,
            _data.MoonlitCrystalRadiusY),
        new Vector2(
            _data.MoonlitCrystalRadiusX * 2,
            _data.MoonlitCrystalRadiusY * 2));

    internal MoonlitGrottoCrystalRoomEntity(
        DungeonMechanicDatabaseRecord record,
        DungeonMechanicDatabase data,
        DungeonInteractionVisual crystalVisual,
        DungeonInteractionVisual breakVisual,
        OracleRoomData room,
        OracleSaveData? save,
        OracleRuntimeState runtime,
        Func<long> animationTick,
        Action<int> playSound)
    {
        if (record.Id != 0x24 ||
            record.SubId is not (0x10 or 0x20 or 0x40 or 0x80))
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }

        _record = record;
        _data = data;
        _runtime = runtime;
        _playSound = playSound;
        Name = $"GrottoCrystal_{record.Room:x2}_{record.SubId:x2}";
        Position = Point(record.PackedPosition);
        ZIndex = NpcCharacter.BehindLinkZIndex;

        _crystal = Load(crystalVisual);
        _breakEffect = Load(breakVisual);
        _broken = save?.HasRoomFlag(
            record.Group, record.Room, (byte)data.MoonlitRoomFlag) == true;
        _crystal.SetAnimation(_broken ? 1 : 0);
        _breakEffect.SetAnimation(0);

        // objectMakeTileSolid is immediately narrowed to collision byte $0a.
        // The visual metatile is untouched because the crystal is an OBJ.
        room.SetPositionTileAndCollision(
            Position,
            room.GetMetatile(Position),
            (byte)data.MoonlitCrystalCollision,
            animationTick(),
            preserveRenderedTile: true);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_breakActive)
            return;

        if (_breakSoundCounter > 0 && --_breakSoundCounter == 0)
            _playSound(_data.MoonlitBreakSound);
        if (_breakEffect.CurrentParameter == 0xff)
        {
            _breakActive = false;
            QueueRedraw();
            return;
        }
        _breakEffect.Advance();
        QueueRedraw();
    }

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        TryBreak(hitbox);
        // COLLISIONEFFECT_26 writes LINKDMG_1c and does not mark ordinary
        // sword contact, matching PART_SWITCH's non-consuming response.
        return false;
    }

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        // partActiveCollisions row $24 is indexed by Part.id, not by
        // enemyCollisionMode $03. It accepts $19 but rejects $16 and $18.
        if (collision == RoomEntityItemCollision.SwordBeam)
        {
            TryBreak(hitbox);
        }
        return false;
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns) =>
        TryBreak(hitbox) ? SeedHitResult.Consume : SeedHitResult.None;

    private bool TryBreak(Rect2 hitbox)
    {
        if (_broken || !hitbox.Intersects(CollisionBounds))
            return false;

        byte switchState = _runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        _runtime.SetWramByte(
            OracleRuntimeState.SwitchStateAddress,
            (byte)(switchState ^ _record.SubId));
        _broken = true;
        _crystal.SetAnimation(1);
        _breakEffect.SetAnimation(0);
        _breakActive = true;
        _breakSoundCounter = _data.MoonlitBreakSoundDelay;
        QueueRedraw();
        return true;
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (!Visible)
            return;
        DrawTexture(
            _crystal.CurrentTexture,
            _crystal.CurrentOffset + TransitionDrawOffset);
        if (_breakActive)
        {
            DrawTexture(
                _breakEffect.CurrentTexture,
                _breakEffect.CurrentOffset + TransitionDrawOffset);
        }
    }

    private EnemyAnimationPlayer Load(DungeonInteractionVisual visual)
    {
        var animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        return animation;
    }

    private static Vector2 Point(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
