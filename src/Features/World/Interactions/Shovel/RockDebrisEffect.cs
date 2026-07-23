using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_ROCKDEBRIS ($06) and INTERAC_ROCKDEBRIS2 ($0c). State 0
/// initializes the interaction-specific common-sprite tile base and palette
/// and requests SND_BREAK_ROCK. Their shared animation 0 spreads four rock
/// chips over four 4-update frames, exposes its terminal $ff frame for one
/// update, then deletes on the following update.
/// </summary>
internal partial class RockDebrisEffect : TransitionOffsetNode2D
{

    private static IReadOnlyDictionary<int, Definition>? _definitions;

    private Definition _record = null!;
    private int _animationFrame;
    private int _animationCounter;
    private bool _initialized;
    private Action<int> _playSound = null!;

    internal bool Finished { get; private set; }
    internal int InteractionId => _record.InteractionId;
    internal int ElapsedUpdates { get; private set; }
    internal int AnimationFrame =>
        Math.Min(_animationFrame, _record.Animation.Count - 1);
    internal int CurrentParameter =>
        _record.Animation[AnimationFrame].Parameter;
    internal Texture2D CurrentTexture =>
        _record.Animation[AnimationFrame].Texture;

    internal void Initialize(
        Vector2 position,
        int interactionId = 0x06,
        Action<int>? playSound = null)
    {
        Position = position;
        IReadOnlyDictionary<int, Definition> definitions =
            _definitions ??= LoadDefinitions();
        if (!definitions.TryGetValue(interactionId, out Definition? record))
        {
            throw new InvalidOperationException(
                $"Unsupported rock-debris interaction ${interactionId:x2}.");
        }
        _record = record;
        _playSound = playSound ?? (static _ => { });
        _animationFrame = 0;
        _animationCounter = _record.Animation[0].Duration;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;

        ElapsedUpdates++;
        if (!_initialized)
        {
            _initialized = true;
            _playSound(_record.Sound);
            return;
        }

        if ((CurrentParameter & 0x80) != 0)
        {
            Finished = true;
            Visible = false;
            return;
        }

        _animationCounter--;
        if (_animationCounter == 0)
        {
            _animationFrame++;
            if (_animationFrame >= _record.Animation.Count)
            {
                throw new InvalidOperationException(
                    "INTERAC_ROCKDEBRIS animation ended without a terminal frame.");
            }
            _animationCounter = _record.Animation[_animationFrame].Duration;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private static IReadOnlyDictionary<int, Definition> LoadDefinitions()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/rock_debris.tsv",
            new GeneratedTableSchema(
                "rock debris",
                GeneratedTableKeySemantics.Unique,
                [
                    "interaction-id", "sprite", "tile-base", "palette",
                    "sound", "animation"
                ],
                ["interaction-id"],
                headerRequired: true));
        if (table.Rows.Count != 2)
        {
            throw new InvalidOperationException(
                $"INTERAC_ROCKDEBRIS $06/$0c should have two rows, got {table.Rows.Count}.");
        }

        var definitions = new Dictionary<int, Definition>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int interactionId = row.HexByte(0);
            string sprite = row.RequiredString(1);
            int tileBase = row.UnsignedDecimal(2);
            int palette = row.UnsignedDecimal(3);
            int sound = row.HexByte(4);
            bool expectedVisual = interactionId switch
            {
                0x06 => tileBase == 0x02 && palette == 3,
                0x0c => tileBase == 0x40 && palette == 5,
                _ => false
            };
            if (sprite != "spr_common_sprites" ||
                !expectedVisual ||
                sound != OracleSoundEngine.SndBreakRock)
            {
                throw new InvalidOperationException(
                    $"Imported INTERAC_ROCKDEBRIS ${interactionId:x2} constants are invalid.");
            }

            Image source = OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{sprite}.png");
            var animation = new List<RockDebrisEffectFrameRecord>();
            foreach (AnimationFrameDefinition frame in
                OracleGraphicsCache.GetAnimationDefinition(
                    row.RequiredString(5)).Frames)
            {
                animation.Add(new RockDebrisEffectFrameRecord(
                    NpcCharacter.BuildOamTexture(
                        source, frame.EncodedOam, tileBase, palette),
                    frame.Duration,
                    frame.Parameter));
            }
            if (animation.Count != 5 ||
                animation[0].Duration != 4 ||
                animation[1].Duration != 4 ||
                animation[2].Duration != 4 ||
                animation[3].Duration != 4 ||
                (animation[^1].Parameter & 0x80) == 0)
            {
                throw new InvalidOperationException(
                    $"INTERAC_ROCKDEBRIS ${interactionId:x2} animation 0 is not its 4/4/4/4/terminal sequence.");
            }
            definitions.Add(
                interactionId,
                new Definition(interactionId, sound, animation));
        }
        return definitions;
    }
}

internal sealed record RockDebrisEffectFrameRecord(Texture2D Texture, int Duration, int Parameter);

internal sealed record Definition(int InteractionId, int Sound, List<RockDebrisEffectFrameRecord> Animation);
