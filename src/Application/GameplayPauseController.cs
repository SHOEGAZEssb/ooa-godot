using System;

namespace oracleofages;

/// <summary>
/// Owns the clone-side equivalent of exclusive menu/input suspension. A lease
/// restores the exact processing and debug-label state it acquired instead of
/// blindly enabling Link when an unrelated modal may still own the pause.
/// </summary>
public sealed class GameplayPauseController
{
    private readonly Player _player;
    private readonly Godot.Label _roomDebug;
    private PauseLease? _activeLease;

    public bool IsLeased => _activeLease is not null;

    public GameplayPauseController(Player player, Godot.Label roomDebug)
    {
        _player = player;
        _roomDebug = roomDebug;
    }

    internal bool IsOwnedBy(object owner) =>
        _activeLease is not null && ReferenceEquals(_activeLease.Owner, owner);

    internal PauseLease? TryAcquire(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (_activeLease is not null)
            return null;

        PauseLease lease = new PauseLease(
            this,
            owner,
            _player.IsProcessing(),
            _player.IsPhysicsProcessing(),
            _roomDebug.Visible);
        _activeLease = lease;
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        _roomDebug.Visible = false;
        return lease;
    }

    internal void Release(PauseLease lease)
    {
        if (!ReferenceEquals(_activeLease, lease))
            throw new InvalidOperationException("A gameplay pause lease was released by a non-owner.");

        _activeLease = null;
        _player.SetProcess(lease.PlayerProcessEnabled);
        _player.SetPhysicsProcess(lease.PlayerPhysicsProcessEnabled);
        _roomDebug.Visible = lease.RoomDebugVisible;
    }
}
