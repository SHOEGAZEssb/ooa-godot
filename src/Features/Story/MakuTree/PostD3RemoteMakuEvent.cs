using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Runs room 0:ba interaction $6b:$06: lightning, the temporary Ambi palace
/// scene, Black Tower explanation stage 1, and the return handoff to dynamic
/// remote Maku interaction $8a:$00/v$04.
/// </summary>
internal sealed class PostD3RemoteMakuEvent :
    IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly PostD3RemoteMakuDatabase _database = new();
    private readonly BlackTowerEntranceEventDatabase _towerDatabase = new();
    private readonly RemoteMakuThirdEssenceEvent _remoteMaku;
    private PostD3RemoteMakuStage _stage;
    private int _counter;
    private int _fadeFrame;
    private int _initialFlashCounter;
    private int _towerFlashCounter;
    private NpcCharacter? _ambi;
    private NpcCharacter? _nayru;
    private BlackTowerExplanationScreen? _towerScreen;
    private Vector2 _fadeOriginalPosition;
    private Vector2 _fadeOriginalSize;
    private int _fadeOriginalZIndex;
    private bool _ownsFade;

    internal PostD3RemoteMakuEvent(
        RoomEventContext context,
        RemoteMakuThirdEssenceEvent remoteMaku)
    {
        _context = context;
        _remoteMaku = remoteMaku;
    }

    public bool HasState => _stage != PostD3RemoteMakuStage.Inactive;
    public bool BlocksGameplay => HasState;
    internal PostD3RemoteMakuStage Stage => _stage;
    internal int Counter => _counter;
    internal int InitialFlashCounter => _initialFlashCounter;
    internal NpcCharacter? Ambi => _ambi;
    internal NpcCharacter? Nayru => _nayru;
    internal BlackTowerExplanationScreen? TowerScreen => _towerScreen;
    internal PostD3RemoteMakuDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room)
    {
        PostD3RemoteMakuRecord record = _database.Record;
        OracleSaveData save = _context.Rooms.SaveData;
        return group == record.Group && room.Id == record.Room &&
            (save.ReadWramByte(0xc6bf) & record.EssenceMask) != 0 &&
            !save.HasRoomFlag(record.Group, record.Room, (byte)record.RoomFlag);
    }

    public void Start(OracleRoomData room)
    {
        if (!Matches(_context.Rooms.ActiveGroup, room))
        {
            throw new InvalidOperationException(
                $"Room {_context.Rooms.ActiveGroup:x}:{room.Id:x2} cannot " +
                "start interaction $6b:$06's post-D3 route.");
        }
        Cancel();
        OwnFade();
        _context.Player.BeginCutsceneControl();
        _counter = _database.Record.InitialWait;
        _stage = PostD3RemoteMakuStage.InitialWait;
    }

    public void UpdateFrame()
    {
        switch (_stage)
        {
            case PostD3RemoteMakuStage.Inactive:
                return;
            case PostD3RemoteMakuStage.InitialWait:
                if (--_counter == 0)
                {
                    _context.Sound.PlaySound(OracleSoundEngine.SndLightning);
                    _initialFlashCounter = 1;
                    _stage = PostD3RemoteMakuStage.InitialFlash;
                }
                return;
            case PostD3RemoteMakuStage.InitialFlash:
                UpdateInitialFlash();
                return;
            case PostD3RemoteMakuStage.PreludeFadeOut:
                if (UpdateFade(fadeOut: true))
                    _stage = PostD3RemoteMakuStage.LoadPalace;
                return;
            case PostD3RemoteMakuStage.LoadPalace:
                LoadPalace();
                return;
            case PostD3RemoteMakuStage.PalaceFadeIn:
                if (UpdateFade(fadeOut: false))
                {
                    _counter = _database.Record.PalaceWait;
                    _stage = PostD3RemoteMakuStage.PalaceWait;
                }
                return;
            case PostD3RemoteMakuStage.PalaceWait:
                if (--_counter == 0)
                {
                    _context.ShowDialogue(_database.Record.PalaceText);
                    _stage = PostD3RemoteMakuStage.PalaceDialogue;
                }
                return;
            case PostD3RemoteMakuStage.PalaceDialogue:
                if (!_context.DialogueOpen)
                {
                    _counter = _database.Record.PalacePostWait;
                    _stage = PostD3RemoteMakuStage.PalacePostWait;
                }
                return;
            case PostD3RemoteMakuStage.PalacePostWait:
                if (--_counter == 0)
                {
                    _fadeFrame = 0;
                    _stage = PostD3RemoteMakuStage.PalaceFadeOut;
                }
                return;
            case PostD3RemoteMakuStage.PalaceFadeOut:
                if (UpdateFade(fadeOut: true))
                    _stage = PostD3RemoteMakuStage.LoadTower;
                return;
            case PostD3RemoteMakuStage.LoadTower:
                LoadTower();
                return;
            case PostD3RemoteMakuStage.TowerFadeIn:
                UpdateTowerEffects();
                if (UpdateFade(fadeOut: false))
                {
                    _counter = _database.Record.ExplanationWait;
                    _stage = PostD3RemoteMakuStage.TowerWait;
                }
                return;
            case PostD3RemoteMakuStage.TowerWait:
                UpdateTowerEffects();
                if (--_counter == 0)
                {
                    _context.ShowDialogue(
                        _database.Record.ExplanationText,
                        textboxFlags: _database.Record.ExplanationTextboxFlags);
                    _stage = PostD3RemoteMakuStage.TowerDialogue;
                }
                return;
            case PostD3RemoteMakuStage.TowerDialogue:
                UpdateTowerEffects();
                if (!_context.DialogueOpen)
                {
                    _counter = _database.Record.ExplanationPostWait;
                    _stage = PostD3RemoteMakuStage.TowerPostWait;
                }
                return;
            case PostD3RemoteMakuStage.TowerPostWait:
                UpdateTowerEffects();
                if (--_counter == 0)
                {
                    _fadeFrame = 0;
                    _stage = PostD3RemoteMakuStage.TowerFadeOut;
                }
                return;
            case PostD3RemoteMakuStage.TowerFadeOut:
                UpdateTowerEffects();
                if (UpdateFade(fadeOut: true))
                    _stage = PostD3RemoteMakuStage.LoadReturnRoom;
                return;
            case PostD3RemoteMakuStage.LoadReturnRoom:
                LoadReturnRoom();
                return;
            case PostD3RemoteMakuStage.ReturnFadeIn:
                if (UpdateFade(fadeOut: false))
                    FinishAndStartRemoteMaku();
                return;
            default:
                throw new InvalidOperationException(
                    $"Unsupported post-D3 stage {_stage}.");
        }
    }

    public void UpdateDuringDialogueFrame()
    {
        if (_stage == PostD3RemoteMakuStage.TowerDialogue)
            UpdateTowerEffects();
    }

    public void Cancel()
    {
        bool controlled = HasState;
        RemoveTowerScreen();
        _ambi?.SetActive(false);
        _nayru?.SetActive(false);
        _ambi = null;
        _nayru = null;
        _context.Player.Visible = true;
        _context.Hud.Visible = true;
        RestoreFade();
        if (controlled)
            _context.Player.EndCutsceneControl();
        _stage = PostD3RemoteMakuStage.Inactive;
        _counter = 0;
        _fadeFrame = 0;
        _initialFlashCounter = 0;
        _towerFlashCounter = 0;
    }

    private void UpdateInitialFlash()
    {
        bool white = _initialFlashCounter is 1 or 2 or 5 or 6 or 9 or 10;
        SetWhiteFade(white ? 1.0f : 0.0f);
        _initialFlashCounter++;
        if (_initialFlashCounter <= _database.Record.FlashFrames)
            return;
        _initialFlashCounter = 0;
        SetWhiteFade(0.0f);
        _fadeFrame = 0;
        _stage = PostD3RemoteMakuStage.PreludeFadeOut;
    }

    private bool UpdateFade(bool fadeOut)
    {
        _fadeFrame++;
        float progress = Math.Min(
            1.0f, _fadeFrame / (float)_database.Record.FadeFrames);
        SetWhiteFade(fadeOut ? progress : 1.0f - progress);
        return _fadeFrame >= _database.Record.FadeFrames;
    }

    private void LoadPalace()
    {
        PostD3RemoteMakuRecord record = _database.Record;
        OracleRoomData loaded = _context.Rooms.LoadCutsceneRoom(
            record.PalaceGroup, record.PalaceRoom);
        _context.RoomView.SetRoom(loaded.Texture);
        _context.Entities.LoadCutsceneRoom(
            record.PalaceGroup, loaded, includeTimePortals: false);
        _context.Transitions.ResetCamera();
        _context.Player.Visible = false;

        _ambi = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            _database.CreateAmbiRecord(), "PostD3Ambi"));
        _nayru = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            _database.CreateNayruRecord(), "PostD3Nayru"));
        _nayru.SetScriptPaletteOverride(_database.PossessedNayruPalette);
        _context.Sound.PlaySound(record.Music);
        _fadeFrame = 0;
        _stage = PostD3RemoteMakuStage.PalaceFadeIn;
    }

    private void LoadTower()
    {
        _context.Entities.LoadCutsceneRoom(
            _context.Rooms.ActiveGroup,
            _context.Rooms.CurrentRoom,
            includeTimePortals: false);
        _ambi = null;
        _nayru = null;
        _towerScreen = new BlackTowerExplanationScreen(_towerDatabase, stage: 1)
        {
            Name = "PostD3BlackTowerExplanationScreen"
        };
        _context.InterfaceLayer.AddChild(_towerScreen);
        _context.Hud.Visible = false;
        _towerFlashCounter = 0;
        _fadeFrame = 0;
        SetWhiteFade(1.0f);
        _stage = PostD3RemoteMakuStage.TowerFadeIn;
    }

    private void UpdateTowerEffects()
    {
        if (_towerFlashCounter != 0)
        {
            bool white = _towerFlashCounter is 1 or 2 or 5 or 6 or 9 or 10;
            _towerScreen!.SetFlashWhite(white);
            _towerFlashCounter++;
            if (_towerFlashCounter > _database.Record.FlashFrames)
            {
                _towerFlashCounter = 0;
                _towerScreen.SetFlashWhite(false);
            }
            return;
        }
        if ((_context.Entities.FrameCounter & 0x1f) != 0 ||
            (_context.Entities.NextRandomValue() & 0x07) != 0)
        {
            return;
        }
        _towerFlashCounter = 1;
        _context.Sound.PlaySound(OracleSoundEngine.SndLightning);
    }

    private void LoadReturnRoom()
    {
        PostD3RemoteMakuRecord record = _database.Record;
        RemoveTowerScreen();
        OracleRoomData loaded = _context.Rooms.LoadCutsceneRoom(
            record.Group, record.Room);
        _context.RoomView.SetRoom(loaded.Texture);
        _context.Entities.LoadCutsceneRoom(
            record.Group, loaded, includeTimePortals: false);
        _context.Transitions.ResetCamera();
        _context.Player.Visible = true;
        _context.Player.WarpTo(
            new Vector2(record.ReturnX, record.ReturnY), recordSafe: false);
        _context.Player.Face(record.ReturnDirection switch
        {
            0 => Vector2I.Up,
            1 => Vector2I.Right,
            2 => Vector2I.Down,
            3 => Vector2I.Left,
            _ => throw new InvalidOperationException(
                $"Unsupported Link direction ${record.ReturnDirection:x2}.")
        });
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        _context.Hud.Visible = true;
        if (!_remoteMaku.PrepareAfterPostD3(
                record.PastFlagGroup,
                record.PastFlagRoom,
                record.PastRoomFlag,
                record.StandardGlobalFlag))
        {
            throw new InvalidOperationException(
                "Dynamic remote Maku $8a:$00/v$04 could not initialize in " +
                "room 0:ba after the post-D3 return.");
        }
        _fadeFrame = 0;
        SetWhiteFade(1.0f);
        _stage = PostD3RemoteMakuStage.ReturnFadeIn;
    }

    private void FinishAndStartRemoteMaku()
    {
        RestoreFade();
        _stage = PostD3RemoteMakuStage.Inactive;
        _remoteMaku.StartPrepared();
        // The global cutscene handler reaches updateAllObjects after state 7,
        // so the new interaction runs its first script update in this same
        // 60 Hz update and immediately reasserts the input lock/Maku music.
        _remoteMaku.UpdateFrame();
    }

    private void OwnFade()
    {
        if (_ownsFade)
            return;
        _ownsFade = true;
        _fadeOriginalPosition = _context.Fade.Position;
        _fadeOriginalSize = _context.Fade.Size;
        _fadeOriginalZIndex = _context.Fade.ZIndex;
        _context.Fade.Position = Vector2.Zero;
        _context.Fade.Size = new Vector2(
            OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight);
        _context.Fade.ZIndex = _context.Hud.ZIndex + 1;
        SetWhiteFade(0.0f);
    }

    private void RestoreFade()
    {
        SetWhiteFade(0.0f);
        if (!_ownsFade)
            return;
        _context.Fade.Position = _fadeOriginalPosition;
        _context.Fade.Size = _fadeOriginalSize;
        _context.Fade.ZIndex = _fadeOriginalZIndex;
        _ownsFade = false;
    }

    private void RemoveTowerScreen()
    {
        if (_towerScreen is null)
            return;
        if (_towerScreen.GetParent() == _context.InterfaceLayer)
            _context.InterfaceLayer.RemoveChild(_towerScreen);
        _towerScreen.QueueFree();
        _towerScreen = null;
    }

    private void SetWhiteFade(float alpha) =>
        _context.Fade.Color = new Color(
            1, 1, 1, Mathf.Clamp(alpha, 0.0f, 1.0f));
}

internal enum PostD3RemoteMakuStage
{
    Inactive,
    InitialWait,
    InitialFlash,
    PreludeFadeOut,
    LoadPalace,
    PalaceFadeIn,
    PalaceWait,
    PalaceDialogue,
    PalacePostWait,
    PalaceFadeOut,
    LoadTower,
    TowerFadeIn,
    TowerWait,
    TowerDialogue,
    TowerPostWait,
    TowerFadeOut,
    LoadReturnRoom,
    ReturnFadeIn
}
