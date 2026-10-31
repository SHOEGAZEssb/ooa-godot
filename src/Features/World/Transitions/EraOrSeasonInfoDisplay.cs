using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>
/// INTERAC_ERA_OR_SEASON_INFO $e0. The four-cell era/season symbol enters
/// from the right, pauses at the upper-left of the gameplay field, and exits
/// left using the original fixed-update state machine.
/// </summary>
internal sealed partial class EraOrSeasonInfoDisplay : TransitionOffsetNode2D
{
    private EraOrSeasonInfoRecord _record;
    private EraOrSeasonInfoDatabase _data = null!;
    private System.Func<OracleRoomData> _room = null!;
    private OracleRuntimeState _memory = null!;
    private EnemyAnimationPlayer _animation = null!;
    private EraOrSeasonInfoStage _stage;
    private int _counter;

    internal int SubId => _record.SubId;
    internal EraOrSeasonInfoStage Stage => _stage;
    internal int Counter => _counter;
    internal bool Finished => _stage == EraOrSeasonInfoStage.Finished;
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

    internal void Initialize(EraOrSeasonInfoDatabase data, System.Func<OracleRoomData> room, OracleRuntimeState memory)
    {
        AssetRoot = data.AssetRoot;
        _data = data; _room = room; _memory = memory;
        Name = "EraOrSeasonInfo";
        ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
        Visible = false;
    }

    private void LoadGraphics()
    {
        _record = _data.Select(_room(),_memory);
        var record = _record;
        Image image = OracleGraphicsCache.LoadImage(
            AssetRoot + $"gfx/{record.Sprite}.png");
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
            case EraOrSeasonInfoStage.Initializing:
                LoadGraphics();
                Position = new Vector2(_record.StartX, _record.StartY);
                _stage = EraOrSeasonInfoStage.Entering;
                Visible = true;
                break;

            case EraOrSeasonInfoStage.Entering:
                Position = new Vector2(
                    Position.X - _record.EnterStep,
                    Position.Y);
                if (Mathf.RoundToInt(Position.X) == _record.TargetX)
                {
                    _stage = EraOrSeasonInfoStage.Holding;
                    _counter = _record.HoldUpdates;
                }
                break;

            case EraOrSeasonInfoStage.Holding:
                if (--_counter == 0)
                {
                    _stage = EraOrSeasonInfoStage.Exiting;
                    _counter = _record.ExitUpdates;
                }
                break;

            case EraOrSeasonInfoStage.Exiting:
                Position = new Vector2(
                    Position.X - _record.ExitStep,
                    Position.Y);
                if (--_counter == 0)
                    _stage = EraOrSeasonInfoStage.Finished;
                break;

            case EraOrSeasonInfoStage.Finished:
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

internal enum EraOrSeasonInfoStage
{
    Initializing,
    Entering,
    Holding,
    Exiting,
    Finished
}
