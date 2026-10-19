using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEyesoarChildPublicationRom()
    {
        foreach(int sub in Enumerable.Range(0,4))
        foreach(Vector2 center in new[]{new Vector2(8,8),new Vector2(120,88),new Vector2(248,248)})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(4,0x6b);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,104));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            var resources=new RoomEntityResources(_rooms);
            var world=new EyesoarEnvironment(_currentRoom,resources.Eyesoar,resources.DungeonBosses,
                resources.DungeonVisuals.Visual("eyesoar-spawn"),_random,()=>false,()=>_entities.PartSlotAvailable,
                _entities.CanAllocateEnemies,()=>_entities.InteractionSlotAvailable,_sound.PlaySound,
                _entities.DisableLinkCollisionsAndMenu,_entities.EnableLinkCollisionsAndMenu,new BossEntryMovement(Vector2I.Zero));
            var body=new EyesoarActor();body.Initialize(resources.DungeonBosses.Enemy(0x7b,1),world,center);
            var actor=new EyesoarActor();actor.Initialize(resources.DungeonBosses.Enemy(0x11,sub),world,
                fractional?new(.25f,.5f):Vector2.Zero);F1Set(actor,"<Parent>k__BackingField",body);
            var bodyAdapter=new EyesoarRoomEntity(body);var adapter=new EyesoarRoomEntity(actor);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(bodyAdapter,1);_entities.AddEntity(bodyAdapter);
            _entities.RegisterEnemySlot(adapter,2);_entities.AddEntity(adapter);
            byte[] observed=new byte[4];int probes=0,updates=0;
            var after=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=0xc4;native[0xcc93]=1;
            const int parent=0xd180,at=0xd280;native[parent]=1;native[parent+1]=0x7b;native[parent+2]=1;
            native[parent+11]=(byte)center.Y;native[parent+13]=(byte)center.X;
            native[at]=1;native[at+1]=0x11;native[at+2]=(byte)sub;native[at+0x16]=0x80;native[at+0x17]=0xd1;
            if(fractional){native[at+10]=0x80;native[at+12]=0x40;}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                FailIf(actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Angle!=native[at+9]||
                    actor.Distance!=native[at+0x30]||actor.TargetDistance!=native[at+0x31]||
                    actor.ZFixed!=unchecked((short)native.Word(at+14))||body.ReadyFlags!=native[parent+0x3a]||
                    actor.Visible!=((native[at+0x1a]&128)!=0)||actor.AnimationParameter!=native[at+0x21]||
                    probes!=1||!observed.SequenceEqual(expected),
                    $"Eyesoar child${sub:x2}/center{center}/fraction{fractional}/update{updates}: original child differs: XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, state{actor.State}/{native[at+4]}, radius{actor.Distance}/{native[at+0x30]}, angle{actor.Angle}/{native[at+9]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    "Declared Eyesoar body/child slots must retain original common initialization RNG order.");probes=0;
            });
            void Set(object owner,string property,int value)=>F1Set(owner,"<"+property+">k__BackingField",value);
            void Flags(int value){Set(body,"Flags",value);native[parent+0x39]=(byte)value;}
            void Child(int state,int radius)
            {
                Set(actor,"State",state);Set(actor,"Distance",radius);F1Set(actor,"_zFixed",-512);actor.Visible=true;
                native[at+4]=(byte)state;native[at+0x30]=(byte)radius;native[at+15]=0xfe;native[at+0x1a]|=128;
            }
            Step(3); // Common state0 and two hidden wait visits.
            foreach(int radius in new[]{0,24,48,255})
            {
                Flags(0);Child(10,radius);
                for(int rotation=0;rotation<32;rotation++)
                {Set(body,"OrbitRotation",rotation);native[parent+0x3b]=(byte)rotation;Step(1);}
            }
            Set(body,"OrbitRotation",0);native[parent+0x3b]=0;
            Set(body,"Formation",48);native[parent+0x38]=48;Flags(4);Child(10,24);
            Step(26);FailIf(actor.State!=10||actor.Distance!=48,"Eyesoar child must finish expansion and publish readiness on the source visit after reaching radius48.");
            Flags(1);Child(12,0);actor.Visible=false;Set(actor,"Counter",2);
            native[at+0x1a]&=127;native[at+6]=2;Step(3);Flags(0);Step(2);Step(50);
            FailIf(actor.State!=10||actor.Distance!=48,"Eyesoar hidden child must resume its counter and expand from zero through the original reappearance branch.");
            Vector2I[] offsets=[new(0,-24),new(24,0),new(0,24),new(-24,0)];
            Vector2 destination=new(((int)center.X+offsets[sub].X)&255,((int)center.Y+offsets[sub].Y)&255);
            actor.Position=destination+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
            native.Word(at+12,(int)(actor.Position.X*256));native.Word(at+10,(int)(actor.Position.Y*256));
            Flags(2);Child(16,24);Step(3); // Reached but waiting: scaled publication still occurs.
            _dialogue.ShowMessage("Eyesoar return hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            Flags(0);Step(1);FailIf(actor.State!=14,"Eyesoar arrived child must publish its parent's high ready flag when released.");
            Flags(10);Child(15,24);Set(actor,"Angle",0);native[at+9]=0;
            actor.Position=new(.25f,.5f);native.Word(at+12,0x40);native.Word(at+10,0x80);
            Step(24);_runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            // Keep the center in the playable arena for a reachable return trajectory.
            body.Position=new(120,88);native.Word(parent+12,120*256);native.Word(parent+10,88*256);
            Flags(2);Step(1);FailIf(actor.State!=16,"Removing scatter bit3 must hand off to return without moving that visit.");
            Flags(0);int remaining=400;while(actor.State==16&&remaining-->0)Step(1);
            FailIf(actor.State!=14,"Eyesoar child must converge and hand readiness back after actual return movement.");
            Step(3);_entities.Clear();StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.Entities<EyesoarActor>().Count!=0,"Cancelled Eyesoar actors must not resume their previous return/orbit sequence.");
            _entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
    }
}
