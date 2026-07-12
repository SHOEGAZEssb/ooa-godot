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

    public override void _Draw()
    {
        if (_current is null)
            return;

        if (_previous is null)
        {
            DrawTexture(_current, Vector2.Zero);
            return;
        }

        float scrollPixels = Mathf.Min(Mathf.Floor(_transitionFrame) * 4.0f, _transitionDistance);
        Vector2 scroll = new(_direction.X * scrollPixels, _direction.Y * scrollPixels);
        Vector2 distance = new(
            _direction.X * _transitionDistance,
            _direction.Y * _transitionDistance);
        DrawTexture(_previous, -scroll);
        DrawTexture(_current, _transitionTextureOffset + distance - scroll);
    }
}
