using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class TimePortal : TransitionOffsetNode2D
{
    internal const float PlacedContactRadius = 8.0f;

    private readonly List<TimePortalAnimationFrame> _frames = new();
    private int _frame;
    private int _frameTicks;
    private int _loopStart;
    private int _palette;
    private float _contactRadius = PlacedContactRadius;
    private OracleRoomData _room = null!;
    private OracleSaveData? _save;
    private Func<int> _playingInstrument = static () => 0;
    private Action<int> _playSound = static _ => { };
    private bool _startsActive;
    private bool _linkWasOutside;
    private TimePortalState _state;

    public PortalRecord Record { get; private set; }
    public bool Entered { get; private set; }
    public bool Active => _state == TimePortalState.Active;
    public bool Temporary { get; private set; }
    internal bool Awakening => _state == TimePortalState.AwaitSongEnd;
    internal int CurrentFrame => _frame;
    internal int CurrentPalette => _palette;

    internal void InitializePlaced(
        PortalRecord record,
        OracleRoomData room,
        bool startsActive,
        OracleSaveData? save,
        Func<int> playingInstrument,
        Action<int> playSound)
    {
        Record = record;
        _room = room;
        _startsActive = startsActive;
        _save = save;
        _playingInstrument = playingInstrument;
        _playSound = playSound;
        _linkWasOutside = true;
        Position = new Vector2(record.X, record.Y);
        _palette = record.Palette;
        BuildAnimation(
            record.SpriteName, record.TileBase, record.Palette,
            record.Animation, cyclePalettes: false);
        _loopStart = record.LoopStart;
        if (_loopStart < 0 || _loopStart >= _frames.Count)
            throw new InvalidOperationException("Time portal has invalid animation data.");
        _state = TimePortalState.AwaitPortalTile;
        TryInitializePlaced();
        QueueRedraw();
    }

    internal void InitializeTemporary(
        TemporaryPortalVisualRecord visual,
        OracleRoomData room,
        Vector2 position)
    {
        _room = room;
        Temporary = true;
        Position = position;
        _palette = visual.Palette;
        _contactRadius = visual.ContactRadius;
        BuildAnimation(
            visual.SpriteName, visual.TileBase, visual.Palette,
            visual.Animation, cyclePalettes: true);
        _loopStart = OracleGraphicsCache.GetAnimationDefinition(
            visual.Animation).LoopStart;
        if (_loopStart < 0 || _loopStart >= _frames.Count)
            throw new InvalidOperationException(
                "Temporary time portal has invalid animation data.");
        _state = TimePortalState.Active;
        Visible = true;
        QueueRedraw();
    }

    internal void UpdateFrame(int frameCounter)
    {
        if (_state == TimePortalState.AwaitPortalTile)
        {
            TryInitializePlaced();
            return;
        }

        if (_state == TimePortalState.AwaitEchoes)
        {
            Visible = false;
            if (_playingInstrument() != 1)
                return;
            MarkSpotDiscovered();
            _state = TimePortalState.AwaitSongEnd;
            return;
        }

        if (_state == TimePortalState.AwaitSongEnd)
        {
            Visible = false;
            if (_playingInstrument() != 0)
                return;
            _playSound(OracleSoundEngine.SndCtrlStopSfx);
            _playSound(OracleSoundEngine.SndTeleport);
            _state = TimePortalState.Active;
        }

        if (!Temporary)
        {
            // INTERAC_TIMEPORTAL_SPAWNER uses objectFlickerVisibility with b=$01.
            Visible = (frameCounter & 1) != 0;
            MarkSpotDiscovered();
        }
        else
        {
            Visible = true;
            if ((frameCounter & 1) == 0)
                _palette = (_palette + 1) & 0x03;
        }

        _frameTicks++;
        if (_frameTicks < _frames[_frame].Duration)
        {
            if (Temporary)
                QueueRedraw();
            return;
        }
        _frameTicks = 0;
        _frame++;
        if (_frame >= _frames.Count)
            _frame = _loopStart;
        QueueRedraw();
    }

    internal bool CheckLinkContact(Vector2 linkPosition)
    {
        if (!Active || Entered)
            return false;
        Vector2 delta = linkPosition - Position;
        bool overlaps =
            Mathf.Abs(delta.X) < _contactRadius &&
            Mathf.Abs(delta.Y) < _contactRadius;
        if (!overlaps)
        {
            _linkWasOutside = true;
            return false;
        }
        // A return portal is created at Link's arrival position. Its source
        // state waits for Link to leave before accepting a fresh collision.
        if (!_linkWasOutside)
            return false;
        Entered = true;
        Visible = false;
        return true;
    }

    public override void _Draw()
    {
        if (_frames.Count == 0)
            return;
        Texture2D[] palettes = _frames[_frame].PaletteTextures;
        int palette = Temporary ? _palette : 0;
        DrawTexture(
            palettes[Math.Clamp(palette, 0, palettes.Length - 1)],
            new Vector2(-16, -16) + TransitionDrawOffset);
    }

    private void TryInitializePlaced()
    {
        if (_room.GetMetatile(Position) != 0xd7)
        {
            Visible = false;
            return;
        }
        _state = _startsActive
            ? TimePortalState.Active
            : TimePortalState.AwaitEchoes;
        Visible = false;
    }

    private void MarkSpotDiscovered()
    {
        if (_save is null)
            return;
        _save.SetRoomFlag(
            Record.Group,
            Record.Room,
            OracleSaveData.RoomFlagPortalSpotDiscovered);
    }

    private void BuildAnimation(
        string spriteName,
        int tileBase,
        int basePalette,
        string encodedAnimation,
        bool cyclePalettes)
    {
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{spriteName}.png");
        AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation);
        foreach (AnimationFrameDefinition frame in definition.Frames)
        {
            int paletteCount = cyclePalettes ? 4 : 1;
            var textures = new Texture2D[paletteCount];
            for (int palette = 0; palette < paletteCount; palette++)
            {
                textures[palette] = NpcCharacter.BuildOamTexture(
                    source,
                    frame.EncodedOam,
                    tileBase,
                    cyclePalettes ? palette : basePalette);
            }
            _frames.Add(new TimePortalAnimationFrame(textures, frame.Duration));
        }
        if (_frames.Count == 0)
            throw new InvalidOperationException("Time portal has no animation frames.");
    }
}

internal sealed record TimePortalAnimationFrame(
    Texture2D[] PaletteTextures,
    int Duration);

internal enum TimePortalState
{
    AwaitPortalTile,
    AwaitEchoes,
    AwaitSongEnd,
    Active
}
