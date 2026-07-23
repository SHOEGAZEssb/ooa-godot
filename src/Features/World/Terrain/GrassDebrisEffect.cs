using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_GRASSDEBRIS ($00) and INTERAC_REDGRASSDEBRIS ($01). State 0
/// initializes the common-sprite graphic and requests SND_CUTGRASS. Their
/// eight four-update OAM frames scatter four grass pieces, then expose the
/// terminal $ff parameter for one update before deletion.
/// </summary>
internal partial class GrassDebrisEffect : TransitionOffsetNode2D
{
    private static IReadOnlyDictionary<int, GrassDebrisDefinition>? _definitions;

    private GrassDebrisDefinition _record = null!;
    private IReadOnlyList<GrassDebrisEffectFrameRecord> _animation = null!;
    private int _animationFrame;
    private int _animationCounter;
    private bool _initialized;
    private Action<int> _playSound = null!;

    internal bool Finished { get; private set; }
    internal bool Flickers { get; private set; }
    internal int InteractionId => _record.InteractionId;
    internal int ElapsedUpdates { get; private set; }
    internal int AnimationFrame =>
        Math.Min(_animationFrame, _animation.Count - 1);
    internal int CurrentParameter =>
        _animation[AnimationFrame].Parameter;
    internal Texture2D CurrentTexture =>
        _animation[AnimationFrame].Texture;

    internal void Initialize(
        Vector2 position,
        int interactionId = 0x00,
        bool flickers = false,
        bool underwater = false,
        Action<int>? playSound = null)
    {
        Position = position;
        IReadOnlyDictionary<int, GrassDebrisDefinition> definitions =
            _definitions ??= LoadDefinitions();
        if (!definitions.TryGetValue(
                interactionId, out GrassDebrisDefinition? record))
        {
            throw new InvalidOperationException(
                $"Unsupported grass-debris interaction ${interactionId:x2}.");
        }

        _record = record;
        _animation = underwater && interactionId == 0x00
            ? record.UnderwaterAnimation
            : record.Animation;
        Flickers = flickers;
        _playSound = playSound ?? (static _ => { });
        _animationFrame = 0;
        _animationCounter = _animation[0].Duration;
        QueueRedraw();
    }

    internal void UpdateFrame(int globalFrameCounter)
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

        // Subid bit 0 uses (wFrameCounter XOR the interaction slot page) bit
        // 0. Managed entities have no WRAM page; like item-drop flicker, use
        // the global even phase while retaining the exact per-update cadence.
        if (Flickers)
            Visible = (globalFrameCounter & 1) == 0;

        _animationCounter--;
        if (_animationCounter == 0)
        {
            _animationFrame++;
            if (_animationFrame >= _animation.Count)
            {
                throw new InvalidOperationException(
                    $"INTERAC_GRASSDEBRIS ${InteractionId:x2} animation " +
                    "ended without a terminal frame.");
            }
            _animationCounter = _animation[_animationFrame].Duration;
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

    private static IReadOnlyDictionary<int, GrassDebrisDefinition>
        LoadDefinitions()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/grass_debris.tsv",
            new GeneratedTableSchema(
                "grass debris",
                GeneratedTableKeySemantics.Unique,
                [
                    "interaction-id", "sprite", "tile-base", "palette",
                    "underwater-palette", "sound", "animation"
                ],
                ["interaction-id"],
                headerRequired: true));
        if (table.Rows.Count != 2)
        {
            throw new InvalidOperationException(
                "INTERAC_GRASSDEBRIS $00/$01 should have two rows, got " +
                $"{table.Rows.Count}.");
        }

        var definitions = new Dictionary<int, GrassDebrisDefinition>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int interactionId = row.HexByte(0);
            string sprite = row.RequiredString(1);
            int tileBase = row.UnsignedDecimal(2);
            int palette = row.UnsignedDecimal(3);
            int underwaterPalette = row.UnsignedDecimal(4);
            int sound = row.HexByte(5);
            if (interactionId is not (0x00 or 0x01) ||
                sprite != "spr_common_sprites" ||
                tileBase != 0x00 ||
                palette != 0 ||
                underwaterPalette != (interactionId == 0x00 ? 6 : 0) ||
                sound != OracleSoundEngine.SndCutGrass)
            {
                throw new InvalidOperationException(
                    $"Imported INTERAC_GRASSDEBRIS ${interactionId:x2} " +
                    "constants are invalid.");
            }

            Image source = OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{sprite}.png");
            AnimationDefinition animationDefinition =
                OracleGraphicsCache.GetAnimationDefinition(
                    row.RequiredString(6));
            List<GrassDebrisEffectFrameRecord> animation =
                BuildAnimation(animationDefinition, source, tileBase, palette);
            List<GrassDebrisEffectFrameRecord> underwaterAnimation =
                BuildAnimation(
                    animationDefinition, source, tileBase, underwaterPalette);

            bool validAnimation = animation.Count == 9 &&
                animation[^1].Duration == 0x7f &&
                (animation[^1].Parameter & 0x80) != 0;
            for (int frame = 0;
                 validAnimation && frame < animation.Count - 1;
                 frame++)
            {
                validAnimation = animation[frame].Duration == 4 &&
                    animation[frame].Parameter == 0;
            }
            if (!validAnimation)
            {
                throw new InvalidOperationException(
                    $"INTERAC_GRASSDEBRIS ${interactionId:x2} animation 0 " +
                    "is not its eight 4-update frames plus terminal sequence.");
            }

            definitions.Add(
                interactionId,
                new GrassDebrisDefinition(
                    interactionId, sound, animation, underwaterAnimation));
        }
        return definitions;
    }

    private static List<GrassDebrisEffectFrameRecord> BuildAnimation(
        AnimationDefinition definition,
        Image source,
        int tileBase,
        int palette)
    {
        var animation = new List<GrassDebrisEffectFrameRecord>();
        foreach (AnimationFrameDefinition frame in definition.Frames)
        {
            animation.Add(new GrassDebrisEffectFrameRecord(
                NpcCharacter.BuildOamTexture(
                    source, frame.EncodedOam, tileBase, palette),
                frame.Duration,
                frame.Parameter));
        }
        return animation;
    }
}

internal sealed record GrassDebrisEffectFrameRecord(
    Texture2D Texture,
    int Duration,
    int Parameter);

internal sealed record GrassDebrisDefinition(
    int InteractionId,
    int Sound,
    List<GrassDebrisEffectFrameRecord> Animation,
    List<GrassDebrisEffectFrameRecord> UnderwaterAnimation);

internal sealed class GrassDebrisRoomEntity(GrassDebrisEffect debris)
    : RoomEntityAdapter<GrassDebrisEffect>(
        debris, debris.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
