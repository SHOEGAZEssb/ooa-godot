using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// soldierSubid0d: story visibility is checked in state 0, then the script is
// advanced twice per state-1 update for stationary actors, once for patrollers.
internal sealed class PalaceFriendlySoldierRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomBlocker, IPlayerInteractable, IPlayerRestriction
{
    private readonly PalaceFriendlySoldierScriptHost _host;
    private readonly CutsceneCommandRunner _runner;
    private readonly PalaceSoldierRecord _record;
    private bool _initialized;

    internal PalaceFriendlySoldierRoomEntity(NpcCharacter actor, PalaceSoldierDatabase database,
        OracleSaveData save, OracleRuntimeState runtime, Func<bool> textActive,
        Action<int, string, Vector2> showText) : base(actor, actor.SetTransitionDrawOffset)
    {
        _record = database.Require(actor.Record.Var03);
        actor.SetActive(save.HasGlobalFlag(AgesGlobalFlag.FinishedGame));
        actor.SetBasePalette(_record.Palette);
        actor.SetDialogue(_record.TextId, _record.Message, true);
        actor.SetAnimationRate(0);
        _host = new(actor, database, _record, runtime, textActive, showText);
        _runner = new(_host); _runner.Start(database.Commands);
    }
    public bool DisablesSword => _host.InputDisabled;
    public bool DisablesItems => _host.InputDisabled;
    public bool DisablesMovement => _host.InputDisabled;
    public bool DisablesMenus => _host.InputDisabled;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active) return;
        _host.Player = frame.Player;
        if (!_initialized) { _initialized = true; Entity.ShowInitializedPresentation(); _runner.AdvanceFrame(); }
        _runner.AdvanceFrame();
        if (_record.Mode == 0)
        {
            _runner.AdvanceFrame();
            Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
        }
        else
        {
            Entity.AdvanceAnimationUpdates(1);
            Entity.PushPlayerAwayAndUpdateDrawPriority(frame.Player);
        }
    }
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public bool TryInteract(Player player)
    {
        if (!Entity.ScriptButtonSensitive || !Entity.CanTalkTo(player)) return false;
        _host.PressA(); player.ApplyObjectInteractionGrace(); return true;
    }
}
