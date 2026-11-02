
namespace Oracles;

internal sealed partial class SeasonsOpeningActor
{
    private int _targetY, _delay, _wait, _beadKind;
    private Vector2 _beadOrigin;
    private void InitializeImprisonment()
    {
        switch (Subid)
        {
            case 0: Script("dinImprisonedScript_setDinCoords"); break;
            case 1: Script("dinImprisoned_OnoxExplainsMotive"); break;
            case 2: break;
            case 3:
                _counter2 = (_session.Random.Next().Value & 7) + 1;
                _angle = (Variable * 4 + 0x10) & 0x1f; _speed = 0x78;
                Node.Hide(); break;
            case 4:
                _counter = Variable == 0 ? 10 : 20;
                if (Variable != 0) SetAnimation(5); break;
            default: throw Unsupported();
        }
    }
    private void Imprisonment()
    {
        switch (Subid)
        {
            case 0:
                if (_session.Signal(0) == 0x0e) { Delete(); return; }
                if (_session.Signal(0) == 0x0d)
                {
                    if (State == 0) { State++; Node.Position = new(0x81, 0x4a); SetAnimation(0x0e); }
                    int[] wave = [0, -1, -2, -1]; SetZ(wave[(_session.FrameCounter >> 3) & 3] << 8);
                }
                Node.AdvanceAnimationUpdates(1); Runner.AdvanceFrame(); break;
            case 1:
                if (State == 0)
                {
                    Runner.AdvanceFrame();
                    if (!Runner.Active) State = 1;
                    else
                    {
                        Node.AdvanceAnimationUpdates(1);
                        if (Node.ConsumeAnimationParameter() == 0x70) _session.Sound.PlaySound(0x70);
                    }
                }
                else if (State == 1 && _session.Signal(0) == 0x0e)
                {
                    State = 2; Node.Hide(); Script("dinImprisonedScript_OnoxSendsTempleDown");
                }
                else if (State == 2) Runner.AdvanceFrame();
                break;
            case 2: MainBead(); break;
            case 3: BeadParticles(); break;
            case 4:
                if (State == 0) { if (Tick()) State++; return; }
                if (_session.Signal(0) == 0x0c) { Delete(); return; }
                Move(_speed, _angle); break;
        }
    }
    private void MainBead()
    {
        int[] ys = [0x60, 0x60, 0x90, 0x90, 0x30, 0x30, 0x60, 0x60];
        int[] xs = [0x98, 0x68, 0x98, 0x68, 0x68, 0x98, 0x68, 0x98];
        int[] angles = [0x1c, 4, 0x14, 0x0c, 0x0c, 0x14, 4, 0x1c];
        switch (State)
        {
            case 0:
                State = 1; if (Variable == 0) _session.Sound.PlaySound(0x7c);
                _angle = (Variable & 1) == 0 ? 0x1c : 4;
                _targetY = Variable < 2 ? 0x30 : 0x60;
                _delay = new[] { 0x3c, 0x46, 0x50, 0x5a }[Variable];
                _wait = new[] { 0x5a, 0x50, 0x46, 0x3c }[Variable];
                Node.Position = new(xs[Variable], ys[Variable]); _counter = 0x3c;
                goto case 1;
            case 1:
                if (Tick()) { State++; Node.Show(); } else Node.Visible = !Node.Visible;
                break;
            case 2:
                if (--_delay != 0) return;
                _speed = 0x78; State++; break;
            case 3:
                Move(_speed, _angle);
                if ((int)Node.Position.Y >= _targetY)
                {
                    int camera = ((int)Node.Position.Y - 0x40) & 255;
                    if (Variable == 0 && camera < 0x10)
                    { _session.NativeScreen.CameraY = camera; _session.NativeScreen.ScrollY = camera; _session.NativeScreen.QueueRedraw(); }
                    return;
                }
                _angle = angles[Variable]; State++; break;
            case 4:
                if (--_wait != 0) return;
                State++; _counter = 0xa0;
                if (Variable == 0) EmitParticles(new(0x82, 0x48), 0xfe, 0);
                break;
            case 5:
                if (Tick())
                {
                    Node.Show(); _counter = 0x28; Node.Position = new(xs[Variable + 4], ys[Variable + 4]); State++; return;
                }
                _angle = (_angle + 1) & 0x1f;
                var offset = NativeObjectMovement.CircleArcOffset(_session.Memory, 0x20, _angle);
                Node.Position = new(0x82 + offset.X, 0x48 + offset.Y); break;
            case 6:
                if (!Tick()) return;
                _speed = 0x14; _counter = 0x3c; _angle = angles[Variable + 4]; State++;
                for (int child = 1; child >= 0; child--)
                    if (_session.Spawn(0x4f, 4, Node.Position, child) is { } actor) { actor._angle = _angle; actor._speed = _speed; }
                break;
            case 7:
                Move(_speed, _angle);
                if (!Tick()) return;
                _session.Signal(0, 0x0c); State++;
                if (Variable == 0) _session.Sound.PlaySound(0x79); break;
            case 8:
                if (_session.Signal(0) == 0x0d) Delete(); break;
        }
    }
    private void EmitParticles(Vector2 origin, int lifetime, int kind)
    {
        for (int index = 7; index >= 0; index--)
            if (_session.Spawn(0x4f, 3, origin, index) is { } actor)
            { actor._counter = lifetime; actor._beadOrigin = origin; actor._beadKind = kind; }
    }
    private void BeadParticles()
    {
        if (_counter != 0xff && Tick()) { Delete(); return; }
        if (State == 0)
        {
            if (--_counter2 != 0) return;
            SetAnimation(Variable + 6 + (_beadKind == 0 ? 0 : 0x0b));
            if (_beadKind == 0)
            {
                var offset = NativeObjectMovement.CircleArcOffset(_session.Memory, 0x38, Variable * 4);
                Node.Position = _beadOrigin + new Vector2(offset.X, offset.Y);
            }
            else Node.Position = _beadOrigin;
            Node.Show(); State = 1; return;
        }
        Move(_speed, _angle); Node.AdvanceAnimationUpdates(1);
        if (Node.CurrentAnimationParameter != 0xff) return;
        State = 0; _counter2 = (_session.Random.Next().Value & 7) + 1; Node.Hide();
    }
}
