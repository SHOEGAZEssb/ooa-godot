using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Mirrors bank 2's shared wOpenedMenuType/wMenuLoadState lifecycle. Menu
/// clients retain their menu-specific code while this class exclusively owns
/// the fast palette fade and gameplay pause boundary.
/// </summary>
internal sealed class OracleMenuLifecycle
{

    internal const int FastFadeUpdates = 11;

    private readonly FixedUpdateFadeController _fade;
    private readonly GameplayPauseController _pause;
    private readonly FixedUpdateAccumulator _updates = new();
    private IOracleMenuLifecycleClient? _owner;
    private PauseLease? _pauseLease;

    internal Phase CurrentPhase { get; private set; }
    internal int FadeUpdate => _fade.Update;
    internal bool IsActive => _owner is not null;
    internal bool IsIdle => _owner is null;

    internal OracleMenuLifecycle(ColorRect fade, GameplayPauseController pause)
    {
        _fade = new FixedUpdateFadeController(fade);
        _pause = pause;
    }

    internal bool IsOwnedBy(IOracleMenuLifecycleClient client) =>
        ReferenceEquals(_owner, client);

    internal bool IsOpenFor(IOracleMenuLifecycleClient client) =>
        ReferenceEquals(_owner, client) && CurrentPhase == Phase.Open;

    internal bool TryBeginOpening(IOracleMenuLifecycleClient client)
    {
        if (_owner is not null)
            return false;
        PauseLease? lease = _pause.TryAcquire(client);
        if (lease is null)
            return false;

        _owner = client;
        _pauseLease = lease;
        CurrentPhase = Phase.OpeningFadeOut;
        _updates.Reset();
        _fade.Begin(Direction.ToWhite);
        return true;
    }

    internal void BeginClosing(IOracleMenuLifecycleClient client)
    {
        RequireOpenOwner(client);
        CurrentPhase = Phase.ClosingFadeOut;
        _updates.Reset();
        _fade.Begin(Direction.ToWhite);
    }

    /// <summary>
    /// MENU_INVENTORY switches to MENU_SAVEQUIT while already white, then runs
    /// only the common fade-in half of the lifecycle.
    /// </summary>
    internal void BeginFadeInFromWhite(IOracleMenuLifecycleClient client)
    {
        RequireOpenOwner(client);
        CurrentPhase = Phase.OpeningFadeIn;
        _updates.Reset();
        _fade.Begin(Direction.FromWhite);
    }

    internal void OpenImmediately(IOracleMenuLifecycleClient client)
    {
        if (!TryBeginOpening(client))
            throw new InvalidOperationException(
                $"Cannot open {client.MenuName}; another modal owns the menu lifecycle.");
        client.OpenAtWhite();
        _fade.SetTransparent();
        CurrentPhase = Phase.Open;
        _updates.Reset();
    }

    internal void CloseImmediately(IOracleMenuLifecycleClient client)
    {
        RequireOwner(client);
        client.CloseAtWhite();
        FinishClosing(client);
    }

    internal void Update(IOracleMenuLifecycleClient client, double delta)
    {
        RequireOwner(client);
        if (CurrentPhase == Phase.Open)
            return;

        int updates = _updates.Consume(delta);
        for (int index = 0; index < updates && _owner is not null; index++)
        {
            switch (CurrentPhase)
            {
                case Phase.OpeningFadeOut:
                    if (!_fade.AdvanceOneUpdate())
                        break;
                    client.OpenAtWhite();
                    CurrentPhase = Phase.OpeningFadeIn;
                    _fade.Begin(Direction.FromWhite);
                    break;

                case Phase.OpeningFadeIn:
                    if (!_fade.AdvanceOneUpdate())
                        break;
                    _fade.SetTransparent();
                    CurrentPhase = Phase.Open;
                    break;

                case Phase.ClosingFadeOut:
                    if (!_fade.AdvanceOneUpdate())
                        break;
                    client.CloseAtWhite();
                    CurrentPhase = Phase.ClosingFadeIn;
                    _fade.Begin(Direction.FromWhite);
                    break;

                case Phase.ClosingFadeIn:
                    if (_fade.AdvanceOneUpdate())
                        FinishClosing(client);
                    break;

                case Phase.Open:
                    // A long rendered frame may contain both complete 11-update
                    // fades. Menu-specific input begins on the next rendered
                    // update, matching the ordinary one-update path.
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Menu lifecycle entered invalid timed phase {CurrentPhase}.");
            }
        }
    }

    private void FinishClosing(IOracleMenuLifecycleClient client)
    {
        _fade.SetTransparent();
        CurrentPhase = Phase.Closed;
        _updates.Reset();
        _owner = null;
        PauseLease lease = _pauseLease ??
            throw new InvalidOperationException("The menu lifecycle lost its gameplay pause lease.");
        _pauseLease = null;
        lease.Dispose();
        client.LifecycleClosed();
    }

    private void RequireOwner(IOracleMenuLifecycleClient client)
    {
        if (!ReferenceEquals(_owner, client))
            throw new InvalidOperationException(
                $"{client.MenuName} attempted to mutate a lifecycle owned by {_owner?.MenuName ?? "none"}.");
    }

    private void RequireOpenOwner(IOracleMenuLifecycleClient client)
    {
        RequireOwner(client);
        if (CurrentPhase != Phase.Open)
            throw new InvalidOperationException(
                $"{client.MenuName} requested an open-state transition during {CurrentPhase}.");
    }
}
