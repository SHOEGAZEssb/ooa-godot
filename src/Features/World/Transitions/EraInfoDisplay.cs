using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_ERA_OR_SEASON_INFO $e0. The four-cell present/past symbol enters
/// from the right, pauses at the upper-left of the gameplay field, and exits
/// left using the original fixed-update state machine.
/// </summary>
internal sealed partial class EraInfoDisplay : TransitionOffsetNode2D
{
    private EraInfoDatabaseRecord _record;
    private EnemyAnimationPlayer _animation = null!;
    private EraInfoStage _stage;
    private int _counter;

    internal int SubId => _record.SubId;
    internal EraInfoStage Stage => _stage;
    internal int Counter => _counter;
    internal bool Finished => _stage == EraInfoStage.Finished;
    internal Vector2I TextureSize => _animation is not null &&
        _animation.HasFrames
        ? new Vector2I(
            _animation.CurrentTexture.GetWidth(),
            _animation.CurrentTexture.GetHeight())
        : Vector2I.Zero;
    internal Vector2 TextureOffset => _animation is not null &&
        _animation.HasFrames
        ? _animation.CurrentOffset
        : Vector2.Zero;
    internal ulong PixelHash => _animation is not null &&
        _animation.HasFrames
        ? OracleGraphicsCache.PixelHash(
            _animation.CurrentTexture.GetImage())
        : 0;

    internal void Initialize(EraInfoDatabaseRecord record)
    {
        _record = record;
        Name = record.SubId == 0 ? "PresentEraInfo" : "PastEraInfo";
        ZIndex = NpcCharacter.InFrontOfLinkZIndex;
        Visible = false;

        Image image = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.Sprite}.png");
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            image,
            [record.Animation],
            record.TileBase,
            record.Palette,
            positionedOam: true);
        if (!_animation.HasFrames ||
            _animation.CurrentParameter != 0 ||
            TextureSize != new Vector2I(32, 16))
        {
            throw new InvalidOperationException(
                $"Malformed INTERAC_ERA_OR_SEASON_INFO visual in {record.Source}.");
        }
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        switch (_stage)
        {
            case EraInfoStage.Initializing:
                Position = new Vector2(_record.StartX, _record.StartY);
                _stage = EraInfoStage.Entering;
                Visible = true;
                break;

            case EraInfoStage.Entering:
                Position = new Vector2(
                    Position.X - _record.EnterStep,
                    Position.Y);
                if (Mathf.RoundToInt(Position.X) == _record.TargetX)
                {
                    _stage = EraInfoStage.Holding;
                    _counter = _record.HoldUpdates;
                }
                break;

            case EraInfoStage.Holding:
                if (--_counter == 0)
                {
                    _stage = EraInfoStage.Exiting;
                    _counter = _record.ExitUpdates;
                }
                break;

            case EraInfoStage.Exiting:
                Position = new Vector2(
                    Position.X - _record.ExitStep,
                    Position.Y);
                if (--_counter == 0)
                    _stage = EraInfoStage.Finished;
                break;

            case EraInfoStage.Finished:
                break;

            default:
                throw new InvalidOperationException(
                    $"Invalid INTERAC_ERA_OR_SEASON_INFO stage {_stage}.");
        }
    }

    public override void _Draw()
    {
        if (_animation is not null && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                _animation.CurrentOffset + TransitionDrawOffset);
        }
    }
}

internal enum EraInfoStage
{
    Initializing,
    Entering,
    Holding,
    Exiting,
    Finished
}
