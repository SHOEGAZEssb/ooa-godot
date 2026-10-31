using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedCompanionEffects(FrontendValidationRoot root,bool batched)
    {
        var session=root._gameplaySession!;var rooms=session.Rooms;var memory=rooms.RuntimeState;
        var entities=root._seasonsOpening?.Entities??root._entities;var app=new ApplicationValidationFixture(root);
        var player=root._scene.Player;var room=rooms.CurrentRoom;
        player.Inventory.SetScriptedHealth(player.Inventory.MaxHealthQuarters);session.StatusBar.SynchronizeHealth();
        var sounds=root._sound.AttachPlayRequestAudit();
        int Count()=>entities.EntityAdapters<IRoomEntity>().Count(actor=>actor is ExclamationMarkRoomEntity);
        try
        {
            foreach(bool hover in new[]{false,true})
            {
                entities.Clear();sounds.Clear();int rng=entities.Random.Calls;
                RoomEntitySpawn spawn=hover?new MooshHoverExclamationSpawn(new(72,64),-0x400):new ExclamationMarkSpawn(new(72,64));
                var actor=(hover?entities.TryCreateInteractionEntity(spawn):entities.TryCreateExclamation(new(72,64),30))
                    ??throw new InvalidOperationException("Native INTERAC $9f fixture needs an empty interaction pool.");
                var npc=actor.PresentationNode as NpcCharacter??throw new InvalidOperationException("INTERAC $9f requires its NPC presenter.");
                FailIf(npc.Visible,"INTERAC $9f must retain hidden construction until its native state-zero visit.");
                FailIf(entities.CanCaptureDebugState,"Transient INTERAC $9f capture must not silently omit its retained counter/presentation.");
                bool rejected=false;
                try{_=entities.CaptureDebugState();}
                catch(NotSupportedException error)when(error.Message.Contains("exclamationMark.s",StringComparison.Ordinal) && error.Message.Contains("$9f",StringComparison.Ordinal)){rejected=true;}
                FailIf(!rejected,"Transient INTERAC $9f capture requires a source-aware reconstruction diagnostic.");
                string sprite=rooms.SaveData.Game==OracleGame.Ages?"spr_zz_bubble_exclamation_heart_kid":"spr_zz_bubble_exclamation_heart";
                FailIf(npc.Record.Id!=0x9f || npc.Record.SubId!=0 || npc.Record.SpriteName!=sprite || npc.Record.TileBase!=8 || npc.Record.Palette!=5 ||
                    npc.Record.DownAnimation!="8@8,0,0,0;8,8,0,32|8@8,0,2,0;8,8,2,32" ||
                    npc.Position!=new Vector2(72,hover?28:64),$"{rooms.SaveData.Game}: common INTERAC $9f must retain its native pack graphics and copied Z projection.");
                FailIf(entities.Random.Calls!=rng || !sounds.Requests.SequenceEqual(hover?Array.Empty<int>():new[]{SoundId.SndClink}),
                    "objectCreateExclamationMark emits one SND_CLINK; Moosh's direct allocation emits none, and neither consumes RNG.");
                app.Step(1,Vector2.Zero,batched:batched);
                int duration=hover?60:30;
                FailIf(((ExclamationMarkRoomEntity)actor).Counter!=duration || !npc.Visible,
                    "INTERAC $9f state0 must publish visibility without consuming the copied native counter.");
                root._scene.Dialogue.ShowMessage("Companion effect update eligibility.",104);
                memory.SetField("wDisabledObjects",1);
                app.Step(4,Vector2.Zero,batched:batched);
                FailIf(Count()!=1 || hover && ((ExclamationMarkRoomEntity)actor).Counter!=56,
                    "Initialized INTERAC $9f enabled bit7 must update through text and the ordinary object freeze.");
                root._scene.Dialogue.Close();memory.SetField("wDisabledObjects",0);
                app.Step(duration-5,Vector2.Zero,batched:batched);
                FailIf(Count()!=1,"INTERAC $9f must remain live on the update before its finite counter reaches zero.");
                app.Step(1,Vector2.Zero,batched:batched);
                FailIf(Count()!=0,"INTERAC $9f must retire on its source zero decrement, after its separate initialization visit.");
                FailIf(!entities.CanCaptureDebugState,"Retired INTERAC $9f must release the transient debug-capture gate.");
            }
            foreach(int counter in new[]{0,255})
            {
                entities.Clear();sounds.Clear();
                var mark=(ExclamationMarkRoomEntity)entities.TryCreateInteractionEntity(
                    new ExclamationMarkSpawn(new(72,64),Duration:counter,PlaySound:false))!;
                var native=new BraceletRom(room,player.PrecisePosition,0,player.Inventory.ActiveRing,game:rooms.SaveData.Game);
                int at=0xd040+entities.NativeInteractionSlot(mark)*256;
                native[at]=1;native[at+1]=0x9f;native[at+6]=(byte)counter;native[at+0xb]=64;native[at+0xd]=72;
                app.Step(20,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceInteractions(entities.FrameCounter);
                    FailIf(mark.Counter!=native[at+6] || mark.Finished || native[at]==0 ||
                        mark.Node.Visible!=((native[at+0x1a]&128)!=0) || sounds.Requests.Count!=0,
                        "Direct INTERAC $9f counter0 must wrap to$ff; both values persist without the helper cue.");
                });
            }
            entities.Clear();sounds.Clear();int calls=entities.Random.Calls;
            for(int slot=0;slot<14;slot++)
                FailIf(entities.TryCreateInteractionEntity(new ExclamationMarkSpawn(new(72,64))) is null,
                    $"Native INTERAC $9f admission failed before physical slot${slot+2:x2}.");
            FailIf(entities.TryCreateInteractionEntity(new ExclamationMarkSpawn(new(72,64))) is not null || Count()!=14 ||
                sounds.Requests.Count!=14 || entities.Random.Calls!=calls,
                "Full INTERAC $d2-$df rejection must precede exclamation construction, sound and RNG.");
            FailIf(entities.TryCreateExclamation(new(72,64),30) is not null || sounds.Requests.Count!=14,
                "The shared exclamation helper must reject a full pool before constructing its presenter or playing SND_CLINK.");
            var pending=(System.Collections.Generic.List<RoomEntitySpawn>)typeof(RoomEntityManager)
                .GetField("_pendingSpawns",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(entities)!;
            pending.Add(new ExclamationMarkSpawn(new(72,64)));
            memory.SetField("wDisabledObjects",1);
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(Count()!=14 || sounds.Requests.Count!=14 || entities.Random.Calls!=calls,
                $"Queued exclamation helpers must retain checked allocation: count{Count()}/14, cues{sounds.Requests.Count}/14, RNG{entities.Random.Calls}/{calls}.");
            entities.Clear();
            FailIf(!entities.CanCaptureDebugState,"Cancelling INTERAC $9f must release the transient debug-capture gate.");
        }
        finally
        {
            root._scene.Dialogue.Close();memory.SetField("wDisabledObjects",0);entities.Clear();
        }
    }
}
