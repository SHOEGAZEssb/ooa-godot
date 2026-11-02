
namespace Oracles;

// object_code/seasons/specialObjects/linkInCutscene.s: linkCutscene1/2.
internal sealed class SeasonsOpeningLink(SeasonsOpeningSession session)
{
    private int _mode = -1, _state, _counter, _z, _speedZ, _direction, _animation, _frame, _ticks, _savedX;
    internal bool Active => _mode >= 0;
    internal int State => _state;
    internal int Z => _z;
    private Player Link => session.Link;
    internal void Begin(bool dance)
    {
        Link.BeginCutsceneControl(owner: this); Link.Show();
        _mode = dance ? 1 : 2; _state = -1; _z = _speedZ = _direction = 0;
    }
    internal void BeginFall()
    {
        Begin(false); _mode = 3; _state = 0;
        _z = Player.NewGameSlowFallInitialZ(0x38) << 8; _speedZ = 0x20; SetAnimation(8);
        Link.SetCutsceneDrawZFixed(_z);
    }
    internal void End()
    {
        _mode = -1; Link.SetCutsceneSpriteFrame(null); Link.SetCutsceneDrawZFixed(0);
        Link.Face(_direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left });
        Link.EndCutsceneControl(owner: this);
    }
    private void SetAnimation(int animation)
    {
        _animation = animation; _frame = 0;
        var frame = session.Data.LinkFrames(animation, _direction)[0]; _ticks = frame.Duration;
        Link.SetCutsceneSpriteFrame(frame);
    }
    private void Animate()
    {
        _ticks = (_ticks - 1) & 0xff;
        if (_ticks != 0) return;
        var frames = session.Data.LinkFrames(_animation, _direction);
        if (++_frame >= frames.Length) _frame = session.Data.LinkLoop(_animation);
        _ticks = frames[_frame].Duration; Link.SetCutsceneSpriteFrame(frames[_frame]);
    }
    private bool Tick() { _counter = (_counter - 1) & 0xff; return _counter == 0; }
    private bool Gravity(int acceleration, bool cap = false)
    {
        bool landed = OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, acceleration);
        if (cap && _speedZ >= 0x300) _speedZ = 0x300;
        Link.SetCutsceneDrawZFixed(_z); return landed;
    }
    private void Move(int speed, int angle) => Link.SetScriptedPosition(
        NativeObjectMovement.ApplySpeed(session.Memory, OracleObjectPosition.FromPixels(Link.PrecisePosition), speed, angle).PrecisePosition);
    private void Jitter() => Link.SetScriptedCoordinateHigh(true, (_savedX + (session.Random.Next().Value & 0xf) - 8) & 0xff);
    private void DanceAnimation()
    {
        SetAnimation(7); session.Din?.SetAnimation(8);
    }
    private void AnimatePair() { Animate(); session.Din?.Node.AdvanceAnimationUpdates(1); }
    internal void AdvanceFrame()
    {
        if (_mode == 3)
        {
            Animate();
            if (Gravity(0x0c)) { _mode = 2; _state = -1; }
            return;
        }
        if (_state == -1)
        {
            _state = 0;
            if (_mode == 1) { SetAnimation(0); return; }
            SetAnimation(9);
        }
        if (_mode == 2) { Wake(); return; }
        switch (_state)
        {
            case 0:
                if (session.Signal(0) != 0) return;
                _state++; _counter = 0xaa; _direction = 0;
                Link.SetScriptedPosition(new(0x50, 0x30));
                if (session.Din is { } din) din.Node.Position = new(0x60, 0x30);
                DanceAnimation(); break;
            case 1:
                if (!Tick()) { AnimatePair(); return; }
                _counter = 0x1e; _state++; DanceAnimation(); break;
            case 2:
                if (!Tick()) return;
                _counter = 0x28; _state++; SetAnimation(0x10); session.Din?.SetAnimation(0x0d); break;
            case 3:
                if (!Tick()) return;
                _counter = 0x3c; _state++; session.ShowText(0x0c17); break;
            case 4:
                if (session.Dialogue.IsOpen || !Tick()) return;
                _counter = 0x96; _state++; DanceAnimation(); break;
            case 5:
                if (!Tick()) { AnimatePair(); return; }
                session.Signal(0, 2); _state++; break;
            case 6:
                if (session.Signal(0) != 3) { AnimatePair(); return; }
                DanceAnimation(); _state++; break;
            case 7:
                if (session.Signal(0) != 4) return;
                _state++; _counter = 0x5a; _direction = 3; SetAnimation(0); break;
            case 8:
                if (!Tick()) return;
                _counter = 0x12; _state++; break;
            case 9:
                if (!Tick()) { Link.SetScriptedCoordinateHigh(true, ((int)Link.Position.X - 1) & 0xff); Animate(); return; }
                _counter = 0x46; _state++; SetAnimation(0); break;
            case 10:
                if (!Tick()) return;
                session.Signal(0, 5); _state++; break;
            case 11:
                if ((session.Signal(1) & 0x40) == 0) return;
                _counter = 0x14; _savedX = (int)Link.Position.X; _state++; break;
            case 12:
                if (!Tick()) { Jitter(); return; }
                Link.SetScriptedCoordinateHigh(true, 0x40); _speedZ = -0x180; SetAnimation(8); _state++; break;
            case 13:
                Move(0x50, 0x0e);
                if (!Gravity(0x20)) return;
                _state++; _counter = 0x28; SetAnimation(0x14); break;
            case 14:
                if (!Tick()) return;
                session.Signal(0, 7); _state++; break;
            case 15:
                if (session.Signal(0) != 9) return;
                _state++; _speedZ = -0x210; _direction = 2; SetAnimation(0x0a); session.Sound.PlaySound(SoundId.SndJump); break;
            case 16:
                Animate();
                if (!Gravity(0x20)) return;
                _state++; _counter = 0x1e; _direction = 0; SetAnimation(0); break;
            case 17:
                if (!Tick()) return;
                _state++; _counter = 0x19; break;
            case 18:
                Animate(); Move(0x50, 2);
                if (!Tick()) return;
                _state++; _counter = 0x14; _savedX = (int)Link.Position.X; break;
            case 19:
                if (!Tick()) { Jitter(); return; }
                _state++; _direction = 0; SetAnimation(8); session.Memory.SetField("wScrollMode", 0); break;
            case 20:
                Animate(); Move(0x78, 0x19); Move(0x78, 0x19);
                if (Link.Position.X is >= 0 and < 160 && Link.Position.Y is >= 0 and < 128) return;
                session.Signal(0, 0x0a); _state++; break;
        }
    }
    private void Wake()
    {
        switch (_state)
        {
            case 0:
                if (session.Signal(0) != 1) return;
                Animate();
                if (session.Data.LinkParameter(_animation, _frame) != 0xff) return;
                _state++; _speedZ = -0x210; _direction = 2; SetAnimation(0x0a); session.Sound.PlaySound(SoundId.SndJump);
                goto case 1;
            case 1:
                Animate();
                if (!Gravity(0x20, cap: true)) return;
                SetAnimation(0); _counter = 0x1e; _state++; break;
            case 2:
                if (!Tick()) return;
                session.Signal(0, 2); _state++; _direction = 3; SetAnimation(0); break;
            case 3:
                if (session.Signal(0) != 3) return;
                End(); session.FinishOpening(); break;
        }
    }
}
