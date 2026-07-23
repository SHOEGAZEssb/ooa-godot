using Godot;
using System;

namespace oracleofages;

internal partial class WallmasterCharacter : SpiritsGraveEnemyCharacter
{

    private WallmasterState _state;
    private int _counter = 180;
    private int _zFixed;
    private int _speedZ;
    private int _remaining = 5;
    private OracleRoomData _room = null!;
    private bool _active;
    private Player? _grabbedPlayer;
    private bool _warpRequested;
    private bool _deathPuffPending;
    private bool _initialized;

    internal WallmasterState State => _state;
    internal int Counter => _counter;
    internal int Remaining => _remaining;
    internal int ZFixed => _zFixed;
    internal bool WarpRequested => _warpRequested;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && _active && _grabbedPlayer is null &&
        Math.Abs(_zFixed >> 8) < 7;

    internal void Initialize(
        EnemyRecord record,
        OracleRoomData room,
        Vector2 position)
    {
        InitializeEnemy(record, position);
        _room = room;
        Visible = false;
    }

    internal void UpdateFrame(Player player, Action<int> soundRequested)
    {
        if (IsDead)
            return;
        BeginFrame();
        if (!_initialized)
        {
            // The subid-$00 spawner installs its 180-update counter in state
            // 0, then begins decrementing it on the next object update.
            _initialized = true;
            _counter = 180;
            return;
        }
        switch (_state)
        {
            case WallmasterState.Waiting:
                _counter--;
                if (_counter != 0)
                    return;
                // The subid-$00 spawner always reloads its 120-update delay
                // before testing Link's tile. A blocked spawn therefore waits
                // for the full interval before trying again.
                _counter = 120;
                if (_room.IsSolid(player.Position))
                    return;
                Position = player.Position;
                _zFixed = -(0x60 << 8);
                _speedZ = 0;
                _active = true;
                Visible = true;
                _state = WallmasterState.Falling;
                soundRequested(OracleSoundEngine.SndFallInHole);
                break;

            case WallmasterState.Falling:
                FollowGrabbedPlayer();
                if (!OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x0e))
                {
                    UpdateHighVisibility();
                    break;
                }
                _counter = 30;
                _state = WallmasterState.Grounded;
                break;

            case WallmasterState.Grounded:
                _counter--;
                if (_counter == 20)
                    SetAnimation(1);
                if (_grabbedPlayer is not null && _counter < 20)
                    _grabbedPlayer.Visible = false;
                if (_counter == 0)
                    _state = WallmasterState.Rising;
                break;

            case WallmasterState.Rising:
                UpdateHighVisibility();
                _zFixed -= 2 << 8;
                if (_zFixed > -(0x60 << 8))
                    break;
                if (_grabbedPlayer is not null)
                {
                    _warpRequested = true;
                    return;
                }
                HideAndReset(120);
                break;
        }
        AdvanceAnimation();
        QueueRedraw();
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!base.TakeSwordHit(sourcePosition, damage))
            return false;
        if (!IsDead)
            return true;

        _deathPuffPending = true;
        _remaining--;
        if (_remaining == 0)
            return true;
        Revive(Record.Health);
        HideAndReset(120);
        return true;
    }

    internal bool HandleLinkContact(Player player)
    {
        if (_grabbedPlayer is not null || !CollisionEnabled ||
            !OverlapsLink(player.Position))
        {
            return false;
        }
        _grabbedPlayer = player;
        player.BeginCutsceneControl();
        player.SetScriptedPosition(Position);
        return true;
    }

    internal EnemyDeathPuffSpawn? TakeDeathPuff()
    {
        if (!_deathPuffPending)
            return null;
        _deathPuffPending = false;
        return new EnemyDeathPuffSpawn(Position, EnemyId: Record.Id);
    }

    internal Player? TakeWarpedPlayer()
    {
        if (!_warpRequested)
            return null;
        _warpRequested = false;
        // Keep ownership until the room reload removes this hand. Clearing it
        // here would re-enable collision for the contact pass later in the
        // same update and capture Link a second time.
        return _grabbedPlayer;
    }

    public override void _Draw()
    {
        if (!_active || IsDead)
            return;
        DrawSetTransform(new Vector2(0, _zFixed >> 8));
        base._Draw();
        DrawSetTransform(Vector2.Zero);
    }

    private void HideAndReset(int delay)
    {
        _active = false;
        Visible = false;
        _counter = delay;
        _state = WallmasterState.Waiting;
        _zFixed = 0;
        _speedZ = 0;
        SetAnimation(0);
    }

    private void FollowGrabbedPlayer()
    {
        if (_grabbedPlayer is null)
            return;
        _grabbedPlayer.SetScriptedPosition(Position);
    }

    private void UpdateHighVisibility()
    {
        int z = _zFixed >> 8;
        if (z < -0x48)
            Visible = !Visible;
        else if (z < -0x44)
            Visible = true;
    }
}
