
namespace Oracles;

public partial class RoomView : Node2D
{
    private OracleRoomData? _room, _previousRoom;
    private RoomPriorityView? _priorityView;
    public override void _Ready() => _priorityView = GetNodeOrNull<RoomPriorityView>("Priority");
    private void RedrawRoom()
    {
        QueueRedraw();
        _priorityView?.QueueRedraw();
    }
    private Texture2D? _current;
    private bool _nativeTilemap;
    private int _nativeScrollY;
    private Vector2 _nativeBackgroundOffset;
    private Vector2 _nativePresentationOffset;
    internal void SetNativeFullScreenPresentation(bool fullScreen)
    {
        Vector2 offset=fullScreen?new(0,-OracleRoomData.GameplayScreenTop):Vector2.Zero;
        if(_nativePresentationOffset==offset)return;
        _nativePresentationOffset=offset;RedrawRoom();
    }
    internal void SetNativeScrollOffset(Vector2 offset){_nativeBackgroundOffset=-offset;RedrawRoom();}
    internal void SetNativeScrollY(int value){_nativeScrollY=value&255;RedrawRoom();}
    private Texture2D? _previous;
    private Vector2I _direction;
    private Vector2 _transitionTextureOffset;
    private float _transitionDistance;
    private float _transitionFrame;
    private int _transitionFrames;
    private bool _waveActive;
    private int _waveAmplitude;
    private int _wavePhase;
    private int _waveStep = 1;
    private bool _waveNormalEvenSamples;
    private bool _waveInvertEvenSamples;
    private int _waveCameraX;
    private Texture2D? _waveBackground;
    private bool _underwaterWaveActive;
    private bool _underwaterWaveSuspended;
    private int _underwaterWavePhase;
    private Texture2D? _underwaterBackground;
    private Color _backgroundFade = new(0, 0, 0, 0);
    private bool _roomLoadColumnReveal;
    private int _roomLoadRevealLoadedColumns;
    private Texture2D? _roomLoadClearedTilemap;

    public bool IsTransitioning => _previous is not null;
    internal bool UnderwaterWaveActive => _underwaterWaveActive;
    internal int UnderwaterWavePhase => _underwaterWavePhase;

    internal void UpdateUnderwaterWave(OracleRoomData room, int frameCounter)
    {
        // underwaterWaves.s: amplitude $02, phase (SCY + wFrameCounter) & $7f.
        // updateGfxRegs2Scroll subtracts $10 from camera Y for the HUD. Drawing
        // in world coordinates already supplies camera Y through the row index.
        bool active = !_underwaterWaveSuspended && (room.TilesetFlags & (int)TilesetFlags.Underwater) != 0;
        if (!active && !_underwaterWaveActive) return;
        _underwaterWaveActive = active;
        _underwaterBackground = _underwaterWaveActive ? room.UnderwaterBackgroundTexture : null;
        _underwaterWavePhase = (frameCounter - OracleRoomData.StatusBarHeight) & 0x7f;
        RedrawRoom();
    }

    public void SetRoom(OracleRoomData room)
    {
        SetRoom(room.Texture);
        _room = room;
    }

    public void SetRoom(Texture2D texture)
    {
        _room = _previousRoom = null;
        _nativeTilemap=false;_nativeScrollY=0;_nativeBackgroundOffset=Vector2.Zero;
        _current = texture;
        _underwaterWaveActive = false;
        _underwaterWaveSuspended = false;
        _underwaterBackground = null;
        _previous = null;
        _transitionFrame = 0.0f;
        _roomLoadColumnReveal = false;
        _roomLoadRevealLoadedColumns = 0;
        _roomLoadClearedTilemap = null;
        RedrawRoom();
    }
    internal void SetNativeTilemap(Texture2D texture)
    {
        int scrollY=_nativeScrollY;SetRoom(texture);_nativeTilemap=true;_nativeScrollY=scrollY;
    }

    internal void SuspendUnderwaterWave(bool suspended)
    {
        _underwaterWaveSuspended=suspended;
        if(suspended){_underwaterWaveActive=false;_underwaterBackground=null;}
        RedrawRoom();
    }

