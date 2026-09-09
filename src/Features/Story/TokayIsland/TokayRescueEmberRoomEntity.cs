using Godot;
using System;

namespace oracleofages;

/// <summary>INTERAC_TOKAY_CUTSCENE_EMBER_SEED $8f; stepped after its source Tokay slots.</summary>
internal sealed partial class TokayRescueEmberRoomEntity : TransitionOffsetNode2D, IRoomEntity, IRoomEntityLifetime
{
    private readonly TokayRescueEmberRecord _record;
    private readonly EnemyAnimationPlayer _seed;
    private readonly EnemyAnimationPlayer _flame;
    private int _zFixed;
    private int _speedZ;
    internal int Phase { get; private set; }
    internal int Counter { get; private set; }
    internal int ZFixed => _zFixed;
    internal Texture2D CurrentTexture => (Phase == 3 ? _flame : _seed).CurrentTexture;
    public Node2D Node => this;
    public bool Finished => Phase == 4;

    internal TokayRescueEmberRoomEntity(TokayRescueEmberSpawn spawn)
    {
        _record = spawn.Record;
        Position = spawn.Position;
        _seed = new(this, 1);
        _flame = new(this, 1);
        _seed.Load(EnemyVisualSource.LoadComposite([_record.Sprite]), [_record.SeedAnimation],
            _record.TileBase, _record.Palette, positionedOam: true);
        _flame.Load(EnemyVisualSource.LoadComposite([_record.FlameSprite]), [_record.FlameAnimation],
            _record.FlameTileBase, _record.FlamePalette, positionedOam: true);
        _seed.SetAnimation(0);
        _flame.SetAnimation(0);
        Visible = false;
        ZIndex = NpcCharacter.InFrontOfLinkZIndex;
    }

    internal void UpdateNative(bool dialogueOpen)
    {
        switch (Phase)
        {
            case 0:
                _speedZ = _record.SpeedZ;
                Visible = true;
                Phase = 1;
                break;
            case 1:
                if (!OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, _record.Gravity)) break;
                Visible = false;
                if (dialogueOpen) Phase = 2;
                break;
            case 2:
                if (dialogueOpen) break;
                Phase = 3;
                Counter = _record.FlameCounter;
                _flame.SetAnimation(0);
                Visible = true;
                break;
            case 3:
                _flame.Advance();
                if (--Counter == 0) Finish();
                break;
            case 4: return;
            default: throw new InvalidOperationException($"Unsupported INTERAC $8f state ${Phase:x2}.");
        }
        QueueRedraw();
    }

    internal void Finish() { Phase = 4; Visible = false; }
    public override void _Draw()
    {
        if (!Visible) return;
        EnemyAnimationPlayer animation = Phase == 3 ? _flame : _seed;
        DrawTexture(animation.CurrentTexture, animation.CurrentOffset + new Vector2(0, _zFixed >> 8) + SourceOamDrawOffset);
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}

internal sealed record TokayRescueEmberSpawn(TokayRescueEmberRecord Record, Vector2 Position) : RoomEntitySpawn;
