using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal partial class SeasonsOpeningSession : Node2D
{
    internal SeasonsOpeningData Data { get; private set; } = null!;
    internal Player Link { get; private set; } = null!;
    internal DialogueBox Dialogue { get; private set; } = null!;
    internal OracleSoundEngine Sound { get; private set; } = null!;
    internal OracleSaveData Save { get; private set; } = null!;
    internal OracleRuntimeState Memory { get; private set; } = null!;
    internal OracleRandom Random { get; private set; } = null!;
    internal OracleRoomData Room { get; private set; } = null!;
    internal RoomCollision Collision { get; private set; } = null!;
    internal int FrameCounter => Memory.ReadWramByte(0xcc00);
    internal int RoomId { get; private set; }
    internal byte DisabledObjects
    {
        get => Memory.ReadField("wDisabledObjects");
        set => Memory.SetField("wDisabledObjects", value);
    }
    internal int Signal(int index) => Memory.ReadWramByte(0xcfd0 + index);
    internal void Signal(int index, int value) => Memory.SetWramByte(0xcfd0 + index, unchecked((byte)value));
    internal bool Fading => _fadeRemaining != 0;
    internal bool Scrolling => _scrollFrames != 0;
    internal bool Complete => Phase == SeasonsOpeningPhase.Complete;
    internal SeasonsOpeningPhase Phase { get; private set; }
    internal SeasonsOpeningActor?[] Actors { get; } = new SeasonsOpeningActor?[16];
    private readonly (NpcCharacter Node, LightningEffect Effect)?[] _lightning = new (NpcCharacter, LightningEffect)?[16];
    private readonly List<SeasonsOpeningActor> _outgoingActors = [];
    private Node2D _actorLayer = null!;
    private RoomView _roomView = null!;
    private Hud _hud = null!;
    private ColorRect _overlay = null!;
    private readonly CanvasItemMaterial _whiteFade = new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    private readonly CanvasItemMaterial _blackFade = new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Sub };
    private StatusBarController _status = null!;
    private InventoryState _inventory = null!;
    private NewGameIntroScreen? _pregameScreen;
    private NewGameIntroController? _pregame;
    private SeasonsOpeningLink _nativeLink = null!;
    private SeasonsOpeningSequence _sequence = null!;
    private SeasonsOpeningScreen _screen = null!;
    private int _roomTicks, _arrivalFrames, _fadeRemaining, _fadeDuration, _scrollFrames, _scrollDirection;
    private bool _fadeOut;
    private Color _fadeColor = Colors.White;
    private Vector2 _scrollStart;
    private int _pendingExit;
    internal int LastText { get; private set; } = -1;
    internal bool ButtonConsumed { get; private set; }

    public override void _EnterTree()
    {
        GetNode<Hud>("Hud").AssetRoot = SeasonsGameContent.AssetRoot;
        GetNode<DialogueBox>("Dialogue").PrepareResources(SeasonsGameContent.AssetRoot);
    }

    internal void Start(OracleSoundEngine sound, OracleRandom random, OracleSaveData? save = null)
    {
        if (!ResourcesPrepared) foreach (bool _ in PrepareResources()) { }
        Sound = sound; Random = random; Memory = new(OracleGame.Seasons);
        Save = save ?? OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        if (save is null) Save.SetLinkName("Link");
        if (Save.Game != OracleGame.Seasons || Save.IsLinkedGame || Save.HasGlobalFlag(0x21))
            throw new System.NotSupportedException("Seasons opening requires an unlinked pre-intro checkpoint.");
        Dialogue.ApplicationUpdateOwned = true;
        Dialogue.SetSoundPlayer(Sound.PlaySound); Dialogue.SetLinkNameProvider(() => Save.LinkName);
        Dialogue.SetBackgroundPaletteState(Data.Palettes);
        Dialogue.MessageSpeed = Save.TextSpeed;
        _hud.AssetRoot = SeasonsGameContent.AssetRoot; _hud.PrepareResources();
        _inventory = new(TreasureDatabase.For(SeasonsGameContent.AssetRoot), Save, runtimeState: Memory);
        _status = new(_inventory, _hud, Sound.PlaySound, () => FrameCounter);
        _hud.Initialize(TreasureDatabase.For(SeasonsGameContent.AssetRoot), _inventory);
        Collision = new(() => Room, (position, _) => Actors.Any(actor => actor?.BlocksLink(position) == true),
            Memory, HasNeighbor, normalizeCornerWalls: false);
        LoadRoom(0xa7, 0, spawn: false, preserveActors: true);
        Link.ApplicationUpdateOwned = true;
        Link.Initialize(new SeasonsOpeningWorld(this), _inventory, new(0x48, 0x38), Random);
        Link.SetPhysicsProcess(true); Link.SetProcess(true);
        _nativeLink = new(this); _sequence = new(this, _screen);
        Link.Hide(); _roomView.Hide(); _hud.Hide();
        _pregameScreen!.Show();
        _pregameScreen.Dialogue.ApplicationUpdateOwned = true;
        _pregameScreen.Dialogue.SetLinkNameProvider(() => Save.LinkName);
        _pregameScreen.Dialogue.MessageSpeed = Save.TextSpeed;
        _pregame = new(_pregameScreen, BeginArrival, Sound, initializing: true, () => FrameCounter);
        Phase = SeasonsOpeningPhase.Pregame;
    }

    internal void AdvanceFrame()
    {
        if (_pregame is { GraphicsLoadPending: true }) { _pregame.CompleteGraphicsLoad(); return; }
        Memory.SetWramByte(0xcc00, unchecked((byte)(FrameCounter + 1)));
        Save.AdvancePlaytime(); ButtonConsumed = false;
        if (_pregame is not null)
        {
            var previous = _pregame.CurrentStage;
            _pregame.Update(1.0 / 60);
            if (previous == Stage.PostVanish && _pregame?.CurrentStage == Stage.RestartGame) Save.SetGlobalFlag(0x2a);
            _pregameScreen?.Dialogue.AdvanceApplicationUpdate(); return;
        }
        AdvanceFade();
        if (Phase == SeasonsOpeningPhase.Arrival) { AdvanceArrival(); Dialogue.AdvanceApplicationUpdate(); return; }
        if (Scrolling) { AdvanceScroll(); return; }
        _sequence.AdvanceFrame();
        if (!_sequence.SkipObjects)
        {
            if (_nativeLink.Active) _nativeLink.AdvanceFrame(); else Link.AdvanceApplicationUpdate();
            for (int slot = 0; slot < _lightning.Length; slot++)
            {
                if (_lightning[slot] is not { } lightning) continue;
                lightning.Effect.UpdateFrame();
                if (!lightning.Effect.Finished) continue;
                lightning.Node.QueueFree(); _lightning[slot] = null;
            }
            bool interactionsBlocked = Dialogue.IsOpen || (DisabledObjects & 0x02) != 0;
            for (int slot = 0; slot < Actors.Length; slot++)
                if (Actors[slot] is { } actor && (!interactionsBlocked || actor.AlwaysUpdate || !actor.Initialized)) actor.AdvanceFrame();
        }
        if (!_screen.Visible)
        {
            if (Room.UpdateAnimation(++_roomTicks)) _roomView.SetRoom(Room.Texture);
            _status.Update(1.0 / 60);
        }
        _sequence.AdvanceShake();
        Dialogue.AdvanceApplicationUpdate();
        if (_pendingExit != 0) { int direction = _pendingExit; _pendingExit = 0; BeginScroll(direction); }
    }

    private void BeginArrival()
    {
        _pregame = null; _pregameScreen!.QueueFree(); _pregameScreen = null;
        Phase = SeasonsOpeningPhase.Arrival;
        OrRoomFlag(OracleSaveData.RoomFlagVisited);
        _roomView.Show(); _hud.HideStatusBar();
        DisabledObjects = 0x81; _arrivalFrames = 0;
        _overlay.Color = Colors.White;
        _nativeLink.BeginFall();
        Link.Hide(); _roomView.SetHorizontalWave(0xff, 0); Sound.PlaySound(SoundId.SndWarpStart);
    }

    private void AdvanceArrival()
    {
        _arrivalFrames++;
        if (_arrivalFrames <= NewGameIntroController.ArrivalFadeWaitFrames)
        {
            _overlay.Color = new(1, 1, 1, 1 - Math.Min(32, (_arrivalFrames + 1) / 2) / 32f);
            _roomView.SetHorizontalWave(0xff, _arrivalFrames);
            return;
        }
        int wave = _arrivalFrames - NewGameIntroController.ArrivalFadeWaitFrames;
        _roomView.SetHorizontalWave(Math.Max(0, 255 - wave * 2), FrameCounter);
        if (wave >= 64) { Link.Show(); _nativeLink.AdvanceFrame(); }
        if (wave <= 128) return;
        _roomView.ClearHorizontalWave(); _hud.ShowStatusBar();
        DisabledObjects = 0;
        Save.SetGlobalFlag(0x21); Random.BeginRoomParse();
        Phase = SeasonsOpeningPhase.Forest; Spawn(0xa5, 9, new(0x48, 0xf8));
    }

    internal void LoadRoom(int room, int season, bool spawn = true, bool preserveActors = false)
    {
        if (!preserveActors) ClearActors();
        RoomId = room; _roomTicks = 0;
        Room = Data.LoadRoom(room, season); _roomView.SetRoom(Room.Texture);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 10; x++) Memory.SetWramByte(0xcf00 + y * 16 + x, Room.GetMetatile(new(x * 16, y * 16)));
        Memory.SetField("wScrollMode", 1);
        if (!preserveActors)
        {
            OrRoomFlag(OracleSaveData.RoomFlagVisited);
            Random.BindPlacementMemory(Memory); Random.BeginRoomParse();
        }
        if (!spawn) return;
        if (room == 0x98)
        {
            Spawn(0x4e, 0x0b, Vector2.Zero)?.AdvanceFrame();
        }
        else if (room == 0x97)
        {
            Spawn(0x85, 0, Vector2.Zero);
        }
        else if (room == 0xb6) Spawn(0x9d, 0, new(0x28, 0x28));
    }

    internal SeasonsOpeningActor? Spawn(int id, int subid, Vector2 position, int variable = 0)
    {
        int slot = Array.FindIndex(Actors, actor => actor is null);
        if (slot < 0) return null;
        var actor = new SeasonsOpeningActor(this, slot, id, subid, position, variable);
        Actors[slot] = actor; _actorLayer.AddChild(actor.Node);
        if (id != 0x85) actor.Node.BindTerrainShadow(Room, () => FrameCounter, () => 0xd0 + slot, () => Scrolling, Memory);
        return actor;
    }
    internal void Delete(SeasonsOpeningActor actor)
    {
        if (Actors[actor.Slot] != actor) return;
        Actors[actor.Slot] = null; actor.Node.NotifyRetired(); actor.Node.QueueFree();
    }
    internal void StrikeLightning(int bits, Vector2 position)
    {
        int slot = Array.FindIndex(_lightning, part => part is null);
        if (slot < 0) return;
        var node = Data.CreateActor(0x27, 0, position); _actorLayer.AddChild(node);
        var effect = new LightningEffect(node, () => Random.Next().Value, Sound.PlaySound,
            frames => _sequence.Shake(frames), debris => Spawn(8, 0, debris),
            SeasonsGameContent.AssetRoot, () => Signal(2, Signal(2) | bits));
        _lightning[slot] = (node, effect);
    }
    internal void ClearActors() { foreach (var actor in Actors.ToArray()) if (actor is not null) Delete(actor); }
    internal SeasonsOpeningActor? Din => Actors.FirstOrDefault(actor => actor is { Id: 0x4e, Subid: 6 });
    internal void StartNativeLink(bool dance) => _nativeLink.Begin(dance);
    internal void ReleaseLink() => _nativeLink.End();
    internal void SetPhase(SeasonsOpeningPhase phase) => Phase = phase;
    internal void StartCapture() => _sequence.StartDance();
    internal void FinishForest()
    {
        LoadRoom(0x97, 0); Link.WarpTo(new(0x48, 0x48)); Link.Face(Vector2I.Up);
        _nativeLink.End(); DisabledObjects = 0; Phase = SeasonsOpeningPhase.Troupe; Fade(false);
    }
    internal void BeginAftermath()
    {
        _screen.Hide(); _actorLayer.Position = new(0, 16); Link.Show();
        LoadRoom(0xb6, 0); Link.WarpTo(new(0x58, 0x48)); Link.Face(Vector2I.Up);
        Signal(0, 0); _nativeLink.Begin(false); _hud.Show(); _roomView.Show();
        Phase = SeasonsOpeningPhase.Aftermath; Fade(false, black: true);
    }
    internal void FinishOpening() { Phase = SeasonsOpeningPhase.Complete; }
    internal bool TryTalk()
    {
        if (DisabledObjects != 0 || Dialogue.IsOpen || ButtonConsumed) return false;
        foreach (var actor in Actors)
            if (actor is not null && actor.TryTalk()) { ButtonConsumed = true; return true; }
        return false;
    }
    internal void ShowText(int id, int? position = null)
    {
        LastText = id;
        Dialogue.ShowMessage(Data.Text(id), _screen.Visible ? DialogueScreenContext.FullScreen(Link.Position.Y) :
            DialogueScreenContext.Gameplay(Link.Position.Y), position, 0, id);
    }
    internal bool RoomFlag(int flag) => (Save.GetRoomFlags(0, RoomId) & flag) != 0;
    internal void OrRoomFlag(int flag) => Save.SetRoomFlag(0, RoomId, (byte)flag);
    internal void Fade(bool outward, bool fast = false, bool black = false)
    {
        _fadeOut = outward; _fadeRemaining = _fadeDuration = fast ? 11 : 32;
        _overlay.Material = black ? _blackFade : _whiteFade;
        _fadeColor = Colors.White;
        _overlay.Color = new(_fadeColor, outward ? 0 : 1);
    }
    private void AdvanceFade()
    {
        if (_fadeRemaining == 0) return;
        _fadeRemaining--;
        float amount = (_fadeDuration - _fadeRemaining) * (_fadeDuration == 11 ? 3 : 1) / 31f;
        _overlay.Color = new(_fadeColor, Math.Clamp(_fadeOut ? amount : 1 - amount, 0, 1));
    }
    internal void SetFullScreen(bool enabled)
    {
        _roomView.Visible = !enabled; _hud.Visible = !enabled;
        _actorLayer.Visible = !enabled || !_screen.Dance;
        _actorLayer.Position = enabled ? new(-_screen.ScrollX, -_screen.ScrollY) : new(0, 16);
        Link.Visible = !enabled;
    }
    internal void UpdateNativeCamera(int x, int y)
    {
        if (_screen.Visible) _actorLayer.Position = new(-x, -y);
    }
    internal SeasonsOpeningScreen NativeScreen => _screen;
    internal void SetRoomShake(Vector2 offset) => _roomView.SetNativeScrollOffset(offset);
    internal void SetFlash(bool white) => _overlay.Color = new(1, 1, 1, white ? 1 : 0);
    internal void SetCrystalFlash(bool white) => _actorLayer.Visible = !white;
    private bool HasNeighbor(Vector2 sample) => Phase == SeasonsOpeningPhase.Troupe &&
        sample.Y is >= 0 and < 128 && ((RoomId == 0x97 && sample.X >= 160) || (RoomId == 0x98 && sample.X < 0));
    internal void CheckRoomExit()
    {
        if (Phase != SeasonsOpeningPhase.Troupe || Scrolling) return;
        int direction = RoomId == 0x97 && Link.Position.X > 154 ? 1 : RoomId == 0x98 && Link.Position.X <= 5 ? -1 : 0;
        if (direction == 0) return;
        Link.SetScreenTransitionBoundaryCoordinate(true, direction > 0 ? 154 : 6);
        _pendingExit = direction;
    }
    private void BeginScroll(int direction)
    {
        _scrollStart = new(direction > 0 ? 154 : 6, Link.PrecisePosition.Y); _scrollDirection = direction;
        Texture2D previous = Room.Texture;
        for (int slot = 0; slot < Actors.Length; slot++)
            if (Actors[slot] is { } actor) { _outgoingActors.Add(actor); Actors[slot] = null; }
        LoadRoom(direction > 0 ? 0x98 : 0x97, 0);
        foreach (var actor in Actors) if (actor is { Initialized: false }) actor.AdvanceFrame();
        _roomView.SetRoom(previous); _roomView.StartScreenTransition(Room.Texture, new(direction, 0), 160, Vector2.Zero, Vector2.Zero);
        Link.BeginScrollingTransition(_scrollStart);
        _scrollFrames = 47;
        foreach (var actor in Actors) actor?.Node.SetTransitionDrawOffset(new(direction * 160, 0));
    }
    private void AdvanceScroll()
    {
        _scrollFrames--; int elapsed = 47 - _scrollFrames;
        // Four setup updates, the retained unique-GFX entry, forty scroll
        // updates, and two cleanup updates for these same-tileset small rooms.
        int steps = Math.Clamp(elapsed - 5, 0, 40);
        _roomView.SetTransitionFrame(steps);
        Link.SetScrollingTransitionPosition(_scrollStart + new Vector2(_scrollDirection * steps * 0.375f, 0), new(_scrollDirection * steps * 4, 0));
        foreach (var actor in Actors) actor?.Node.SetTransitionDrawOffset(new(_scrollDirection * (160 - steps * 4), 0));
        foreach (var actor in _outgoingActors) actor.Node.SetTransitionDrawOffset(new(-_scrollDirection * steps * 4, 0));
        if (_scrollFrames != 0) return;
        _roomView.FinishTransition(); Link.FinishScrollingTransition(_scrollStart + new Vector2(_scrollDirection * (15 - 160), 0));
        foreach (var actor in _outgoingActors) { actor.Node.NotifyRetired(); actor.Node.QueueFree(); }
        _outgoingActors.Clear();
    }
    public override void _ExitTree() => _status?.Dispose();
}

internal enum SeasonsOpeningPhase { Pregame, Arrival, Forest, Troupe, Dance, Capture, Imprisonment, TempleSinking, Aftermath, Complete }