    public void StartScreenTransition(
        OracleRoomData room,
        Vector2I direction,
        float distance,
        Vector2 sourceCameraOrigin,
        Vector2 destinationCameraOrigin)
    {
        _previous = _current;
        _previousRoom = _room;
        _room = room;
        _underwaterWaveActive = false;
        _underwaterBackground = null;
        _current = room.Texture;
        _direction = direction;
        _transitionDistance = distance;
        _transitionTextureOffset = sourceCameraOrigin - destinationCameraOrigin;
        _transitionFrame = 0.0f;
        _transitionFrames = Mathf.Max(1, Mathf.RoundToInt(distance / 4.0f));
        RedrawRoom();
    }

    public void SetTransitionFrame(float frame)
    {
        if (_previous is null)
            return;

        _transitionFrame = Mathf.Clamp(frame, 0.0f, _transitionFrames);
        RedrawRoom();
    }

    public void FinishTransition()
    {
        _previous = null;
        _previousRoom = null;
        _transitionFrame = 0.0f;
        RedrawRoom();
    }

    public void SetHorizontalWave(int amplitude, int phase, int step = 1, bool normalEvenSamples = false,
        Vector2 cameraOrigin = default, Texture2D? background = null, bool invertEvenSamples = false)
    {
        _waveActive = true;
        _waveAmplitude = Mathf.Clamp(amplitude, 0, 0xff);
        _wavePhase = (phase - (int)cameraOrigin.Y * step) & 0x7f;
        _waveStep = step;
        _waveNormalEvenSamples = normalEvenSamples;
        _waveInvertEvenSamples = invertEvenSamples;
        _waveCameraX = (int)cameraOrigin.X;
        _waveBackground = background;
        RedrawRoom();
    }

    public void ClearHorizontalWave()
    {
        _waveActive = false;
        _waveBackground = null;
        RedrawRoom();
    }

    public void SetBackgroundFade(Color color, float alpha)
    {
        _backgroundFade = new Color(
            color.R, color.G, color.B, Mathf.Clamp(alpha, 0.0f, 1.0f));
        RedrawRoom();
    }

    public void ClearBackgroundFade()
    {
        _backgroundFade = new Color(0, 0, 0, 0);
        RedrawRoom();
    }

    public void SetRoomLoadColumnReveal(
        Texture2D clearedTilemap,
        int loadedColumns)
    {
        _roomLoadColumnReveal = true;
        _roomLoadClearedTilemap = clearedTilemap;
        _roomLoadRevealLoadedColumns = Mathf.Clamp(
            loadedColumns,
            0,
            RoomTransitionController.RoomLoadRevealColumnUpdates);
        RedrawRoom();
    }

    public void SetRoomLoadRevealLoadedColumns(int loadedColumns)
    {
        if (!_roomLoadColumnReveal)
            return;
        _roomLoadRevealLoadedColumns = Mathf.Clamp(
            loadedColumns,
            0,
            RoomTransitionController.RoomLoadRevealColumnUpdates);
        RedrawRoom();
    }

    public void ClearRoomLoadColumnReveal()
    {
        _roomLoadColumnReveal = false;
        _roomLoadRevealLoadedColumns = 0;
        _roomLoadClearedTilemap = null;
        RedrawRoom();
    }

    internal float BackgroundFadeAlpha => _backgroundFade.A;
    internal Color BackgroundFadeColor => _backgroundFade;
    internal bool RoomLoadColumnRevealActive => _roomLoadColumnReveal;
    internal int RoomLoadRevealLoadedColumns => _roomLoadRevealLoadedColumns;
    internal Texture2D? RoomLoadClearedTilemap => _roomLoadClearedTilemap;

    public override void _Draw() => DrawRoom(this, priority: false);

