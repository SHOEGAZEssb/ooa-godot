using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed record Room148DebrisSpawn(
    Vector2 Position,
    int Palette,
    int Angle,
    int DrawPriority)
    : RoomEntitySpawn(UpdateThisFrame: true);

/// <summary>
/// Native fixed-update owner for INTERAC_PICKAXE_WORKER $57:$00. The original
/// animation parameter is nonzero only on a strike update and directly owns
/// both SND_CLINK and creation of the two dirt-chip interactions.
/// </summary>
internal sealed class Room148PickaxeWorkerRoomEntity(
    NpcCharacter worker,
    Room148PickaxeDatabase.PickaxeRecord record,
    Action<int> playSound)
    : RoomEntityAdapter<NpcCharacter>(worker, worker.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomBlocker, ITalkTarget, INpcTalkLifecycle
{
    private bool _talking;

    public NpcCharacter TalkNpc => Entity;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;

        // interactionAnimateAsNpc runs before the worker checks animParameter.
        Entity.AdvanceAnimationUpdates(1);
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position);

        int parameter = Entity.CurrentAnimationParameter;
        if (parameter == 0)
            return;
        if (parameter is not 1 and not 2)
            throw new InvalidOperationException(
                $"Pickaxe worker $57:$00 produced unsupported animation parameter ${parameter:x2}.");

        playSound(record.Sound);
        float x = Entity.Position.X +
            (parameter == 1 ? -record.OffsetX : record.OffsetX);
        Vector2 position = new(x, Entity.Position.Y + record.OffsetY);
        int[] angles = { record.Angle0, record.Angle1 };
        for (int index = record.DebrisCount - 1; index >= 0; index--)
        {
            spawns.Add(new Room148DebrisSpawn(
                position,
                parameter,
                angles[index],
                // The child copies the worker's visible priority and
                // decrements its low priority code during $92:$06 state 0.
                Math.Max(0, Entity.ZIndex - 1)));
        }
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    public void OnNpcTalkStarted()
    {
        if (_talking)
            return;
        _talking = true;
        Entity.SetScriptAnimation(record.TalkAnimation);
    }

    public void OnNpcTalkEnded()
    {
        if (!_talking)
            return;
        _talking = false;
        Entity.SetScriptAnimation(record.WorkAnimation);
        // The script jumps back to animation $02 and then reaches
        // interactionAnimateAsNpc later on this same update.
        Entity.AdvanceAnimationUpdates(1);
    }
}

internal sealed class Room148DebrisRoomEntity(Room148PickaxeDebris debris)
    : RoomEntityAdapter<Room148PickaxeDebris>(
        debris, debris.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

/// <summary>
/// INTERAC_FALLING_ROCK $92:$06, including its state-0 return and exact 8.8 Z
/// integration. OAM flag bit 3 addresses fixed VRAM bank 1, so tile base $02
/// comes from spr_common_sprites rather than the worker's dynamic sprite slot.
/// </summary>
internal partial class Room148PickaxeDebris : Node2D
{
    private Texture2D _texture = null!;
    private Vector2 _transitionDrawOffset;
    private Vector2 _precisePosition;
    private int _speed;
    private int _gravity;
    private bool _stateInitialized;

    internal bool Finished { get; private set; }
    internal int Palette { get; private set; }
    internal int Angle { get; private set; }
    internal int ZFixed { get; private set; }
    internal int SpeedZ { get; private set; }
    internal Vector2 PrecisePosition => _precisePosition;
    internal bool StateInitialized => _stateInitialized;

    internal void Initialize(
        Room148PickaxeDatabase.PickaxeRecord record,
        Room148DebrisSpawn spawn)
    {
        OracleGraphicsCache.AnimationFrameDefinition[] frames =
            OracleGraphicsCache.GetAnimationDefinition(
                record.DebrisAnimation).Frames;
        if (frames.Length == 0)
            throw new InvalidOperationException(
                "INTERAC_FALLING_ROCK $92:$06 has no imported animation frame.");
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.DebrisSpriteName}.png");
        _texture = NpcCharacter.BuildOamTexture(
            source,
            frames[0].EncodedOam,
            record.DebrisTileBase,
            spawn.Palette);
        Palette = spawn.Palette;
        Angle = spawn.Angle;
        ZIndex = spawn.DrawPriority;
        _speed = record.Speed;
        _gravity = record.Gravity;
        SpeedZ = record.InitialSpeedZ;
        ZFixed = 0;
        _precisePosition = spawn.Position;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;
        if (!_stateInitialized)
        {
            // fallingRock_subid06 state 0 installs angle/speed/Z and returns.
            _stateInitialized = true;
            return;
        }

        int z = ZFixed;
        int speedZ = SpeedZ;
        if (OracleObjectMath.UpdateSpeedZ(ref z, ref speedZ, _gravity))
        {
            ZFixed = z;
            SpeedZ = speedZ;
            Finished = true;
            Visible = false;
            return;
        }

        ZFixed = z;
        SpeedZ = speedZ;
        _precisePosition += OracleObjectMath.VectorFromAngle32(Angle) *
            (_speed / 40.0f);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _texture,
                new Vector2(-16, -16 + (ZFixed >> 8)) +
                    _transitionDrawOffset);
        }
    }
}
