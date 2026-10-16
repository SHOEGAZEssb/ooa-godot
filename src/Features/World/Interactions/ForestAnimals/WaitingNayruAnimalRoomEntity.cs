using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// bear.s:$02/v$00 owns parseGivenObjectData even when later gates delete the
// bear. Its two monkey $39:$07 children and rabbit $4b:$07 retain separate slots.
internal sealed class WaitingNayruAnimalRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IOrdinaryNpcEntity, IRoomEntityLifetime, ITalkTarget, IRoomBlocker,
    IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity
{
    private readonly RoomEntityManager _entities;
    private readonly SharedNpcDatabase _data;
    private readonly OracleSaveData _save;
    private bool _initialized, _talking;
    private Func<bool>? _pendingDialogue;
    private int _idleAnimation;
    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => false;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    internal WaitingNayruAnimalRoomEntity(NpcCharacter npc, RoomEntityManager entities, SharedNpcDatabase data, OracleSaveData save)
        : base(npc, npc.SetTransitionDrawOffset)
    { _entities=entities; _data=data; _save=save; npc.SetScriptVisible(false); }
    private void Initialize()
    {
        if (_initialized) return;
        _initialized=true;
        int id=Entity.Record.Id;
        bool saved=_save.HasGlobalFlag(GlobalFlag.SavedNayru);
        bool finished=_save.HasGlobalFlag(GlobalFlag.FinishedGame);
        bool maku=_save.HasGlobalFlag(GlobalFlag.MakuTreeSaved);
        if (id==0x5d)
        {
            if (!_save.HasGlobalFlag(GlobalFlag.IntroDone)) { Delete(); return; }
            // parseGivenObjectData allocates all children in source order
            // before FINISHEDGAME/MAKU_TREE_SAVED can delete the parent.
            foreach (var record in _data.WaitingAnimals)
            {
                if (!_entities.InteractionSlotAvailable) throw new NotSupportedException("animalsWaitingForNayru cannot allocate its original three child slots ($39/$4b/$39).");
                _entities.AddEntity(new WaitingNayruAnimalRoomEntity(NpcCharacter.CreateFromRecord(record), _entities, _data, _save));
            }
            if (finished || !maku) { Delete(); return; }
            Entity.SetDialogue(saved?0x5713:0x5712,_data.Text(saved?0x5713:0x5712),false);
        }
        else if (id==0x39)
        {
            if (finished || (Entity.Record.Var03==0 ? !saved : !maku)) { Delete(); return; }
            int text=Entity.Record.Var03==0?0x5715:saved?0x571a:0x5719;
            Entity.SetDialogue(text,_data.Text(text),false);
            _idleAnimation=Entity.Record.Var03==0?6:5;
            Entity.SetScriptAnimation(_data.Animation(0x39,_idleAnimation));
        }
        else if (id==0x4b)
        {
            if (finished || !maku) { Delete(); return; }
            Entity.SetDialogue(saved?0x5718:0x5717,_data.Text(saved?0x5718:0x5717),true);
        }
        else throw new NotSupportedException($"Waiting animal ${id:x2}:${Entity.Record.SubId:x2}.");
        Entity.SetScriptVisible(true);
        Entity.SetScriptButtonSensitive(true);
        Entity.ShowInitializedPresentation();
    }
    private void Delete() { Finished=true; Entity.SetActive(false); }
    public new ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Initialize(); return Entity.Active ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden; }
    public bool TryQueueDialogue(Func<bool> tryShowDialogue) { _pendingDialogue=tryShowDialogue; return true; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) { Initialize(); if(Entity.Record.Id!=0x4b)return; }
        if (Finished) return;
        if (_talking)
        {
            _talking=false;
            if(Entity.Record.Id==0x39)Entity.SetScriptAnimation(_data.Animation(0x39,_idleAnimation));
        }
        if (_pendingDialogue is not null)
        {
            if(Entity.Record.Id==0x39)Entity.SetScriptAnimation(_data.Animation(0x39,frame.Player.Position.Y>=Entity.Position.Y?6:5));
            if(_pendingDialogue.Invoke()){_pendingDialogue=null;_talking=true;}
        }
        if(Entity.Record.Id==0x4b)Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
        else Entity.AnimateAsNpcOneUpdate(frame.Player);
    }
    public bool BlocksLink(Vector2 center) => Entity.ScriptVisible && Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => _initialized && !Finished && Entity.CanTalkTo(player) ? Entity : null;
}
