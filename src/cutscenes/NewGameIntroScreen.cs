using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Screen-space presentation for CUTSCENE_PREGAME_INTRO. Link and the two
/// INTERAC_SPARKLE objects are composed from their imported graphics, OAM,
/// palette, animation, and unsigned object-coordinate records.
/// </summary>
public partial class NewGameIntroScreen : Node2D
{
    private CutsceneSpriteRenderer _renderer = null!;
    private NewGameIntroDatabase.NewGameIntroRecord _record;
    private NewGameIntroDatabase.IntroSpriteFrame[] _linkSpin = null!;
    private NewGameIntroDatabase.IntroSpriteFrame[] _linkVanish = null!;
    private NewGameIntroDatabase.IntroSpriteFrame[] _orbDescend = null!;
    private NewGameIntroDatabase.IntroSpriteFrame[] _orbVanish = null!;
    private int _clock;
    private int _motionClock;
    private int _stageFrame;
    private bool _vanishing;
    private bool _linkVisible = true;

    public DialogueBox Dialogue { get; private set; } = null!;

    public override void _Ready()
    {
        var database = new NewGameIntroDatabase();
        _record = database.Record;
        _linkSpin = database.SpriteFrames("link-spin");
        _linkVanish = database.SpriteFrames("link-vanish");
        _orbDescend = database.SpriteFrames("orb-descend");
        _orbVanish = database.SpriteFrames("orb-vanish");
        _renderer = new CutsceneSpriteRenderer();
        Dialogue = new DialogueBox
        {
            Name = "IntroDialogue",
            ZIndex = 2,
            Visible = false
        };
        AddChild(Dialogue);
        QueueRedraw();
    }

    internal void ShowDialogue() =>
        Dialogue.ShowMessage(_record.Text, 0, _record.TextPosition);

    internal void SetAnimation(
        int clock,
        int motionClock,
        int stageFrame,
        bool vanishing,
        bool linkVisible)
    {
        _clock = clock;
        _motionClock = motionClock;
        _stageFrame = stageFrame;
        _vanishing = vanishing;
        _linkVisible = linkVisible;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 160, 144), Colors.Black);

        int z = LinkZForValidation(
            _motionClock,
            _record.InitialWaitFrames + _record.VoiceWaitFrames,
            _record.DescendOscillation,
            _record.HoverOscillation);
        int objectY = (_record.LinkY + 0x10 + z) & 0xff;
        int objectX = _record.LinkX & 0xff;

        if (_linkVisible)
        {
            NewGameIntroDatabase.IntroSpriteFrame frame = _vanishing
                ? AnimationFrame(_linkVanish, _stageFrame, -1)
                : AnimationFrame(_linkSpin, _clock, 0);
            _renderer.DrawScreenFrame(this, frame, objectY, objectX);
        }

        // Priority $80 puts both INTERAC_SPARKLE subids over Link's priority
        // $81. Their even-update flicker exposes Link on alternating frames,
        // which makes him appear contained within the orb.
        if ((_clock & 1) == 0)
        {
            if (_vanishing)
                _renderer.DrawScreenFrame(
                    this, AnimationFrame(_orbVanish, _stageFrame, 2), objectY, objectX);
            else if (_linkVisible)
                _renderer.DrawScreenFrame(
                    this, AnimationFrame(_orbDescend, _clock, 0), objectY, objectX);
        }
    }

    private static NewGameIntroDatabase.IntroSpriteFrame AnimationFrame(
        NewGameIntroDatabase.IntroSpriteFrame[] frames,
        int clock,
        int loopStart)
    {
        int remaining = Math.Max(0, clock);
        int index = 0;
        while (remaining >= frames[index].Duration)
        {
            remaining -= frames[index].Duration;
            index++;
            if (index >= frames.Length)
            {
                if (loopStart < 0)
                    return frames[^1];
                index = loopStart;
            }
        }
        return frames[index];
    }

    internal static int LinkZForValidation(
        int clock,
        int descendFrames,
        int[] descendDeltas,
        int[] hoverDeltas)
    {
        int z = 0;
        for (int frame = 8; frame <= clock; frame += 8)
        {
            int[] deltas = frame <= descendFrames ? descendDeltas : hoverDeltas;
            z = (z + deltas[(frame & 0x38) >> 3]) & 0xff;
        }
        return z;
    }

    internal static int FirstVisibleLinkFrameForValidation(
        int rawY,
        int descendFrames,
        int[] descendDeltas,
        int[] hoverDeltas,
        int oamY = 0x08)
    {
        for (int frame = 0; frame < 0x1000; frame++)
        {
            int z = LinkZForValidation(
                frame, descendFrames, descendDeltas, hoverDeltas);
            if (((rawY + 0x10 + z + oamY) & 0xff) < 0xa0)
                return frame;
        }
        throw new InvalidOperationException("Link never entered the visible OAM range.");
    }

}
