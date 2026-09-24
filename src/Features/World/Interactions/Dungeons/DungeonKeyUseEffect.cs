using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_DUNGEON_KEY_SPRITE $17, small-key subid $00 or boss-key subid $01.
/// The key stays four pixels above the door for eight updates, then eight
/// pixels above it for 20.
/// </summary>
internal partial class DungeonKeyUseEffect : FixedEffectNode2D
{
    private Texture2D _texture = null!;
    private Vector2 _textureOffset;
    private int _phase;
    private int _counter;
    private int _z;
    private int _initialAnimationParameter;
    private System.Action _playSound = null!;
    internal bool Initialized { get; private set; }

    internal override bool Finished { get; private protected set; }
    internal int Phase => _phase;
    internal int Counter => _counter;
    internal int Z => _z;
    internal int Graphic { get; private set; }
    // interactionInitGraphics loads the first parameter in state0. States1/2
    // never call interactionAnimate, so that byte stays until deletion.
    internal int AnimationParameter => Initialized ? _initialAnimationParameter : 0;

    internal void Initialize(
        Vector2 position,
        TreasureObjectVisualRecord visual,
        System.Action playSound)
    {
        _playSound = playSound;
        Visible = false;
        Position = position;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animation);
        bool validSmallKey = visual.Graphic == 0x42 && visual.TileBase == 0x0c;
        bool validBossKey = visual.Graphic == 0x43 && visual.TileBase == 0x08;
        if ((!validSmallKey && !validBossKey) || visual.Palette != 5 ||
            definition.Frames.Length == 0)
        {
            throw new System.InvalidOperationException(
                "INTERAC_DUNGEON_KEY_SPRITE visual no longer matches treasure graphic $42/$43.");
        }
        (_texture, _textureOffset) = NpcCharacter.BuildPositionedOamTexture(
            source,
            definition.Frames[0].EncodedOam,
            visual.TileBase,
            visual.Palette,
            paletteOverride: null,
            sourceGrayscaleInverted: true);
        Graphic = visual.Graphic;
        _initialAnimationParameter = definition.Frames[0].Parameter;
        _phase = 0;
        _counter = 8;
        _z = -4;
        QueueRedraw();
    }

    internal override void UpdateFrame()
    {
        if (Finished)
            return;
        // interactionCode17 state0 returns after graphics, visibility and sound.
        // Its counter does not decrement until the following eligible update.
        if (!Initialized)
        {
            Initialized = true;
            Visible = true;
            _playSound();
            return;
        }
        _counter--;
        if (_counter != 0)
            return;
        if (_phase == 0)
        {
            _phase = 1;
            _counter = 20;
            _z = -8;
            QueueRedraw();
            return;
        }
        Finished = true;
        Visible = false;
    }

    public override void _Draw()
    {
        if (!Finished)
            DrawTexture(
                _texture,
                _textureOffset + new Vector2(0, _z) + TransitionDrawOffset);
    }
}
