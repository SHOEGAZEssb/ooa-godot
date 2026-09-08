using Godot;

namespace oracleofages;

public partial class EmberSeedEffect
{
    private Texture2D[][] _galeTextures = [];
    private int _galePalette;
    private bool _galeLanded;
    private int _galeSubstate;
    private int _galeCounter2;
    private bool _galeCollisionPending;
    internal Player? GalePlayer { get; set; }
    internal bool GaleMenuRequested { get; private set; }
    internal int GaleSubstate => _galeSubstate;
    internal int GaleCounter2 => _galeCounter2;
    internal int GalePalette => _galePalette;

    private void InitializeGaleTextures(Image source)
    {
        _galeTextures = new Texture2D[4][];
        for (int palette = 0; palette < 4; palette++)
            _galeTextures[palette] = BuildTextures(source,
                _record.FlameTileBase, palette, _effectFrames);
    }

    private void BeginGale(bool landed, bool wall = false)
    {
        _state = EmberState.Gale;
        _collisionEnabled = false;
        _galeLanded = landed;
        _galePalette = _record.FlameOamFlags;
        _flameCounter = landed ? _record.FlameCounter :
            wall ? 0x1e : _record.CollisionEffectCounter;
        _playSound(_record.FlameSound);
        QueueRedraw();
    }

    private void AnimateGale()
    {
        AdvanceAnimation();
        if ((_flameCounter & 3) == 0)
            _galePalette = (_galePalette + 1) & 0x0b;
        QueueRedraw();
    }

    private void UpdateGaleCounter()
    {
        AnimateGale();
        _flameCounter = (_flameCounter - 1) & 0xff;
        if (_flameCounter == 0) Finish();
        else if (_flameCounter < 0x14) Visible = !Visible;
    }

    private void UpdateGale()
    {
        if (_galeCollisionPending)
        {
            _galeCollisionPending = false;
            return;
        }
        if (!_galeLanded)
        {
            UpdateGaleCounter();
            return;
        }
        // galeSeedTryToWarpLink animates once before dispatch, then again
        // when substate 0 fails a capture predicate. Do not consolidate it.
        AnimateGale();
        switch (_galeSubstate)
        {
            case 0:
                if ((_room.TilesetFlags & 1) == 0)
                {
                    _galeSubstate = 3;
                    return;
                }
                if (GalePlayer is not { CanBeCaughtByGale: true } player ||
                    !Player.EnemyCollisionOverlaps(player.Position,
                        new Rect2(Position - Vector2.One * 2, Vector2.One * 4)) ||
                    !RoomEntityManager.ObjectCollisionZOverlaps(player.GaleCollisionZ, CollisionZ, 7))
                {
                    UpdateGaleCounter();
                    return;
                }
                _precisePosition = player.GalePosition;
                Position = player.Position;
                _zFixed = player.GaleZFixed;
                _galeCounter2 = 0x3c;
                _galeSubstate = 1;
                ZIndex = 20;
                player.BeginGale();
                return;
            case 1:
                if (GalePlayer is { IsDying: true })
                {
                    _galeSubstate = 3;
                    return;
                }
                _galeCounter2 = (_galeCounter2 - 1) & 0xff;
                if (_galeCounter2 != 0)
                {
                    if (_group == 0) FlickerGaleAndCopyLink();
                    return;
                }
                _galeSubstate = 2;
                goto case 2;
            case 2:
                _zFixed = (unchecked((sbyte)((_zFixed >> 8) - 2)) << 8) | (_zFixed & 0xff);
                if ((_zFixed & 0x8000) != 0)
                {
                    FlickerGaleAndCopyLink();
                    return;
                }
                GaleMenuRequested = true;
                Finish();
                return;
            case 3:
                // Indoors counter2 starts at $00, so this wraps through $ff.
                _galeCounter2 = (_galeCounter2 - 1) & 0xff;
                if (_galeCounter2 == 0) Finish();
                else Visible = !Visible;
                return;
        }
    }

    private void FlickerGaleAndCopyLink()
    {
        Visible = !Visible;
        GalePlayer?.SetGalePosition(_precisePosition, _zFixed);
    }
}
