using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed partial class SeasonsOpeningActor : INativeInteractionRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity, IPlayerInteractable
{
    private readonly SeasonsOpeningSession _session;
    internal int Slot => _session.Entities.NativeInteractionSlot(this);
    internal int Id { get; }
    internal int Subid { get; private set; }
    internal int Variable { get; }
    internal NpcCharacter Node { get; }
    internal CutsceneCommandRunner Runner { get; }
    internal byte[] Bytes { get; } = new byte[256];
    internal int TextId, RadiusY = 6, RadiusX = 6;
    internal bool Initialized { get; private set; }
    internal bool AlwaysUpdate => (Id == 0x4e && Subid == 7) || Id is 0x9f or 5 or 8 or 0x88;
    internal bool ButtonPressed;
    internal int ZFixed, SpeedZ;
    internal int State { get; private set; }
    private int _counter, _counter2, _savedX, _angle, _speed, _lightningRow;
    private bool _scriptStarted, _deleted;

    Node2D IRoomEntity.Node => Node;
    public bool Finished => _deleted;
    public bool UpdatesDuringDialogue => !Initialized || AlwaysUpdate;
    public bool UpdatesDuringRoomEntityFreeze => !Initialized || AlwaysUpdate;
    public void SetTransitionDrawOffset(Vector2 offset) => Node.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => AdvanceFrame();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (AlwaysUpdate || !Initialized) AdvanceFrame();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!Initialized) AdvanceFrame();
        return Finished || !Node.Visible ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible;
    }
    public bool TryInteract(Player player) => TryTalk();

    internal SeasonsOpeningActor(SeasonsOpeningSession session, int id, int subid, Vector2 position, int variable)
    {
        _session = session; Id = id; Subid = subid; Variable = variable;
        Node = id == 0x85 ? new NpcCharacter { AssetRoot = SeasonsGameContent.AssetRoot } : session.Data.CreateActor(id, subid, position);
        Node.ZIndex = id switch {
            0x4e => subid == 7 ? ObjectDrawPriority.FixedHighPriorityZIndex : ObjectDrawPriority.FixedLowPriorityZIndex,
            0x4f => subid switch { 2 => ObjectDrawPriority.InFrontOfLinkZIndex, 3 => ObjectDrawPriority.FixedHighPriorityZIndex,
                4 when variable == 0 => ObjectDrawPriority.FixedLowPriorityZIndex, _ => ObjectDrawPriority.BehindLinkZIndex },
            0x9f => ObjectDrawPriority.FixedHighPriorityZIndex,
            0x86 => ObjectDrawPriority.InFrontOfLinkZIndex,
            0x77 when subid == 1 => ObjectDrawPriority.FixedLowPriorityZIndex,
            _ => ObjectDrawPriority.BehindLinkZIndex };
        if (id == 0xa5 || id == 0x4f && subid == 0)
            Node.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex, withTerrainEffects: true);
        Node.SetWorldToScreen(point => session.NativeScreen.Visible
            ? point - new Vector2(session.NativeScreen.CameraX, session.NativeScreen.CameraY) : point,
            () => session.Scrolling);
        Runner = new(new SeasonsOpeningScriptHost(session, this));
        Node.Hide();
    }
    internal void SetAnimation(int animation)
    {
        Node.SetScriptAnimation(_session.Data.Animation(Id, animation));
    }
    internal void SetZ(int zFixed) { ZFixed = zFixed; Node.SetScriptDrawOffset(new(0, zFixed >> 8)); }
    internal void Move(int speed, int angle) => Node.Position = NativeObjectMovement.ApplySpeed(_session.Memory,
        OracleObjectPosition.FromPixels(Node.Position), speed, angle).PrecisePosition;
    internal bool BlocksLink(Vector2 position) => Initialized && (Id == 0x9d || Id == 0x4e && Subid is >= 0 and <= 6 && State < 3) && Node.BlocksLinkCenter(position);
    internal bool TryTalk()
    {
        if (!(Id == 0x9d || Id == 0x4e && Subid is >= 0 and <= 4 or 6)) return false;
        if (!Initialized || !Node.CanScriptTalkTo(_session.Link, RadiusY, RadiusX, NpcCharacter.AButtonPointOffset)) return false;
        ButtonPressed = true; return true;
    }
    private void Script(string name)
    {
        int speed = Runner.ActorSpeed("Self"), angle = Runner.ActorAngle("Self"), counter = Runner.NativeCounter1;
        Runner.Start(_session.Data.Scripts[name]);
        Runner.SetInitialMotionRegisters("Self", speed, angle); Runner.NativeCounter1 = counter;
        _scriptStarted = true;
    }
    private bool Tick() { _counter = (_counter - 1) & 0xff; return _counter == 0; }
    private void Delete() { _deleted = true; Runner.Clear(); Node.Hide(); _session.Delete(this); }
    internal void AdvanceFrame()
    {
        if (Id == 0x85) { MusicController(); return; }
        if (Id == 0x88 && !Initialized && _session.Fading) return;
        if (!Initialized)
        {
            Initialized = true; Node.ShowInitializedPresentation();
            switch (Id)
            {
                case 0xa5:
                    if (Subid != 9) throw Unsupported();
                    Script("dinScript_discoverLinkCollapsed_body"); return;
                case 0x4e:
                    if (Subid == 0x0b)
                    {
                        if (!_session.RoomFlag(0x40))
                        {
                            foreach (var placement in _session.Data.TroupePlacements()) _session.Spawn(placement.Id, placement.Subid, placement.Position);
                            _session.Signal(0, 0); _session.Signal(1, 0); _session.Signal(2, 0); _session.Sound.SetMusicVolume(3);
                        }
                        Delete(); return;
                    }
                    if (Subid != 5)
                        Script(Subid switch { 0 => "troupeScript1", 1 => "troupeScript2", 2 => "troupeScript3", 3 => "troupeScript4",
                            4 => "troupeScript_Impa", 6 => "troupeScript_Din", 7 => "tornadoScript_startDestruction", _ => throw Unsupported() });
                    else Node.SetCollisionRadii(6, 6);
                    break;
                case 0x9d:
                    if (Subid == 4 && !_session.Save.IsLinkedGame) { Delete(); return; }
                    if (Subid != 0) throw Unsupported();
                    if (_session.RoomFlag(0x40)) { Node.Position = new(0x18, 0x28); Subid = 1; }
                    else _session.Sound.PlaySound(0x1f);
                    Script("impaScript_afterOnoxTakesDin"); return;
                case 0x4f: InitializeImprisonment(); return;
                case 0x9f: _counter = Variable; return;
                case 0x05: _session.Sound.PlaySound(SoundId.SndPoof); return;
                case 0x08: _session.Sound.PlaySound(SoundId.SndKillEnemy); return;
                case 0x77: SetAnimation(Subid); return;
                case 0x86: ResetExplosion(); return;
                case 0x88:
                    Node.Hide(); Node.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
                    Node.Position = new(0, Subid == 0 ? 0x28 : (0x28 - _session.NativeScreen.ScrollY) & 255);
                    if (Subid == 0) { State = 1; SetLightningRow(0); }
                    else if (_session.Save.HasGlobalFlag(0x30)) Delete();
                    else _counter = 60;
                    return;
                default: throw Unsupported();
            }
        }
        switch (Id)
        {
            case 0xa5:
                if ((Bytes[0x78] & 0x80) == 0) Node.AdvanceAnimationUpdates((Bytes[0x78] & 0x7f) == 0 ? 1 : 2);
                Runner.AdvanceFrame();
                if (!Runner.Active && !_deleted)
                {
                    if (State == 0) { State = 1; _session.Fade(true); }
                    else if (!_session.Fading) _session.FinishForest();
                }
                break;
            case 0x4e: Troupe(); break;
            case 0x9d:
                Runner.AdvanceFrame();
                if (Subid == 1) Node.FaceLinkImmediatelyAndAnimateAsNpcOneUpdate(_session.Link);
                else
                {
                    if (_session.RoomFlag(0x40)) Subid = 1;
                    Node.AdvanceAnimationUpdates(1); Node.PushPlayerAwayAndUpdateDrawPriority(_session.Link);
                }
                break;
            case 0x4f: Imprisonment(); break;
            case 0x9f:
                Node.AdvanceAnimationUpdates(1); if (_counter != 0xff && Tick()) Delete(); break;
            case 0x05:
            case 0x08:
                Node.AdvanceAnimationUpdates(1); if (Node.CurrentAnimationParameter == 0xff) Delete(); break;
            case 0x77:
                Node.SetScriptVisible((_session.Signal(3) & 0x80) == 0);
                var screen = _session.NativeScreen;
                Node.Position = Subid == 0 ? new((0x54 - screen.ScrollX) & 255, (0x7d - screen.ScrollY) & 255) :
                    new((0x19 + screen.WindowX + 7) & 255, (0xe9 + screen.WindowY) & 255); break;
            case 0x86: Explosion(); break;
            case 0x88: CastleLightning(); break;
        }
    }
    private void MusicController()
    {
        if (!Initialized)
        {
            Initialized = true;
            if ((_session.Save.GetRoomFlags(0, 0x98) & 0x40) != 0) { Delete(); return; }
            if (_session.Signal(7) != 0) return;
            _session.Signal(7, 1); _session.Sound.PlaySound(0x08);
        }
        int volume = _session.Link.Position.X < 0x70 ? 1 : 2;
        if (Bytes[0x77] != volume) { Bytes[0x77] = (byte)volume; _session.Sound.SetMusicVolume(volume); }
    }
    private void SetLightningRow(int row)
    {
        if (row == _session.Data.CastleLightning.Length)
        {
            if (Subid == 0) Delete(); else { State = 0; _counter = 60; }
            return;
        }
        _lightningRow = row; _counter = _session.Data.CastleLightning[row].Duration; _counter2 = 0;
    }
    private void CastleLightning()
    {
        if (!_session.Save.HasGlobalFlag(0x0a) && _session.Fading) { Delete(); return; }
        if (State == 0)
        {
            if (!Tick()) return;
            _counter = 60;
            if ((_session.Random.Next().Value & 1) == 0) return;
            _session.Random.Next(); State = 1; SetLightningRow(0); return;
        }
        int palette = _session.Data.CastleLightning[_lightningRow].Palette;
        if (_counter2 == 0)
        {
            _counter2 = 1;
            string name = new[] { "PALH_TILESET_ONOX_CASTLE_OUTSIDE_WINTER", "PALH_SEASONS_99", "PALH_SEASONS_9a" }[palette];
            _session.Data.Palettes.Restore(OracleGraphicsData.LoadPalette(SeasonsOpeningData.Root + $"opening/{name}-bg.bin"));
            if (palette != 0) _session.Sound.PlaySound(0xd2);
            if (palette != 2) Node.Hide();
            else
            {
                int direction = _session.Random.Next().Value & 1;
                Node.Position = new(direction == 0 ? 0x13 : 0x8d, Node.Position.Y);
                var colors = OracleGraphicsData.LoadPalette(SeasonsOpeningData.Root + "opening/PALH_SEASONS_9a-spr.bin");
                Node.SetScriptPaletteOverrides(new System.Collections.Generic.Dictionary<int, Color[]> {
                    [7] = [Colors.Transparent, colors[7, 1], colors[7, 2], colors[7, 3]] });
                SetAnimation(direction); Node.Show();
            }
        }
        if (palette == 2) { Node.AdvanceAnimationUpdates(1); if (Node.CurrentAnimationParameter == 0xff) Node.Hide(); }
        if (Tick()) SetLightningRow(_lightningRow + 1);
    }
    private void Troupe()
    {
        if (Subid == 6) { Din(); return; }
        if (Subid == 7)
        {
            if (State != 1) Runner.AdvanceFrame();
            if (State == 0 && !Runner.Active) { _session.Signal(0, 4); State = 1; }
            else if (State == 1 && _session.Signal(0) == 6) { State = 2; Script("tornadoScript_endDestruction"); }
            else if (State == 2 && !Runner.Active) { Delete(); return; }
            Node.AdvanceAnimationUpdates(1);
            if ((_session.FrameCounter & 0x3f) == 0) _session.Sound.PlaySound(0xd3);
            return;
        }
        if (Subid == 5)
        {
            if (_session.Signal(0) != 0) { _session.Spawn(5, 0, Node.Position); Delete(); return; }
            Node.AdvanceAnimationUpdates(1); Node.PushPlayerAwayAndUpdateDrawPriority(_session.Link); return;
        }
        switch (State)
        {
            case 0:
                if (_session.Signal(0) != 0) State = 1;
                Node.AdvanceAnimationUpdates(1); RunAndPush(); break;
            case 1:
                if ((_session.Signal(1) & (1 << Subid)) != 0) { State = 2; _counter = 0x20; _savedX = (int)Node.Position.X; }
                RunAndPush(); break;
            case 2:
                if (Tick()) { State = 3; _angle = new[] { 4, 0x1d, 0x1e, 5, 0x15 }[Subid]; _speed = 0x78; return; }
                Node.Position = new((_savedX + (_session.Random.Next().Value & 15) - 8) & 255, Node.Position.Y);
                RunAndPush(); break;
            case 3:
                Move(_speed, _angle); Move(_speed, _angle);
                if (Node.Position.X is < 0 or >= 160 || Node.Position.Y is < 0 or >= 128) Delete(); break;
        }
    }
    private void RunAndPush()
    {
        if (_scriptStarted) Runner.AdvanceFrame();
        Node.PushPlayerAwayAndUpdateDrawPriority(_session.Link);
    }
    private void Din()
    {
        switch (State)
        {
            case 0:
                if (_session.Signal(3) == 0x3f) { State = 1; Script("troupeScript_startDanceScene"); goto case 1; }
                Node.AdvanceAnimationUpdates(1); RunAndPush(); break;
            case 1:
                Node.AdvanceAnimationUpdates(1); RunAndPush();
                if ((_session.Signal(3) & 0x40) == 0) return;
                _session.Fade(true, fast: true); State = 2; break;
            case 2:
                if (_session.Fading) return;
                _session.Signal(3, 0x80); _session.StartNativeLink(true); _session.StartCapture(); State = 3; break;
            case 3:
                if (_session.Signal(0) != 0) return;
                RunAndPush(); State = 4; break;
            case 4:
                if (_session.Signal(0) != 4) return;
                State = 5; SetAnimation(0x0d); break;
            case 5:
                if (_session.Signal(0) != 7) return;
                State = 6; break;
            case 6:
                Move(0x0a, 8);
                if ((_session.Signal(1) & 0x80) == 0) return;
                _session.Signal(0, 8); Delete(); break;
        }
    }
    private void ResetExplosion()
    {
        int[][] positions = [[0x79,0x42,0x7b,0x4e,0x7e,0x5b,0x80,0x70,0x81,0x8a],
            [0,0x38,0x6c,0x20,0x48,0x40,0x3c,0x91,0x34,0x64],
            [0x2c,0x7e,0x1e,0x9e,0x50,0x6e,0x28,0x24,0x60,0x20],
            [0x1c,0x18,0x44,0x64,0,0x5c,0x68,0x70,0x74,0x34],
            [0xe0,0xe0,0x7b,0x4e,0x7e,0x58,0x80,0x68,0x81,0x80]];
        _counter2 = _session.Signal(3) & 0x7f;
        if (_counter2 >= positions.Length) throw Unsupported();
        int y = positions[_counter2][Subid * 2] + (_session.Random.Next().Value & 3) - 2;
        int x = positions[_counter2][Subid * 2 + 1] + (_session.Random.Next().Value & 3) - 2;
        Node.Position = new(x & 255, y & 255); SetAnimation(0);
    }
    private void Explosion()
    {
        if (_session.Signal(3) == 0xff) { Delete(); return; }
        if (_counter2 != (_session.Signal(3) & 0x7f) || Node.CurrentAnimationParameter == 0xff) ResetExplosion();
        Node.AdvanceAnimationUpdates(1); Node.SetScriptVisible(((_session.FrameCounter ^ Subid) & 1) != 0);
    }
    private NotSupportedException Unsupported() => new($"Seasons opening interaction ${Id:x2}:${Subid:x2} state ${State:x2} is unsupported.");
}
