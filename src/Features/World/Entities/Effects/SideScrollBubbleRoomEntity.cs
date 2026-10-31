using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>INTERAC_BUBBLE $91:$00 in side-view water.</summary>
internal sealed partial class SideScrollBubbleRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly OracleRoomData _room;
    private readonly OracleRandom _random;
    private readonly OracleRuntimeState _runtime;
    private readonly BubbleDefinition _definition;
    private Vector2 _precisePosition;
    private bool _initialized;
    private int _turnStep;
    private int _turns;
    private int _counter;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BubbleDefinition> Definitions = new(System.StringComparer.Ordinal);
    static SideScrollBubbleRoomEntity() => OracleGraphicsCache.RegisterApplicationCache(Definitions.Clear);

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int Angle { get; private set; }
    internal int TurnCounter => _counter;
    internal int TurnsRemaining => _turns;
    internal Vector2 PrecisePosition => _precisePosition;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal SideScrollBubbleRoomEntity(Vector2 position, OracleRoomData room, OracleRandom random, OracleRuntimeState runtime)
    {
        AssetRoot = room.AssetRoot;
        Name = "SideScrollBubble";
        _room = room;
        _random = random;
        _runtime = runtime;
        _definition = Definitions.GetOrAdd(AssetRoot, LoadDefinition);
        // objectCopyPosition copies yh/xh only; new fractional bytes are zero.
        _precisePosition = OracleObjectMath.ToPixelPosition(position);
        Position = OracleObjectMath.ToPixelPosition(position);
        Visible = false;
        // objectSetVisible83: behind Link, with room-space coordinates.
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => Advance();

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized && !Finished) Advance();
        return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    private void Advance()
    {
        if (Finished) return;
        // @checkDelete consumes lookupCollisionTable's match carry, including
        // Seasons' lava rows. Probe native byte-indexed layout before RNG.
        Vector2 sample = new(unchecked((byte)Mathf.FloorToInt(Position.X)),unchecked((byte)Mathf.FloorToInt(Position.Y)));
        byte tile = _room.ReadNativeLayoutByte((byte)_room.GetPackedPosition(sample),_runtime);
        if (TilePropertyDatabase.For(AssetRoot).NativeHazardFlags(_room.ActiveCollisions,tile) == 0)
        {
            Finished = true;
            Visible = false;
            return;
        }
        if (!_initialized)
        {
            _initialized = true;
            _counter = _definition.TurnCounter;
            _turns = _definition.InitialTurns;
            _turnStep = (_random.Next().Value & 1) == 0 ? -1 : 1;
            Visible = true;
            QueueRedraw();
            return;
        }

        Position = NativeObjectMovement.ApplySpeed(
            _runtime, ref _precisePosition, _definition.Speed, Angle);
        if ((Mathf.FloorToInt(Position.Y) & 0xff) >= 0xf0)
        {
            Finished = true;
            Visible = false;
            return;
        }
        if (--_counter == 0)
        {
            _counter = _definition.TurnCounter;
            if (--_turns == 0)
            {
                _turns = _definition.Turns;
                _turnStep = -_turnStep;
            }
            Angle = (Angle + _turnStep) & ObjectAngle.Mask;
        }
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (Visible && !Finished)
            DrawTexture(_definition.Texture, _definition.Offset + SourceOamDrawOffset);
    }

    private static BubbleDefinition LoadDefinition(string assetRoot)
    {
        GeneratedTable table = GeneratedTable.Load(
            assetRoot + "effects/side_scroll_bubble.tsv",
            new GeneratedTableSchema("side-view swimming bubble",
                GeneratedTableKeySemantics.Ordered,
                ["tile-base", "palette", "speed-raw", "turn-counter", "initial-turns",
                    "turns", "animation", "source"], headerRequired: true));
        GeneratedTableRow row = table.SingleRow();
        AnimationDefinition animation = OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(6));
        if (row.UnsignedDecimal(0) != 0x16 || row.UnsignedDecimal(1) != 1 ||
            row.UnsignedDecimal(2) != 0x14 || row.UnsignedDecimal(3) != 4 ||
            row.UnsignedDecimal(4) != 5 || row.UnsignedDecimal(5) != 8 ||
            animation.Frames.Length != 1 || animation.Frames[0].Duration != 0x7f)
            throw new InvalidOperationException($"{row.RequiredString(7)}: unexpected bubble constants or animation.");
        (Texture2D texture, Vector2 offset) = OracleOamCompositor.BuildPositionedOamTexture(
            OracleGraphicsCache.LoadImage(assetRoot + "gfx/spr_common_sprites.png"),
            animation.Frames[0].EncodedOam, row.UnsignedDecimal(0), row.UnsignedDecimal(1),
            paletteOverride: null, sourceGrayscaleInverted: true);
        return new BubbleDefinition(texture, offset, row.UnsignedDecimal(2),
            row.UnsignedDecimal(3), row.UnsignedDecimal(4), row.UnsignedDecimal(5));
    }

    private sealed record BubbleDefinition(Texture2D Texture, Vector2 Offset,
        int Speed, int TurnCounter, int InitialTurns, int Turns);
}

internal sealed record SideScrollBubbleSpawn(Vector2 Position) : RoomEntitySpawn;
