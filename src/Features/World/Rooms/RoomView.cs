using Godot;

namespace Oracles;

public partial class RoomView : Node2D
{
    private Texture2D? _current;
    private bool _nativeTilemap;
    private int _nativeScrollY;
    private Vector2 _nativeBackgroundOffset;
    private Vector2 _nativePresentationOffset;
    internal void SetNativeFullScreenPresentation(bool fullScreen)
    {
        Vector2 offset=fullScreen?new(0,-OracleRoomData.GameplayScreenTop):Vector2.Zero;
        if(_nativePresentationOffset==offset)return;
        _nativePresentationOffset=offset;QueueRedraw();
    }
    internal void SetNativeScrollOffset(Vector2 offset){_nativeBackgroundOffset=-offset;QueueRedraw();}
    internal void SetNativeScrollY(int value){_nativeScrollY=value&255;QueueRedraw();}
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
        QueueRedraw();
    }

    public void SetRoom(Texture2D texture)
    {
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
        QueueRedraw();
    }
    internal void SetNativeTilemap(Texture2D texture)
    {
        int scrollY=_nativeScrollY;SetRoom(texture);_nativeTilemap=true;_nativeScrollY=scrollY;
    }

    internal void SuspendUnderwaterWave(bool suspended)
    {
        _underwaterWaveSuspended=suspended;
        if(suspended){_underwaterWaveActive=false;_underwaterBackground=null;}
        QueueRedraw();
    }

    public void StartScreenTransition(
        Texture2D texture,
        Vector2I direction,
        float distance,
        Vector2 sourceCameraOrigin,
        Vector2 destinationCameraOrigin)
    {
        _previous = _current;
        _underwaterWaveActive = false;
        _underwaterBackground = null;
        _current = texture;
        _direction = direction;
        _transitionDistance = distance;
        _transitionTextureOffset = sourceCameraOrigin - destinationCameraOrigin;
        _transitionFrame = 0.0f;
        _transitionFrames = Mathf.Max(1, Mathf.RoundToInt(distance / 4.0f));
        QueueRedraw();
    }

    public void SetTransitionFrame(float frame)
    {
        if (_previous is null)
            return;

        _transitionFrame = Mathf.Clamp(frame, 0.0f, _transitionFrames);
        QueueRedraw();
    }

    public void FinishTransition()
    {
        _previous = null;
        _transitionFrame = 0.0f;
        QueueRedraw();
    }

    public void SetHorizontalWave(int amplitude, int phase, int step = 1, bool normalEvenSamples = false,
        Vector2 cameraOrigin = default, Texture2D? background = null)
    {
        _waveActive = true;
        _waveAmplitude = Mathf.Clamp(amplitude, 0, 0xff);
        _wavePhase = (phase - (int)cameraOrigin.Y * step) & 0x7f;
        _waveStep = step;
        _waveNormalEvenSamples = normalEvenSamples;
        _waveCameraX = (int)cameraOrigin.X;
        _waveBackground = background;
        QueueRedraw();
    }

    public void ClearHorizontalWave()
    {
        _waveActive = false;
        _waveBackground = null;
        QueueRedraw();
    }

    public void SetBackgroundFade(Color color, float alpha)
    {
        _backgroundFade = new Color(
            color.R, color.G, color.B, Mathf.Clamp(alpha, 0.0f, 1.0f));
        QueueRedraw();
    }

    public void ClearBackgroundFade()
    {
        _backgroundFade = new Color(0, 0, 0, 0);
        QueueRedraw();
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
        QueueRedraw();
    }

    public void SetRoomLoadRevealLoadedColumns(int loadedColumns)
    {
        if (!_roomLoadColumnReveal)
            return;
        _roomLoadRevealLoadedColumns = Mathf.Clamp(
            loadedColumns,
            0,
            RoomTransitionController.RoomLoadRevealColumnUpdates);
        QueueRedraw();
    }

    public void ClearRoomLoadColumnReveal()
    {
        _roomLoadColumnReveal = false;
        _roomLoadRevealLoadedColumns = 0;
        _roomLoadClearedTilemap = null;
        QueueRedraw();
    }

    internal float BackgroundFadeAlpha => _backgroundFade.A;
    internal Color BackgroundFadeColorForValidation => _backgroundFade;
    internal bool RoomLoadColumnRevealActive => _roomLoadColumnReveal;
    internal int RoomLoadRevealLoadedColumns => _roomLoadRevealLoadedColumns;
    internal Texture2D? RoomLoadClearedTilemap => _roomLoadClearedTilemap;

    public override void _Draw()
    {
        if (_current is null)
            return;

        if(_nativeTilemap)
        {
            // Native fullscreen SCY=$00 includes the rows above the room.
            // World coordinates retain the ordinary gameplay-field origin.
            int first=Mathf.Min(144,256-_nativeScrollY);
            DrawTextureRectRegion(_current,new Rect2(0,-OracleRoomData.GameplayScreenTop,160,first),new Rect2(0,_nativeScrollY,160,first));
            if(first<144)DrawTextureRectRegion(_current,new Rect2(0,first-OracleRoomData.GameplayScreenTop,160,144-first),new Rect2(0,0,160,144-first));
            return;
        }

        if (_previous is null)
        {
            if (_roomLoadColumnReveal && _roomLoadClearedTilemap is not null)
                DrawRoomLoadColumnReveal(_current, _roomLoadClearedTilemap);
            else if (_waveActive)
                DrawWavedTexture(_waveBackground ?? _current, _waveAmplitude, _wavePhase, _waveStep, _waveNormalEvenSamples, _waveCameraX);
            else if (_underwaterWaveActive && _underwaterBackground is not null)
                DrawWavedTexture(_underwaterBackground, 0x02, _underwaterWavePhase);
            else
                DrawTexture(_current, _nativeBackgroundOffset+_nativePresentationOffset);
            DrawBackgroundFade();
            return;
        }

        float scrollPixels = Mathf.Min(Mathf.Floor(_transitionFrame) * 4.0f, _transitionDistance);
        Vector2 scroll = new(_direction.X * scrollPixels, _direction.Y * scrollPixels);
        Vector2 distance = new(
            _direction.X * _transitionDistance,
            _direction.Y * _transitionDistance);
        DrawTexture(_previous, -scroll);
        DrawTexture(_current, _transitionTextureOffset + distance - scroll);
        DrawBackgroundFade();
    }

    private void DrawRoomLoadColumnReveal(
        Texture2D texture,
        Texture2D clearedTilemap)
    {
        DrawTextureRect(clearedTilemap, new Rect2(Vector2.Zero, texture.GetSize()), tile: true);
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
        DrawTextureRectRegion(texture, revealed, revealed);
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

    private void DrawWavedTexture(Texture2D texture, int amplitude, int phase,
        int step = 1, bool normalEvenSamples = false, int cameraX = 0)
    {
        int width = texture.GetWidth();
        int height = texture.GetHeight();
        for (int y = 0; y < height; y++)
        {
            int sample = phase + y * step;
            int offset = normalEvenSamples && (sample & 1) == 0 ? 0 : ScreenWaveData.Shared.Offset(amplitude, sample) - cameraX;
            Rect2 source = new(0, y, width, 1);
            Rect2 target = new(-offset, y, width, 1);
            DrawTextureRectRegion(texture, target, source);
            DrawTextureRectRegion(texture, new Rect2(target.Position + new Vector2(width, 0), target.Size), source);
            DrawTextureRectRegion(texture, new Rect2(target.Position - new Vector2(width, 0), target.Size), source);
        }
    }

    internal static int WaveOffsetForValidation(int amplitude, int index)
        => ScreenWaveData.Shared.Offset(amplitude, index);
}
