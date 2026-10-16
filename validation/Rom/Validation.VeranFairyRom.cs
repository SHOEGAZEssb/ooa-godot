using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranFairyRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            for(int heart=0;heart<11;heart++)_inventory.GiveTreasure(TreasureId.HeartContainer,4);
            _inventory.RefillHealth();_statusBar.SynchronizeHealth();_saveData.SetRoomFlag(4,0xfc,0x80);
            LoadValidationRoom(4,0xfc);_entities.Clear();_inventory.EquipA(0);_inventory.EquipB(0);
            _player.WarpTo(new(0x18,0x98));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,1);_runtimeState.SetWramByte(WramAddress.wMenuDisabled,1);
            var fairy=_roomEvents.Get<BlackTowerPossessionEvent>().SpawnFairy(new(0x78,0x58))!;
            var actor=fairy.Actor;var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x18,0x98)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xd008]=0;rom[0xcc08]=0xd4;rom[0xcc0a]=0xd5;rom[0xcc0c]=0xd6;rom[0xcc0e]=0x8e;
            rom[0xcd16]=0;rom[0xcd17]=0xd0;rom[0xffaa]=48;rom[0xffac]=0;
            rom[WramAddress.wDisabledObjects]=1;rom[WramAddress.wMenuDisabled]=1;
            rom[0xd080]=1;rom[0xd081]=6;rom[0xd08b]=0x58;rom[0xd08d]=0x78;rom[0xcdd1]=1;
            int update=0,generation=0,hold=0,texts=0,inputAngle=0xff,inputKeys=0;
            Vector2 movement=Vector2.Zero;var attacks=new HashSet<int>();var patterns=new HashSet<int>();
            var sounds=_sound.AttachPlayRequestAudit();
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.UpdateGameplay(inputKeys,0,inputAngle,++update);rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                FailIf(!actor.IsDead&&(actor.State!=rom[0xd084]||actor.Substate!=rom[0xd085]||actor.Counter!=rom[0xd086]||actor.Counter2!=rom[0xd087]||
                    actor.Angle!=rom[0xd089]||actor.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f)||
                    actor.Speed!=rom[0xd090]||actor.ZFixed!=unchecked((short)rom.Word(0xd08e))||actor.Pattern!=rom[0xd0b0]||
                    actor.Attack!=rom[0xd083]||actor.HealthTier!=rom[0xd0b5]||actor.AttackPhase!=rom[0xd0b6]||
                    actor.Shots!=rom[0xd0b7]||actor.StillCounter!=rom[0xd0b8]||
                    actor.Health!=rom[0xd0a9]||(actor.InvincibilityCounter&255)!=rom[0xd0ab]||
                    SomariaPrivate<int>(actor.Animation,"_frameCounter")!=rom[0xd0a0]||actor.AnimationParameter!=rom[0xd0a1]),
                    $"Fairy Veran update {update}, batch={batched}: port state/sub/counts/angle/XY/speed/Z/pattern/attack/tier/phase/shots/still/HP/inv/anim="+
                    $"{actor.State}/{actor.Substate}/{actor.Counter},{actor.Counter2}/{actor.Angle}/{actor.Position}/{actor.Speed}/{actor.ZFixed}/{actor.Pattern}/{actor.Attack}/{actor.HealthTier}/{actor.AttackPhase}/{actor.Shots}/{actor.StillCounter}/{actor.Health}/{actor.InvincibilityCounter}/{SomariaPrivate<int>(actor.Animation,"_frameCounter")},{actor.AnimationParameter}; "+
                    $"ROM={rom[0xd084]}/{rom[0xd085]}/{rom[0xd086]},{rom[0xd087]}/{rom[0xd089]}/{rom.Word(0xd08c)/256f},{rom.Word(0xd08a)/256f}/{rom[0xd090]}/{unchecked((short)rom.Word(0xd08e))}/{rom[0xd0b0]}/{rom[0xd083]}/{rom[0xd0b5]}/{rom[0xd0b6]}/{rom[0xd0b7]}/{rom[0xd0b8]}/{rom[0xd0a9]}/{rom[0xd0ab]}/{rom[0xd0a0]},{rom[0xd0a1]}.");
                foreach(var part in _entities.EntityAdapters<VeranFairyProjectileRoomEntity>())
                {
                    int slot=Enumerable.Range(0,16).Single(s=>ReferenceEquals(_entities.ResolvePartSlot(s),part));int at=0xd0c0+slot*256;
                    FailIf(rom[at]==0||rom[at+1]!=part.Id||part.State!=rom[at+4]||part.Counter!=rom[at+6]||part.Angle!=rom[at+9]||
                        part.Speed!=rom[at+16]||part.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        part.ZFixed!=unchecked((short)rom.Word(at+14)),
                        $"Fairy PART ${part.Id:x2} slot {slot:x2} update {update}: port={part.State}/{part.Counter}/{part.Angle}/{part.Position}/{part.ZFixed}; "+
                        $"ROM={rom[at+4]}/{rom[at+6]}/{rom[at+9]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{unchecked((short)rom.Word(at+14))}.");
                    attacks.Add(part.Id);
                }
                foreach(var part in _entities.EntityAdapters<VeranProjectileRoomEntity>())
                {
                    int slot=Enumerable.Range(0,16).Single(s=>ReferenceEquals(_entities.ResolvePartSlot(s),part));int at=0xd0c0+slot*256;
                    FailIf(rom[at]==0||rom[at+1]!=0x37||part.SubId!=rom[at+2]||part.State!=rom[at+4]||part.Counter!=rom[at+6]||
                        part.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||part.ZFixed!=unchecked((short)rom.Word(at+14)),
                        $"Fairy circular PART $37:{part.SubId:x2} slot {slot:x2} update {update} differs from native XYZ/state/counter.");
                }
                foreach(var mimic in _entities.EntityAdapters<ArmMimicRoomEntity>())
                {
                    var body=(ArmMimicCharacter)mimic.Node;
                    int slot=Enumerable.Range(0,16).Single(s=>ReferenceEquals(_entities.ResolveEnemySlot(s),mimic));int at=0xd080+slot*256;
                    FailIf(rom[at+1]!=0x64||body.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||body.Angle!=rom[at+9]||
                        body.Direction!=rom[at+0x30]||SomariaPrivate<int>(body.Animation,"_frameCounter")!=rom[at+0x20],
                        $"Link Mimic $64 slot {slot:x2} update {update}: XY/angle/direction/animation must follow the shared Arm Mimic source dispatch.");
                }
                foreach(var explosion in _entities.Entities<InteractionExplosionEffect>())
                {
                    int at=0xd040+_entities.InteractionSlot(explosion)*256;
                    FailIf(rom[at+1]!=0x56||explosion.Position!=new Vector2(rom[at+13],rom[at+11])||
                        explosion.ZOffset!=unchecked((sbyte)rom[at+15]),
                        $"Fairy death explosion at update {update} must retain source XH/YH/ZH and interaction allocation order.");
                }
                if(_entities.EntityAdapters<VeranProjectileRoomEntity>().Any())attacks.Add(0x37);
                int count=_entities.EntityAdapters<VeranFairyProjectileRoomEntity>().Count()+_entities.EntityAdapters<VeranProjectileRoomEntity>().Count();
                int native=Enumerable.Range(0,16).Count(s=>rom[0xd0c0+s*256]!=0&&rom[0xd0c1+s*256] is 0x2d or 0x2f or 0x37);
                var random=_random.CaptureState();
                FailIf(count!=native||random.Rng1!=rom[0xff94]||random.Rng2!=rom[0xff95]||random.Calls-seed.Calls!=rom.RandomCalls||
                    !sounds.Requests.SequenceEqual(rom.Sounds)||_entities.RoomEnemyCount!=rom[0xcdd1]||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||
                    _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||_player.HealthQuarters!=rom[0xc6aa],
                    $"Fairy Veran shared state update {update}: PART={count}/{native}, RNG={random.Calls-seed.Calls}/{rom.RandomCalls}, "+
                    $"count={_entities.RoomEnemyCount}/{rom[0xcdd1]}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}, SFX={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(actor.State>=2&&actor.Pattern<4)patterns.Add(actor.Pattern);
                if(generation!=rom.TextGeneration)
                {
                    int[] expected=[0x5a0f,0x5a10,0x5a12];
                    FailIf(texts>=expected.Length||rom.Word(0xcba2)!=expected[texts]||!_dialogue.IsOpen,"Fairy dialogue must retain TX_560f/TX_5610/TX_5612 source order.");
                    texts++;generation=rom.TextGeneration;hold=3;
                }
                if(hold>0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
            }
            void Step(int count)=>StepGameplayUpdates(count,movement,batched:batched,afterUpdate:Compare);
            for(int wait=0;wait<400&&actor.State!=2;wait++)Step(1);
            FailIf(actor.State!=2||texts!=2,$"Fairy reveal must finish within its source counter bound: state={actor.State}:{actor.Substate}, counter={actor.Counter}, texts={texts}, update={update}, native={rom[0xd084]}:{rom[0xd085]}, mask={rom[WramAddress.wDisabledObjects]:x2}.");
            var spawns=new List<RoomEntitySpawn>();
            foreach(int health in new[]{39,19,9})
            {
                if(health!=39)
                {
                    while(actor.InvincibilityCounter!=0)Step(1);
                    rom.HitEnemyWithItem(0,4,actor.Health-health);
                    fairy.SetLinkSwordState(SwordActionState.Swing,1);
                    FailIf(!fairy.ApplySwordHit(actor.CollisionBounds,actor.Position-Vector2.Right,actor.Health-health,EnemyKnockbackStrength.Low,spawns),"Fairy must accept a native damage hit for its health-tier transition.");
                }
                Step(1200);
            }
            for(int wait=0;wait<1200&&(patterns.Count!=4||attacks.Count!=3);wait+=4)Step(4);
            FailIf(!attacks.SetEquals(new[]{0x2d,0x2f,0x37})||patterns.Count!=4,
                $"Fairy trace must exercise all four source patterns and all three attacks; got patterns={string.Join(',',patterns)}, attacks={string.Join(',',attacks)}, LinkHP={_player.HealthQuarters}/{rom[0xc6aa]}, LinkState={rom[0xd004]}, fairy={actor.State}:{actor.Attack}.");
            movement=Vector2.Right;inputKeys=0x10;inputAngle=8;Step(8);
            movement=Vector2.Zero;inputKeys=0;inputAngle=0xff;Step(8);
            for(int wait=0;wait<100&&_player.InvincibilityFrames!=0;wait++)Step(1);
            if(_player.ActiveTransformation==0)
            {
                int slot=Enumerable.Range(0,16).First(s=>_entities.ResolvePartSlot(s) is null);int at=0xd0c0+slot*256;
                Vector2 position=_player.Position.Floor();
                _entities.AddEntity(new VeranFairyProjectileRoomEntity(_entities,actor.Data,actor,0x2f,position,-256,0));
                FailIf(rom[at]!=0,"Baby-ball fixture requires the same free PART slot in the ROM.");
                rom[at]=1;rom[at+1]=0x2f;rom[at+11]=(byte)position.Y;rom[at+13]=(byte)position.X;rom[at+15]=0xff;
                rom[at+0x16]=0x80;rom[at+0x17]=0xd0;
                Step(4);
            }
            FailIf(_player.ActiveTransformation!=2||rom[0xd001]!=2,
                "PART $2f contact must hand off to the existing baby-Link owner on the following special-object update.");
            while(actor.InvincibilityCounter!=0)Step(1);
            rom.HitEnemyWithItem(0,4,actor.Health);
            fairy.SetLinkSwordState(SwordActionState.Swing,1);
            FailIf(!fairy.ApplySwordHit(actor.CollisionBounds,actor.Position-Vector2.Right,actor.Health,EnemyKnockbackStrength.Low,spawns),"Fairy must accept its terminal native damage hit.");
            for(int wait=0;wait<240&&!(actor.State==5&&actor.Substate==2&&actor.Counter2==1);wait++)Step(1);
            FailIf(texts!=3||actor.State!=5||actor.Substate!=2||actor.Counter2!=1||actor.Health!=1,
                "Fairy death must retain its native invincibility flags, flicker for 60 updates, show TX_5612, and emit four explosions before requesting the escape attempt.");
            Step(1);
            var escape=_roomEvents.Get<BlackTowerEscapeAttemptEvent>();
            rom.SelectTriggeredCutscene();
            FailIf(!escape.HasState||escape.State!=0||rom[0xc2ef]!=0x1f||rom[0xd080]!=0||!actor.IsDead||
                _entities.EntityAdapters<VeranFairyRoomEntity>().Any(),
                "Fairy terminal counter must select native escape $1f without running its state0 in the same object pass.");
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEscapeAttempt();rom.UpdateGameplay(0,0,0xff,++update);
                rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                FailIf(escape.State!=1||rom[0xcc03]!=1||_entities.EntityAdapters<VeranFairyRoomEntity>().Any()||
                    _entities.EntityAdapters<NayruRescueActorRoomEntity>().Count()!=2||
                    _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0),
                    "The following gameplay update must clear the fairy object bank, restore Link and initialize both escape companions once.");
            });
            LoadValidationRoom(4,0xfd);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranFairyRoomEntity>().Any()||_entities.EntityAdapters<VeranFairyProjectileRoomEntity>().Any()||
                _entities.PlayerMenusDisabled||_transitions.NativePaletteFadeActive,"Leaving the fairy battle must release actors, parts, masks and palette ownership.");
        }
    }
}
