using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class OverworldKeyUseRoomEntity(OverworldKeyUseEffect effect)
    : RoomEntityAdapter<OverworldKeyUseEffect>(effect, effect.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

/// <summary>
/// INTERAC_OVERWORLD_KEY_SPRITE $18. The named key rises until its vertical
/// speed reaches the apex, remains at that height for $3c updates, then
/// disappears. Unlike the dungeon-key sprite, it requests no extra sound.
/// </summary>
internal partial class OverworldKeyUseEffect : TransitionOffsetNode2D
{
    private Texture2D _texture = null!;
    private Vector2 _textureOffset;
    private int _initialSpeedZ;
    private int _gravity;
    private int _holdFrames;
    private int _state;
    private int _counter;
    private int _zFixed;
    private int _speedZ;

    internal bool Finished { get; private set; }
    internal int State => _state;
    internal int Counter => _counter;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;

    internal void Initialize(
        Vector2 position,
        OverworldKeyholeDatabase.Record visual,
        OverworldKeyholeDatabase.ConstantsRecord constants)
    {
        Position = position;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        OracleGraphicsCache.AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animation);
        if (visual.SubId != visual.Treasure - constants.FirstKey ||
            definition.Frames.Length == 0)
        {
            throw new InvalidOperationException(
                "INTERAC_OVERWORLD_KEY_SPRITE visual no longer matches its named key.");
        }
        (_texture, _textureOffset) = NpcCharacter.BuildPositionedOamTexture(
            source,
            definition.Frames[0].EncodedOam,
            visual.TileBase,
            visual.Palette,
            paletteOverride: null,
            sourceGrayscaleInverted: true);
        _initialSpeedZ = constants.InitialSpeedZ;
        _gravity = constants.Gravity;
        _holdFrames = constants.HoldFrames;
        _state = 0;
        _counter = 0;
        _zFixed = 0;
        _speedZ = 0;
        Finished = false;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;

        switch (_state)
        {
            case 0:
                // State 0 initializes speed/graphics and returns without
                // applying vertical motion.
                _state = 1;
                _speedZ = _initialSpeedZ;
                return;
            case 1:
                OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, _gravity);
                QueueRedraw();
                if (_speedZ < 0)
                    return;
                _counter = _holdFrames;
                _state = 2;
                return;
            case 2:
                _counter--;
                if (_counter != 0)
                    return;
                Finished = true;
                Visible = false;
                return;
            default:
                throw new InvalidOperationException(
                    $"Invalid INTERAC_OVERWORLD_KEY_SPRITE state {_state}.");
        }
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _texture,
                _textureOffset + new Vector2(0, _zFixed >> 8) + TransitionDrawOffset);
        }
    }
}
