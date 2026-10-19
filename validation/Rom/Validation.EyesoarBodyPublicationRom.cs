using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEyesoarBodyPublicationRom()
    {
        Vector2I[] far=[new(-16,-16),new(0,-16),new(16,-16),new(-16,0),new(16,0),new(-16,16),new(0,16),new(16,16)];
        var offsets=far.Concat(from y in Enumerable.Range(-2,5) from x in Enumerable.Range(-2,5) select new Vector2I(x,y));
        foreach(var offset in offsets)
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(4,0x6b);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(80+offset.X,80+offset.Y));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            var resources=new RoomEntityResources(_rooms);
            var world=new EyesoarEnvironment(_currentRoom,resources.Eyesoar,resources.DungeonBosses,
                resources.DungeonVisuals.Visual("eyesoar-spawn"),_random,()=>false,()=>_entities.PartSlotAvailable,
                _entities.CanAllocateEnemies,()=>_entities.InteractionSlotAvailable,_sound.PlaySound,
                _entities.DisableLinkCollisionsAndMenu,_entities.EnableLinkCollisionsAndMenu,new BossEntryMovement(Vector2I.Zero));
            var actor=new EyesoarActor();actor.Initialize(resources.DungeonBosses.Enemy(0x7b,1),world,new(80,80));
            var adapter=new EyesoarRoomEntity(actor);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            byte[] observed=new byte[4];int probes=0,updates=0;
            var after=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=0xc4;native[0xcc93]=1;
            const int at=0xd180;native[at]=1;native[at+1]=0x7b;native[at+2]=1;native[at+11]=native[at+13]=80;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                FailIf(actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Angle!=native[at+9]||
                    actor.ZFixed!=unchecked((short)native.Word(at+14))||actor.AnimationParameter!=native[at+0x21]||
                    probes!=1||!observed.SequenceEqual(expected),
                    $"Eyesoar body/offset{offset}/fraction{fractional}/update{updates}: original target/near-box/motion differs: XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, state{actor.State}/{native[at+4]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    "Eyesoar target movement must retain the single original common initialization draw.");probes=0;
            });
            void StartTarget()
            {
                F1Set(actor,"<State>k__BackingField",10);F1Set(actor,"<Counter>k__BackingField",1);
                native[at+4]=10;native[at+6]=1;Step(1);
            }
            Step(1);
            if(fractional){actor.Position+=new Vector2(.25f,.5f);native[at+10]=0x80;native[at+12]=0x40;}
            StartTarget();Step(2);
            _dialogue.ShowMessage("Eyesoar body hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,4);native[0xcd00]=4;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            int remaining=100;while(actor.State==11&&remaining-->0)Step(1);
            FailIf(actor.State!=10,"Eyesoar body must complete its source-selected target movement before waiting.");Step(2);
            _player.WarpTo(new(120,96));StartTarget();remaining=140;
            while(actor.State==11&&remaining-->0)Step(1);
            FailIf(actor.State!=10,"Eyesoar body must complete a second actual target movement.");Step(2);
            _entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
    }
}
