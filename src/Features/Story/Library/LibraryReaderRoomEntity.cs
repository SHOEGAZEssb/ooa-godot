using Godot;
using System.Collections.Generic;

namespace oracleofages;

// Independently owned reading scripts. The carpenter alone sets enabled bit
// $80 and runs the native animation/gravity tail before interactionRunScript.
internal sealed class LibraryReaderRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomBlocker, IPlayerInteractable, IOrdinaryNpcEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly LibraryDatabase _database;
    private readonly LibraryReaderData _record;
    private readonly OracleSaveData? _save;
    private readonly LibraryReaderScriptHost _host;
    private readonly CutsceneCommandRunner _runner;
    private bool _initialized;
    private int _z, _speedZ;
    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => false;
    public bool RefreshVisibility => false;
    public bool UpdatesDuringDialogue => !_initialized || _record.AlwaysUpdate;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;

    internal LibraryReaderRoomEntity(NpcCharacter npc, LibraryDatabase database,
        OracleSaveData? save, RoomEntityManager entities) : base(npc, npc.SetTransitionDrawOffset)
    {
        _database = database; _record = database.Reader(npc.Record); _save = save;
        npc.SetScriptVisible(false); npc.SetScriptButtonSensitive(false);
        npc.SetDialogue(0, "", canFace: false);
        npc.SetCollisionRadii(0, 0);
        npc.TimeWarpSolidMarked = false;
        _host = new(npc, database, save, entities);
        _runner = new(_host);
    }
    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        bool clean = _save?.HasGlobalFlag(GlobalFlag.WaterPollutionFixed) == true;
        Npc.SetCollisionRadii(_record.RadiusY, _record.RadiusX);
        Npc.SetDialogue(clean ? _record.AfterText : _record.BeforeText,
            clean ? _record.AfterMessage : _record.BeforeMessage, canFace: false);
        Npc.SetScriptAnimation(_database.Animation(Npc.Record.Id, _record.InitialAnimation, Npc.Record.SubId));
        Npc.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        Npc.TimeWarpSolidMarked = _record.SolidMark;
        Npc.SetScriptVisible(true);
        _runner.Start(_record.Commands);
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) { Initialize(); return; }
        _host.Player = frame.Player;
        Npc.ClearFixedDrawPriority();
        if (_record.AlwaysUpdate) Animate(frame.Player);
        _runner.AdvanceFrame();
        if (!_record.AlwaysUpdate) Animate(frame.Player);
    }
    private void Animate(Player player)
    {
        Npc.AnimateAsNpcOneUpdate(player);
        if (_record.AlwaysUpdate)
        {
            // carpenter.s:@runSubid uses gravity $40. This script never
            // starts a hop, but native grounding still precedes script work.
            OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x40);
            Npc.SetScriptDrawOffset(new(0, _z >> 8));
        }
    }
    public bool TryInteract(Player player)
    {
        if (!Npc.ScriptButtonSensitive || !Npc.CanTalkTo(player) || !_host.PressA()) return false;
        player.ApplyObjectInteractionGrace(); return true;
    }
    public bool BlocksLink(Vector2 center) => Npc.BlocksLinkCenter(center);
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (_record.AlwaysUpdate) UpdateFrame(frame, System.Array.Empty<RoomEntitySpawn>());
    }
    ScreenTransitionPresentation IScreenTransitionPreloadRoomEntity.PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return ScreenTransitionPresentation.Visible;
    }
}
