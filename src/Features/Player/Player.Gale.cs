using Godot;

namespace oracleofages;

public partial class Player
{
    private IntroSpriteFrame[]? _galeFrames;
    private int _galeFrame;
    private int _galeFrameTicks;
    private bool _galePending;
    private bool _galeReturning;
    internal bool GaleActive { get; private set; }
    internal int GaleZFixed => _topDownAirborne ? _topDownAirZFixed : 0;
    internal int GaleCollisionZ => GaleZFixed >> 8;
    internal Vector2 GalePosition => _precisePosition;
    internal bool CanBeCaughtByGale => !GaleActive && !IsDying &&
        _activeTransformation == 0 && !_world.RidingObject && !_world.GaleWarpDisabled &&
        !_world.PlayerContactDisabled && !_braceletLiftCollisionsDisabled &&
        !ElectricShockActive && _enemyInvincibilityFrames == 0 &&
        _enemyKnockbackFrames == 0 && !_fallingInHole && !_drowning && !TopDownDiving;

    internal void BeginGale()
    {
        GaleActive = true;
        _galePending = true;
        _galeReturning = false;
    }

    internal void SetGalePosition(Vector2 position, int zFixed)
    {
        SetScriptedPosition(position);
        SetCutsceneDrawZFixed(zFixed);
        _topDownSwimmingState = 0;
    }

    private void AdvanceGale()
    {
        if (_galeReturning)
        {
            AdvanceRoomWarpFall();
            if (!IsRoomWarpFalling) EndGale();
            return;
        }
        if (_galePending)
        {
            _galePending = false;
            _world.InterruptBracelet(this, discard: false);
            _world.InterruptBomb(this, discard: false);
            _world.InterruptSeedShooter();
            ClearShieldParent();
            CancelSwordAttack();
            CancelShovelAction();
            _galeFrames ??= new NewGameIntroDatabase().SpriteFrames("link-gale");
            _galeFrame = 0;
            _galeFrameTicks = _galeFrames[0].Duration;
        }
        else if (--_galeFrameTicks == 0)
        {
            _galeFrame = (_galeFrame + 1) % _galeFrames!.Length;
            _galeFrameTicks = _galeFrames[_galeFrame].Duration;
        }
        _walking = false;
        _pushing = false;
        SetCutsceneSpriteFrame(_galeFrames![_galeFrame]);
    }

    internal void ReturnFromGale(int? gameplayScreenY = null)
    {
        _galeReturning = true;
        SetCutsceneSpriteFrame(null);
        SetCutsceneDrawZFixed(0);
        // linkState07 adds $04; warpTransition5 destInit subtracts it again.
        BeginRoomWarpFall(RoomWarpFallInitialZ((gameplayScreenY ?? (int)Position.Y) + 4));
    }

    private void EndGale()
    {
        GaleActive = false;
        _galePending = false;
        _galeReturning = false;
        SetCutsceneSpriteFrame(null);
        SetCutsceneDrawZFixed(0);
    }
}
