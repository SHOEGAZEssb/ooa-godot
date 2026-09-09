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
    private Func<bool> _menuDisabled = static () => false;
    private bool _startsActive;
    private bool _linkWasOutside;
    private TimePortalState _state;
    private bool _ranActiveUpdate;
    private int _temporaryState;

    public PortalRecord Record { get; private set; }
    public bool Entered { get; private set; }
    internal bool Expired { get; private set; }
    public bool Active => !Entered && !Expired && _state == TimePortalState.Active;
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
        Vector2 position,
        OracleSaveData? save = null,
        Func<bool>? menuDisabled = null)
    {
        _room = room;
        Temporary = true;
        _save = save;
        _menuDisabled = menuDisabled ?? (static () => false);
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

    internal void UpdateFrame(int frameCounter, Player? player = null)
    {
        if (Entered || Expired) return;
        if (Temporary && _menuDisabled())
        {
            Visible = false;
            return;
        }
        if (Temporary && player is not null)
        {
            Visible = true;
            bool overlaps = player.OverlapsTimePortalHeight && Overlaps(player.EnemyContactPosition);
            if (_temporaryState == 0)
            {
                _temporaryState = overlaps ? 1 : 2;
                _linkWasOutside = !overlaps;
                return;
            }
            if (_temporaryState == 1 && !overlaps)
            {
                _temporaryState = 2;
                _linkWasOutside = true;
                return;
            }
            if (_temporaryState == 2 && _save is not null &&
                _save.TimePortalPosition != _room.GetPackedPosition(Position))
            {
                Expired = true;
                Visible = false;
                return;
            }
        }
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
            // interactionCodee1 state 2 returns after interactionIncState.
            // Presentation and contact belong to the following state-3 update.
            return;
        }

        _ranActiveUpdate = true;
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

    /// <summary>
    /// INTERAC_TIMEPORTAL_SPAWNER state 0 resolves the portal tile and installs
    /// graphics while scrolling. An already-active portal reaches state 3 and
    /// is drawable during the incoming transition; dormant spots remain
    /// intentionally hidden until the Tune of Echoes activation states.
    /// </summary>
    internal ScreenTransitionPresentation PrepareForScreenTransition()
    {
        if (_state == TimePortalState.AwaitPortalTile)
            TryInitializePlaced();
        if (_state != TimePortalState.Active)
            return ScreenTransitionPresentation.Hidden;

        Visible = true;
        QueueRedraw();
        return ScreenTransitionPresentation.Visible;
    }

    internal bool CheckLinkContact(Vector2 linkPosition)
    {
        if (!Active || !_ranActiveUpdate || Entered)
            return false;
        bool overlaps = Overlaps(linkPosition);
        if (!overlaps)
        {
            _linkWasOutside = true;
            return false;
        }
        // A return portal is created at Link's arrival position. Its source
        // state waits for Link to leave before accepting a fresh collision.
        if (!_linkWasOutside)
            return false;
        if (!Temporary && (Record.SubId & 0x40) != 0)
            _save?.SetRoomFlag(Record.Group, Record.Room, 0x02);
        Entered = true;
        Visible = false;
        return true;
    }

    private bool Overlaps(Vector2 position)
    {
        Vector2 delta = position - Position;
        return ((Mathf.FloorToInt(delta.X) + (int)_contactRadius) & 0xff) < _contactRadius * 2 &&
            ((Mathf.FloorToInt(delta.Y) + (int)_contactRadius) & 0xff) < _contactRadius * 2;
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