    internal void DrawRoom(Node2D canvas, bool priority)
    {
        if (_current is null)
            return;
        Texture2D? current = priority ? _room?.GetPriorityTexture() : _current;
        if (current is null) return;

        if(_nativeTilemap)
        {
            // Native fullscreen SCY=$00 includes the rows above the room.
            // World coordinates retain the ordinary gameplay-field origin.
            int first=Mathf.Min(144,256-_nativeScrollY);
            canvas.DrawTextureRectRegion(current,new Rect2(0,-OracleRoomData.GameplayScreenTop,160,first),new Rect2(0,_nativeScrollY,160,first));
            if(first<144)canvas.DrawTextureRectRegion(current,new Rect2(0,first-OracleRoomData.GameplayScreenTop,160,144-first),new Rect2(0,0,160,144-first));
            return;
        }

        if (_previous is null)
        {
            if (_roomLoadColumnReveal && _roomLoadClearedTilemap is not null)
                DrawRoomLoadColumnReveal(canvas, current, priority ? null : _roomLoadClearedTilemap);
            else if (_waveActive)
                DrawWavedTexture(canvas, priority ? _room!.GetPriorityTexture(_waveBackground is not null) : _waveBackground ?? current, _waveAmplitude, _wavePhase, _waveStep, _waveNormalEvenSamples, _waveCameraX, _waveInvertEvenSamples);
            else if (_underwaterWaveActive && _underwaterBackground is not null)
                DrawWavedTexture(canvas, priority ? _room!.GetPriorityTexture(true) : _underwaterBackground, 0x02, _underwaterWavePhase);
            else
                canvas.DrawTexture(current, _nativeBackgroundOffset+_nativePresentationOffset);
            if (!priority) DrawBackgroundFade();
            return;
        }

        float scrollPixels = Mathf.Min(Mathf.Floor(_transitionFrame) * 4.0f, _transitionDistance);
        Vector2 scroll = new(_direction.X * scrollPixels, _direction.Y * scrollPixels);
        Vector2 distance = new(
            _direction.X * _transitionDistance,
            _direction.Y * _transitionDistance);
        Texture2D? previous = priority ? _previousRoom?.GetPriorityTexture() : _previous;
        if (previous is not null) canvas.DrawTexture(previous, -scroll);
        canvas.DrawTexture(current, _transitionTextureOffset + distance - scroll);
        if (!priority) DrawBackgroundFade();
    }

    private void DrawRoomLoadColumnReveal(
        Node2D canvas,
        Texture2D texture,
        Texture2D? clearedTilemap)
    {
        if (clearedTilemap is not null)
            canvas.DrawTextureRect(clearedTilemap, new Rect2(Vector2.Zero, texture.GetSize()), tile: true);
        (int left, int right) =
            RoomTransitionController.RoomLoadRevealBounds(
                _roomLoadRevealLoadedColumns);
        if (right <= left)
            return;

        var revealed = new Rect2(
            left,
            0,
            right - left,
            texture.GetHeight());
        canvas.DrawTextureRectRegion(texture, revealed, revealed);
    }

    private void DrawBackgroundFade()
    {
        if (_backgroundFade.A <= 0.0f || _current is null)
            return;
        DrawRect(
            new Rect2(_nativePresentationOffset, new Vector2(_current.GetWidth(),
                Mathf.Max(_current.GetHeight(), OracleRoomData.ViewportHeight - _nativePresentationOffset.Y))),
            _backgroundFade);
    }

    private void DrawWavedTexture(Node2D canvas, Texture2D texture, int amplitude, int phase,
        int step = 1, bool normalEvenSamples = false, int cameraX = 0, bool invertEvenSamples = false)
    {
        int width = texture.GetWidth();
        int height = texture.GetHeight();
        for (int y = 0; y < height; y++)
        {
            int sample = phase + y * step;
            int offset = normalEvenSamples && (sample & 1) == 0 ? 0 : ScreenWaveData.Shared.Offset(amplitude, sample) - cameraX;
            if (invertEvenSamples && (sample & 1) == 0) offset = -offset;
            Rect2 source = new(0, y, width, 1);
            Rect2 target = new(-offset, y, width, 1);
            canvas.DrawTextureRectRegion(texture, target, source);
            canvas.DrawTextureRectRegion(texture, new Rect2(target.Position + new Vector2(width, 0), target.Size), source);
            canvas.DrawTextureRectRegion(texture, new Rect2(target.Position - new Vector2(width, 0), target.Size), source);
        }
    }

    internal static int WaveOffsetForValidation(int amplitude, int index)
        => ScreenWaveData.Shared.Offset(amplitude, index);
}
