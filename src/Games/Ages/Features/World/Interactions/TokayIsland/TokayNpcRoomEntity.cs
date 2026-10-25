using System.Collections.Generic;
using System;
using Godot;

namespace Oracles;

internal sealed class TokayNpcRoomEntity(TokayCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
      IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IRoomEntityLifetime
{
    private bool _deleted;
    public bool Finished=>_deleted;
    internal void DeleteActor(){_deleted=true;Entity.SetActive(false);}
    internal TokayNpcScriptHost? Script { get; set; }
    private bool _scriptInitialized;
    internal Func<TokayHoldingItemCharacter, bool>? CreateHeldAccessory { get; set; }
    internal Action<TokayCharacter>? CreateShieldAccessory { get; set; }
    internal Action? NativeUpdate { get; set; }
    internal bool ReservesInitializationSlot=>npc.Record.SubId is 0x0f or 0x10;
    public NpcCharacter Npc => Entity;
    // $0b/$0f/$10 test their visibility predicates only in state0. Publishing
    // room flag80 or wDimitriState bit1 must
    // retain a live actor until its own scriptend/update, including a peer
    // still waiting behind tokayRunSubid0f's additional scroll gate.
    public bool RefreshVisibility => Entity.Record.SubId is not (0x0b or 0x0f or 0x10);
    public bool UpdatesDuringDialogue => (Script is not null||NativeUpdate is not null) && !_scriptInitialized || Entity.Record.SubId is 0x05 or 0x0e or 0x0f or 0x10 or 0x11 or 0x1e;
    public bool UpdatesDuringRoomEntityFreeze => ReservesInitializationSlot&&!_scriptInitialized || NativeUpdate is not null && (!_scriptInitialized||Entity.Record.SubId==5) || Script is not null && (!_scriptInitialized || Entity.Record.SubId is 0x0e or 0x0f or 0x10 or 0x11 or 0x1e);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if(!npc.Active){if(ReservesInitializationSlot){_scriptInitialized=true;_deleted=true;}return;}
        if(NativeUpdate is not null){_scriptInitialized=true;NativeUpdate();return;}
        if (Script is not null)
        {
            if (!_scriptInitialized)
            {
                _scriptInitialized=true;
                if (npc.Record.SubId==0x1d) (CreateShieldAccessory ?? throw new InvalidOperationException("Shield Tokay has no native allocator."))(npc);
                if (npc is TokayHoldingItemCharacter { HeldItemVisible: true } holder &&
                    !(CreateHeldAccessory ?? throw new InvalidOperationException("Tokay held accessory has no native allocator."))(holder))
                    holder.RemoveHeldItem();
                if (Entity.Record.SubId is not (0x0e or 0x0f or 0x10 or 0x11 or 0x1e)) return;
            }
            if(Entity.Record.SubId==0x1e){Script.AdvanceVine(frame.Player);return;}
            if(Entity.Record.SubId==0x0b){Script.AdvanceEscape(frame.Player);return;}
            if(Entity.Record.SubId is 0x0f or 0x10){Script.AdvanceDimitri(frame.Player);return;}
            // $0e/$11's always-update native animation precedes its text-gated
            // script, including the initialization fallthrough.
            if (Entity.Record.SubId is 0x0e or 0x11)
            {
                npc.RunNativeUpdate(frame.Player);
                Script.Advance(frame.Player);
                return;
            }
            Script.Advance(frame.Player);
        }
        npc.UpdateNative(frame.Player);
    }
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
