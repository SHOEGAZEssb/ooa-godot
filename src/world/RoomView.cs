using Godot;

namespace oracleofages;

public partial class RoomView : Node2D
{
    private Texture2D? _current;
    private Texture2D? _previous;
    private Vector2I _direction;
    private Vector2 _transitionTextureOffset;
    private float _transitionDistance;
    private float _transitionFrame;
    private int _transitionFrames;
    private bool _waveActive;
    private int _waveAmplitude;
    private int _wavePhase;
    private Color _backgroundFade = new(0, 0, 0, 0);

    private static readonly int[] WaveQuarter =
    {
        0x00, 0x0d, 0x19, 0x26, 0x32, 0x3e, 0x4a, 0x56,
        0x62, 0x6d, 0x79, 0x84, 0x8e, 0x98, 0xa2, 0xac,
        0xb5, 0xbe, 0xc6, 0xce, 0xd5, 0xdc, 0xe2, 0xe7,
        0xed, 0xf1, 0xf5, 0xf8, 0xfb, 0xfd, 0xff, 0xff
    };

    public bool IsTransitioning => _previous is not null;

    public void SetRoom(Texture2D texture)
    {
        _current = texture;
        _previous = null;
        _transitionFrame = 0.0f;
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

    public void SetHorizontalWave(int amplitude, int phase)
    {
        _waveActive = true;
        _waveAmplitude = Mathf.Clamp(amplitude, 0, 0xff);
        _wavePhase = phase & 0x7f;
        QueueRedraw();
    }

    public void ClearHorizontalWave()
    {
        _waveActive = false;
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

    internal float BackgroundFadeAlpha => _backgroundFade.A;

    public override void _Draw()
    {
        if (_current is null)
            return;

        if (_previous is null)
        {
            if (_waveActive)
                DrawWavedTexture(_current);
            else
                DrawTexture(_current, Vector2.Zero);
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

    private void DrawBackgroundFade()
    {
        if (_backgroundFade.A <= 0.0f || _current is null)
            return;
        DrawRect(
            new Rect2(Vector2.Zero, new Vector2(_current.GetWidth(), _current.GetHeight())),
            _backgroundFade);
    }

    private void DrawWavedTexture(Texture2D texture)
    {
        int width = texture.GetWidth();
        int height = texture.GetHeight();
        for (int y = 0; y < height; y++)
        {
            int offset = WaveOffsetForValidation(_waveAmplitude, _wavePhase + y);
            Rect2 source = new(0, y, width, 1);
            Rect2 target = new(-offset, y, width, 1);
            DrawTextureRectRegion(texture, target, source);
            DrawTextureRectRegion(texture, new Rect2(target.Position + new Vector2(width, 0), target.Size), source);
            DrawTextureRectRegion(texture, new Rect2(target.Position - new Vector2(width, 0), target.Size), source);
        }
    }

    internal static int WaveOffsetForValidation(int amplitude, int index)
    {
        int phase = index & 0x7f;
        int quarterIndex = phase & 0x1f;
        if ((phase & 0x20) != 0)
            quarterIndex = 0x1f - quarterIndex;
        int value = (WaveQuarter[quarterIndex] * Mathf.Clamp(amplitude, 0, 0xff)) >> 8;
        if ((phase & 0x40) != 0)
            value = -value;
        return value;
    }
}
