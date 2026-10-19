using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Real room approach/cart collision, compared with the clean-US broken-item
    // handler and original child dispatch from declared source parent poses.
    private void ValidatePatchCreationPhaseRom()
    {
        var bytes=ValidationRom.LoadCleanUs().Span;
        FailIf(!bytes.Slice(0x0a*0x4000+0x3bf0,10).SequenceEqual(new byte[]{0x1e,0x44,0x1a,0xc7,0xfa,0x7b,0x11,0x7c,0x38,0x7c}),
            "Original Patch broken-item dispatch $0a:$7bf0 changed.");
        foreach(bool sword in new[]{false,true})
        foreach(bool batch in new[]{false,true})
        foreach(int free in new[]{0,1})
        {
            ReinitializeGameplayForValidation();_inventory.EquipA(0);_inventory.EquipB(0);
            _inventory.GiveTreasure(TreasureId.TuniNut,0);
            if(sword)_inventory.GiveTreasure(TreasureId.TradeItem,0x0b);
            _runtimeState.SetWramByte(0xcfd0,sword?(byte)1:(byte)0);
            _runtimeState.SetWramByte(0xcfd1,0);_runtimeState.SetWramByte(0xcfd2,1);
            _runtimeState.SetWramByte(0xcfd7,sword?(byte)0x13:(byte)0x12);
            LoadValidationRoom(5,0xe8);var patch=_roomEvents.Get<PatchEvent>();
            Action? shadow=null;
            void Step(int count=1)=>StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:()=>shadow?.Invoke());
            void PressA(){StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batch);Step();}
            void Until(Func<bool> condition,int limit,string context)
            {
                for(int i=0;i<limit&&!condition();i++)Step();
                FailIf(!condition(),$"Patch ROM {context}: state{patch.State},script{patch.ScriptIndex},text={_dialogue.CurrentMessage}.");
            }
            void Text(string text){Until(()=>_dialogue.IsOpen,400,text);FailIf(!_dialogue.CurrentMessage.Contains(text),$"Expected Patch text {text}.");}
            void Close()
            {
                for(int i=0;i<180&&_dialogue.IsOpen;i++){Step(30);PressA();}
                FailIf(_dialogue.IsOpen,"Patch ROM dialogue did not close through A input.");
            }
            void Talk(bool reposition=true)
            {
                var npc=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x94&&n.Record.SubId==1&&n.Active);
                if(reposition)_player.WarpTo(npc.Position+new Vector2(0,48));
                _player.Face(Vector2I.Up);
                FailIf(_collision.Collides(_player.Position)||_entities.BlocksLink(_player.Position),"Patch approach must begin on actual open floor.");
                for(int i=0;i<80&&!npc.CanTalkTo(_player);i++)StepGameplayUpdates(1,Vector2.Up,["move_up"],i==0?["move_up"]:[],batch);
                Step();FailIf(!npc.CanTalkTo(_player),"Patch must be reachable through actual room collision.");PressA();
            }
            void StartGame(bool reposition=true)
            {
                Talk(reposition);Text("Welcome to");_dialogue.SubmitChoiceForValidation(0);Step(2);
                Text("Very well!");_dialogue.SubmitChoiceForValidation(0);Step(2);
                Text("Let the ceremony");Close();Until(()=>patch.State==2,30,"game start");
            }
            StartGame();
            Until(()=>_entities.Entities<HardhatBeetleCharacter>().Count==4,65,"initial beetles");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,4); // Hold unrelated ENEMY AI; interactions keep their source category.
            _player.WarpTo(new(120,104));_player.SetScriptedPosition(_player.Position+new Vector2(0.25f,0.75f));
            FailIf(_collision.Collides(_player.Position),"Patch crash observer must occupy actual floor.");
            var broken=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x94&&n.Record.SubId==(sword?5:4));
            var cart=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x94&&n.Record.SubId==2);
            bool WillHit()
            {
                Vector2 next=SomariaPrivate<Vector2>(patch,"_cartPosition");
                next=OracleObjectMovement.Shared.ApplySpeed(ref next,patch.Database.Constant("speed"),SomariaPrivate<int>(patch,"_cartAngle"));
                return Player.EnemyCollisionOverlaps(next,new Rect2(broken.Position-Vector2.One*6,Vector2.One*12));
            }
            Until(WillHit,1600,"actual cart collision boundary");
            FailIf(SomariaPrivate<int>(patch,"_brokenState")!=1||_runtimeState.ReadWramByte(0xcfd5)!=0||patch.Fading,
                "Broken item must still be active before actual cart collision.");
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots");
            while(14-slots.Count>free)_entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,104),SoundId.MusNone));
            var rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)_player.Position.X,(int)_player.Position.Y);
            foreach(int slot in slots.Values){int at=0xd040+slot*256;rom[at]=1;rom[at+1]=5;rom[at+2]=0x80;}
            int atBroken=0xd040+_entities.InteractionSlot(broken)*256;
            int atCart=0xd040+_entities.InteractionSlot(cart)*256;
            rom[atBroken]=1;rom[atBroken+1]=0x94;rom[atBroken+2]=sword?(byte)5:(byte)4;rom[atBroken+4]=1;
            rom[atBroken+11]=24;rom[atBroken+13]=120;rom[atBroken+0x26]=rom[atBroken+0x27]=6;
            rom.Word(atBroken+0x16,atCart);
            rom[atCart]=1;rom[atCart+1]=0x94;rom[atCart+2]=2;rom[atCart+0x26]=rom[atCart+0x27]=6;
            var cue=_sound.AttachPlayRequestAudit();var before=ValidationNodeLifetime.OrphanIds().ToHashSet();int rejected=0;
            try{Step();}catch(InvalidOperationException e)when(e.Message.Contains("native dynamic INTERACTION pool is full")){rejected++;}
            int orphaned=ValidationNodeLifetime.OrphanIds().Count(id=>!before.Contains(id));
            rom[atCart+11]=(byte)cart.Position.Y;rom[atCart+13]=(byte)cart.Position.X;
            rom[0xffae]=0x40;rom[0xffaf]=(byte)(atBroken>>8);
            byte[] caller=[0x16,(byte)(atBroken>>8),0xcd,0xf0,0x7b,0xc9];
            for(int i=0;i<caller.Length;i++)rom[0xc100+i]=caller[i];
            var native=SomariaPrivate<FrontendRom>(rom,"_rom");native.Call(0xc100,0x0a);
            int[] NativeExplosions()=>Enumerable.Range(2,14).Select(i=>0xd040+i*256).Where(a=>rom[a]!=0&&rom[a+1]==0x56).ToArray();
            FailIf(rejected!=0||orphaned!=0||_runtimeState.ReadWramByte(0xcfd5)!=1||rom[0xcfd5]!=1||
                SomariaPrivate<int>(patch,"_brokenState")!=(free==0?1:2)||rom[atBroken+4]!=(free==0?1:2)||
                _entities.Entities<InteractionExplosionEffect>().Count!=free||NativeExplosions().Length!=free,
                $"Patch hit sword={sword},batch={batch},free={free}: rejected{rejected},orphans{orphaned},state={SomariaPrivate<int>(patch,"_brokenState")}/{rom[atBroken+4]},failed={_runtimeState.ReadWramByte(0xcfd5)}/{rom[0xcfd5]}.");
            FailIf(cue.Requests.Count(id=>id==SoundId.SndExplosion)!=free,"A later-slot explosion must cue on its creation update.");
            int nativeExpiry=-1,portExpiry=-1,relativeUpdate=0;
            if(free==1)
            {
                int explosion=NativeExplosions().Single();var effect=_entities.Entities<InteractionExplosionEffect>().Single();
                FailIf(rom[explosion+3]!=1||rom[explosion+11]!=24||rom[explosion+13]!=112||effect.Position!=new Vector2(112,24)||
                    effect.ZIndex!=ObjectDrawPriority.InFrontOfLinkZIndex,"Source admitted explosion must increment var03 and apply X-08.");
                FailIf(explosion<=atBroken,"Last-free source child must follow its producer in the ascending native walk.");
                void ChildPass()
                {
                    // Supplied source parent/collision boundary; execute only
                    // the original later child, without uninitialized actor AI.
                    var held=Enumerable.Range(2,14).Select(i=>0xd040+i*256).Where(a=>a!=explosion)
                        .Select(a=>(Address:a,Enabled:rom[a])).ToArray();
                    foreach(var h in held)rom[h.Address]=0;
                    rom.AdvanceInteractions(_entities.FrameCounter);
                    foreach(var h in held)rom[h.Address]=h.Enabled;
                }
                ChildPass();
                FailIf(rom[explosion+4]!=1||effect.ElapsedUpdates!=1||rom.Sounds.Count(id=>id==SoundId.SndExplosion)!=1||
                    cue.Requests.Count(id=>id==SoundId.SndExplosion)!=1,
                    "The shared interaction walk must initialize/cue the later child on birth.");
                shadow=()=>
                {
                    relativeUpdate++;ChildPass();
                    if(nativeExpiry<0&&rom[explosion]==0)nativeExpiry=relativeUpdate;
                    if(portExpiry<0&&_entities.Entities<InteractionExplosionEffect>().Count==0)portExpiry=relativeUpdate;
                };
            }
            GD.Print($"ROM_PATCH_PHASE patch_hit sword={sword} batch={batch} free={free} rejected={rejected} orphaned={orphaned} broken_state={SomariaPrivate<int>(patch,"_brokenState")}/{rom[atBroken+4]} failed_flag=1/1 explosions={free}/{free}");
            Step();
            FailIf(cue.Requests.Count(id=>id==SoundId.SndExplosion)!=free,"An admitted effect must cue exactly once, including its next update.");
            FailIf(!patch.Fading||patch.State!=5||SomariaPrivate<int>(patch,"_brokenState")!=2,"Normal next update must begin US failure fade.");
            rom[0xc4ab]=1;native.Call(0xc100,0x0a);
            FailIf(rom[atBroken+4]!=2,"Original broken item must enter state2 when the palette fade starts.");
            Until(()=>patch.State==6&&!patch.Fading,130,"failure fade completion");
            FailIf(sword?_inventory.TradeItem!=0x0b:_inventory.TuniNutState!=0,"Actual failure flow must return the broken inventory state.");
            Until(()=>_dialogue.IsOpen,400,"failure script text");Close();Until(()=>!_player.CutsceneControlled,140,"failure control release");
            Step(40);
            FailIf(_entities.Entities<InteractionExplosionEffect>().Count!=0,"Failure explosion/fillers must expire without a late rejected retry.");
            if(free==1)
            {
                // interactionAnimation5a902: 4+4+3+7+8+8=34 animation
                // advances, then delete on the following source dispatch.
                FailIf(nativeExpiry!=35||portExpiry!=35,$"Patch effect must delete on source update35 after birth: {nativeExpiry}/{portExpiry}.");
                GD.Print($"ROM_PATCH_PHASE patch_phase sword={sword} batch={batch} source_birth_initialized=1 port_birth_initialized=1 birth_cues=1/1 next_cue=1 expiry={nativeExpiry}/{portExpiry}");
            }
            GD.Print($"ROM_PATCH_PHASE patch_completion sword={sword} batch={batch} free={free} restored_inventory=1 control_released=1 late_explosions=0");
            shadow=null;StartGame(reposition:false);
            Until(()=>_entities.Entities<HardhatBeetleCharacter>().Count==4,65,"repeated initial beetles");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,4);
            _player.WarpTo(new(120,104));
            broken=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x94&&n.Record.SubId==(sword?5:4)&&n.Active);
            Until(WillHit,1600,"repeated actual cart collision");cue.Clear();Step();
            var repeatedEffect = _entities.Entities<InteractionExplosionEffect>().Single();
            int repeatedSetup = _entities.InteractionSlot(repeatedEffect) > _entities.InteractionSlot(broken) ? 1 : 0;
            FailIf(_runtimeState.ReadWramByte(0xcfd5)!=1||_entities.Entities<InteractionExplosionEffect>().Count!=1||
                repeatedEffect.ElapsedUpdates!=repeatedSetup||cue.Requests.Count(id=>id==SoundId.SndExplosion)!=repeatedSetup,
                "Repeated game after full failure cleanup must create a fresh explosion at its real cart hit.");
            Step();
            FailIf(repeatedEffect.ElapsedUpdates!=repeatedSetup+1 || cue.Requests.Count(id=>id==SoundId.SndExplosion)!=1,
                "A reused earlier interaction slot must wait until the next pass, then cue exactly once.");
            GD.Print($"ROM_PATCH_PHASE patch_repeat sword={sword} batch={batch} free={free} reachable_talk=1 repeated_cart_hit=1 fresh_explosion=1");
            _dialogue.Close();patch.Cancel();_entities.Clear();
            FailIf(slots.Count!=0,"Patch cancellation must release actor/effect slots.");
        }
        ReinitializeGameplayForValidation();
    }
}
