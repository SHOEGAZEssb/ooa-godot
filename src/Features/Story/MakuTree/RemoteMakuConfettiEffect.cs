using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native INTERAC_MAKU_CONFETTI $62:$00 and the $84:$02 sparkles it leaves
/// behind. Positions and component speeds retain the original signed 8.8
/// arithmetic and slot-order update boundaries.
/// </summary>
internal sealed partial class RemoteMakuConfettiEffect : Node2D
{
    private readonly List<ConfettiPiece> _pieces = new();
    private readonly List<Sparkle> _sparkles = new();
    private RemoteMakuFirstEssenceRecord _record;
    private RemoteMakuFirstEssenceDatabase _database = null!;
    private OracleSoundEngine _sound = null!;
    private Vector2 _cameraOrigin;
    private int _frame;
    private int _spawnedPieces;
    private int _spawnDelay;
    private bool _spawnerInitialized;
    private bool _spawnerActive = true;

    internal int SpawnedPieces => _spawnedPieces;
    internal int LivePieces => _pieces.Count;
    internal int LiveSparkles => _sparkles.Count;
    internal bool Finished =>
        !_spawnerActive && _pieces.Count == 0 && _sparkles.Count == 0;
    internal IReadOnlyList<Vector2> PiecePositions
    {
        get
        {
            var result = new Vector2[_pieces.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = _pieces[index].Position;
            return result;
        }
    }

    internal void Initialize(
        RemoteMakuFirstEssenceDatabase database,
        OracleSoundEngine sound,
        Vector2 cameraOrigin)
    {
        _database = database;
        _record = database.Record;
        _sound = sound;
        _cameraOrigin = cameraOrigin;
        ZIndex = NpcCharacter.BehindLinkZIndex;
    }

    internal void UpdateFrame()
    {
        _frame++;
        UpdateSpawner();

        for (int index = 0; index < _pieces.Count; index++)
        {
            ConfettiPiece piece = _pieces[index];
            if (piece.BornFrame == _frame)
                continue;
            if (!UpdatePiece(piece))
            {
                _pieces.RemoveAt(index);
                index--;
            }
        }

        for (int index = 0; index < _sparkles.Count; index++)
        {
            Sparkle sparkle = _sparkles[index];
            if (sparkle.BornFrame == _frame)
                continue;
            // sparkle.s tests the previous frame's terminal parameter before
            // calling interactionAnimate.
            if (sparkle.Animation.CurrentParameter == 0xff)
            {
                _sparkles.RemoveAt(index);
                index--;
                continue;
            }
            sparkle.Animation.Advance();
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (ConfettiPiece piece in _pieces)
        {
            DrawTexture(
                piece.Animation.CurrentTexture,
                piece.Position - new Vector2(16, 16));
        }
        foreach (Sparkle sparkle in _sparkles)
        {
            DrawTexture(
                sparkle.Animation.CurrentTexture,
                sparkle.Position - new Vector2(16, 16));
        }
    }

    private void UpdateSpawner()
    {
        if (!_spawnerActive)
            return;
        if (!_spawnerInitialized)
        {
            _spawnerInitialized = true;
            _spawnDelay = _record.SpawnDelays[0];
            return;
        }
        if (--_spawnDelay != 0)
            return;

        SpawnPiece(_spawnedPieces);
        _spawnedPieces++;
        _sound.PlaySound(_record.Sound);
        if (_spawnedPieces >= _record.ConfettiPieces)
        {
            _spawnerActive = false;
            return;
        }
        _spawnDelay = _record.SpawnDelays[_spawnedPieces];
    }

    private void SpawnPiece(int index)
    {
        RemoteMakuConfettiPieceRecord source = _record.Pieces[index];
        RemoteMakuVisualRecord left = _database.Visual("confetti-left");
        RemoteMakuVisualRecord right = _database.Visual("confetti-right");
        var animation = new EnemyAnimationPlayer(this, 2);
        animation.Load(
            EnemyVisualSource.LoadComposite([left.Sprite]),
            [left.Animation, right.Animation],
            left.TileBase,
            left.Palette);
        animation.SetAnimation(0);
        _pieces.Add(new ConfettiPiece(
            animation,
            _frame,
            ToFixed(_cameraOrigin.Y + source.Y),
            ToFixed(_cameraOrigin.X + source.X),
            source.AccelerationY,
            source.AccelerationX,
            _record.SoundCounter,
            _record.SparkleInitialDelay));
    }

    private bool UpdatePiece(ConfettiPiece piece)
    {
        if (--piece.SoundCounter == 0)
        {
            piece.SoundCounter = _record.SoundCounter;
            _sound.PlaySound(_record.Sound);
        }
        if (--piece.SparkleCounter == 0)
        {
            piece.SparkleCounter = _record.SparkleRepeatDelay;
            SpawnSparkle(piece.Position);
        }

        piece.YFixed = AddWord(piece.YFixed, _record.YOffsetFixed);
        piece.SpeedY = AddWord(piece.SpeedY, piece.AccelerationY);
        piece.SpeedX = AddWord(piece.SpeedX, piece.AccelerationX);
        piece.YFixed = AddWord(piece.YFixed, piece.SpeedY);
        piece.XFixed = AddWord(piece.XFixed, piece.SpeedX);

        int yHigh = (byte)(piece.YFixed >> 8);
        if (yHigh >= _record.DeleteY && yHigh < 0xd8)
            return false;

        if (AbsoluteWord(piece.SpeedY) >= _record.YSpeedLimit)
            piece.AccelerationY = NegateWord(piece.AccelerationY);
        if (AbsoluteWord(piece.SpeedX) >= _record.XSpeedLimit)
            piece.AccelerationX = NegateWord(piece.AccelerationX);

        bool movingLeft = piece.SpeedX < 0;
        if ((movingLeft && piece.Direction == 0) ||
            (!movingLeft && piece.Direction != 0))
        {
            piece.Direction ^= 1;
            piece.Animation.SetAnimation(piece.Direction);
        }
        return true;
    }

    private void SpawnSparkle(Vector2 position)
    {
        RemoteMakuVisualRecord visual = _database.Visual("sparkle");
        var animation = new EnemyAnimationPlayer(this, 1);
        animation.Load(
            EnemyVisualSource.LoadComposite([visual.Sprite]),
            [visual.Animation],
            visual.TileBase,
            visual.Palette);
        animation.SetAnimation(0);
        _sparkles.Add(new Sparkle(animation, position, _frame));
    }

    private static int ToFixed(float value) =>
        unchecked((short)(Mathf.RoundToInt(value) << 8));

    private static int AddWord(int left, int right) =>
        unchecked((short)(left + right));

    private static int NegateWord(int value) =>
        unchecked((short)-value);

    private static int AbsoluteWord(int value) =>
        value == short.MinValue ? 0x8000 : Math.Abs(value);

    private sealed class ConfettiPiece(
        EnemyAnimationPlayer animation,
        int bornFrame,
        int yFixed,
        int xFixed,
        int accelerationY,
        int accelerationX,
        int soundCounter,
        int sparkleCounter)
    {
        internal EnemyAnimationPlayer Animation { get; } = animation;
        internal int BornFrame { get; } = bornFrame;
        internal int YFixed { get; set; } = yFixed;
        internal int XFixed { get; set; } = xFixed;
        internal int SpeedY { get; set; }
        internal int SpeedX { get; set; }
        internal int AccelerationY { get; set; } = accelerationY;
        internal int AccelerationX { get; set; } = accelerationX;
        internal int SoundCounter { get; set; } = soundCounter;
        internal int SparkleCounter { get; set; } = sparkleCounter;
        internal int Direction { get; set; }
        internal Vector2 Position => new(
            unchecked((short)XFixed) >> 8,
            unchecked((short)YFixed) >> 8);
    }

    private sealed record Sparkle(
        EnemyAnimationPlayer Animation,
        Vector2 Position,
        int BornFrame);
}
