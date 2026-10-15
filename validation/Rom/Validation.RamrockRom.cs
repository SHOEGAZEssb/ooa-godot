using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRamrockRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0x78);
            _inventory.SetScriptedEquippedItems(0,0); _player.WarpTo(new(120,152));
            FailIf(_collision.Collides(_player.Position), "Ramrock fixture requires the room$5:$78 south floor.");
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach (var entity in active.Where(e => e.Node is not RamrockCharacter && e is not DungeonRewardRoomEntity).ToArray())
            { active.Remove(entity); free.Invoke(_entities,[entity]); }
            typeof(Player).GetField("_enemyInvincibilityFrames",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_player,100000f);
            var slots = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            var parts = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            FailIf(slots.Count != 1 || slots.Single().Value != 0 || _entities.RoomEnemyCount != 1,
                "Room$5:$78 must allocate one counted Ramrock$07 in source before-event order$05.");
            var seed = _random.CaptureState(); var rom = new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            foreach (var (header,index) in new byte[] {0xce,0xcf,0xa7,0x78,0x8e}.Select((header,index)=>(header,index))) rom[0xcc08+index*2]=header;
            rom[0xcc39]=8; rom[0xcdd1]=1; rom[0xd080]=0x11; rom[0xd081]=7; rom[0xd08b]=0x48; rom[0xd08d]=0x78;
            var reward = active.OfType<DungeonRewardRoomEntity>().Single(); int rewardSlot=0xd040+_entities.InteractionSlot(reward)*256;
            rom[rewardSlot]=1; rom[rewardSlot+1]=0x20; rom[rewardSlot+2]=1; rom[rewardSlot+11]=0x58; rom[rewardSlot+13]=0x78;
            rom[0xff94]=seed.Rng1; rom[0xff95]=seed.Rng2;
            int update=0; var states=new HashSet<int>();
            SomariaRom? gameplayRom=null; int gameplayRandomBase=0;
            int inputPressed=0,inputHeld=0,inputAngle=0xff;
            void Compare()
            {
                Vector2 camera=-_entities.ToScreen(Vector2.Zero);
                rom[0xffaa]=(byte)(int)camera.Y; rom[0xffac]=(byte)(int)camera.X;
                if(gameplayRom is null) rom.Update(_entities.FrameCounter,_player.EnemyContactPosition);
                else
                {
                    gameplayRom[0xffaa]=(byte)(int)camera.Y; gameplayRom[0xffac]=(byte)(int)camera.X;
                    gameplayRom[0xd02b]=127; // Same protected-Link fixture as the preceding actor trace.
                    gameplayRom.UpdateGameplay(inputPressed,inputHeld,inputAngle,_entities.FrameCounter);
                    inputPressed=0;
                    for(int address=0xcc00;address<0xe000;address++) rom[address]=gameplayRom[address];
                    rom[0xff94]=gameplayRom[0xff94]; rom[0xff95]=gameplayRom[0xff95];
                    FailIf(_player.PrecisePosition!=new Vector2(gameplayRom.Word(0xd00c)/256f,gameplayRom.Word(0xd00a)/256f),
                        $"Ramrock glove update{update+1}: Link runtime={_player.PrecisePosition}, ROM={gameplayRom.Word(0xd00c)/256f},{gameplayRom.Word(0xd00a)/256f}, input=${inputHeld:x2}/${inputAngle:x2}, roots="+
                        string.Join(";",parts.Where(p=>p.Key.Node is RamrockPart {Id:0x35,Subid:0 or 16}).Select(p=>{
                            var glove=(RamrockPart)p.Key.Node; int at=0xd0c0+p.Value*256;
                            return $"${p.Value:x2} runtime{glove.State}/{glove.Substate} ROM{gameplayRom[at+4]}/{gameplayRom[at+5]}";
                        })));
                }
                update++;
                foreach (var (entity,slot) in slots)
                {
                    var actor=(RamrockCharacter)entity.Node; int address=0xd080+slot*256;
                    if (actor.Record.Id==7) states.Add(actor.State);
                    FailIf(rom[address]==0 || actor.Record.Id!=rom[address+1] || actor.Subid!=rom[address+2] ||
                        actor.State!=rom[address+4] || actor.Substate!=rom[address+5] || actor.Counter1!=rom[address+6] || actor.Counter2!=rom[address+7] ||
                        actor.Speed!=rom[address+16] || actor.Angle!=rom[address+9] || actor.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) ||
                        unchecked((ushort)actor.ZFixed)!=rom.Word(address+14) || actor.NativeVisible!=rom[address+26] ||
                        actor.InvincibilityCounter!=unchecked((sbyte)rom[address+43]) || actor.AnimationParameter!=rom[address+0x21],
                        $"Ramrock slot${slot:x2} update{update}: runtime id/sub/state/substate/counters/speed/angle/XY/Z/visible/inv/anim="+
                        $"{actor.Record.Id}/{actor.Subid}/{actor.State}/{actor.Substate}/{actor.Counter1},{actor.Counter2}/{actor.Speed}/{actor.Angle}/{actor.Position}/{actor.ZFixed}/{actor.NativeVisible}/{actor.InvincibilityCounter}/{actor.AnimationParameter}; ROM="+
                        $"{rom[address+1]}/{rom[address+2]}/{rom[address+4]}/{rom[address+5]}/{rom[address+6]},{rom[address+7]}/{rom[address+16]}/{rom[address+9]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{rom.Word(address+14)}/{rom[address+26]}/{unchecked((sbyte)rom[address+43])}/{rom[address+0x21]}.");
                    FailIf(actor.HitCount!=rom[address+0x35] || actor.ShieldSignal!=rom[address+0x36] || actor.GloveSignal!=rom[address+0x37],
                        $"Ramrock update{update}: native phase/hand signals differ.");
                }
                int nativeEnemies=Enumerable.Range(0,16).Count(slot=>rom[0xd080+slot*256]!=0);
                FailIf(nativeEnemies!=slots.Count || _entities.RoomEnemyCount!=rom[0xcdd1],$"Ramrock update{update}: counted enemy allocation differs.");
                foreach(var (entity,slot) in parts.Where(pair=>pair.Key.Node is RamrockPart))
                {
                    var part=(RamrockPart)entity.Node; int address=0xd0c0+slot*256;
                    FailIf(rom[address]==0 || part.Id!=rom[address+1] || part.Subid!=rom[address+2] || part.State!=rom[address+4] ||
                        part.Substate!=rom[address+5] || part.Counter1!=rom[address+6] || part.Counter2!=rom[address+7] ||
                        part.Speed!=rom[address+16] || part.Angle!=rom[address+9] || part.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) ||
                        unchecked((ushort)part.ZFixed)!=rom.Word(address+14) || part.NativeVisible!=rom[address+26],
                        $"Ramrock PART${part.Id:x2}/slot${slot:x2} update{update}: runtime sub/state/substate/counters/speed/angle/XY/Z/visible="+
                        $"{part.Subid}/{part.State}/{part.Substate}/{part.Counter1},{part.Counter2}/{part.Speed}/{part.Angle}/{part.Position}/{part.ZFixed}/{part.NativeVisible}; ROM="+
                        $"{rom[address+2]}/{rom[address+4]}/{rom[address+5]}/{rom[address+6]},{rom[address+7]}/{rom[address+16]}/{rom[address+9]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{rom.Word(address+14)}/{rom[address+26]}.");
                }
                int nativeBossParts=Enumerable.Range(0,16).Count(slot=>rom[0xd0c0+slot*256]!=0 && rom[0xd0c1+slot*256] is 0x34 or 0x35 or 0x4f);
                FailIf(nativeBossParts!=parts.Keys.Count(entity=>entity.Node is RamrockPart),$"Ramrock update{update}: part allocation closure differs.");
                var rng=_random.CaptureState();
                int calls=gameplayRom is null?rom.RandomCalls:gameplayRandomBase+gameplayRom.RandomCalls;
                FailIf(rng.Rng1!=rom[0xff94] || rng.Rng2!=rom[0xff95] || rng.Calls-seed.Calls!=calls,$"Ramrock update{update}: global RNG differs.");
                if (update==3)
                {
                    // Declared closed-shutter input, after proving the intro
                    // waits for the shared entry signal. No boss state is set.
                    SomariaPrivate<BossShutterSignal>(_entities,"_bossShutterSignal").Clear(); rom[0xcc93]=0;
                }
            }
            void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:Compare);
            Step(450);
            FailIf(!states.Contains(9) || slots.Count!=3,"Ramrock must finish the source intro and enter fist mode with two counted hands.");
            var boss = _entities.Entities<RamrockCharacter>().Single(a=>a.Record.Id==7);
            int bossSlot = boss.Slot;
            // Declare the two prior exchanges to focus on the last real fist
            // reflection and the phase handoff, rather than replay a fight.
            boss.HitCount=2; rom[0xd080+bossSlot*256+0x35]=2;
            bool reflected=false;
            for (int wait=0;wait<1800 && boss.State==9;wait++)
            {
                var incoming=slots.Keys.FirstOrDefault(e=>e.Node is RamrockCharacter {Record.Id:5,Subid:0 or 1,Substate:3} hand &&
                    hand.Position.Y >= boss.Position.Y+12 && hand.Position.Y <= boss.Position.Y+36);
                if (incoming is not null)
                {
                    var hand=(RamrockCharacter)incoming.Node;
                    rom.HitWithSword(4,1,hand.Slot);
                    ((ILinkSwordStateAwareRoomEntity)incoming).SetLinkSwordState(SwordActionState.Swing,1);
                    FailIf(!((ISwordHittableRoomEntity)incoming).ApplySwordHit(hand.CollisionBounds,hand.Position-Vector2.Down,
                        1,EnemyKnockbackStrength.Normal,[]),"Ramrock's flying fist must admit a sword reflection.");
                    reflected=true; Step(60);
                }
                else Step(1);
            }
            FailIf(!reflected || boss.State!=10 || boss.HitCount!=0,
                "The third reflected fist must advance Ramrock to bomb form and clear its phase hit count.");
            Step(400);
            FailIf(boss.State!=10 || boss.Substate is not (3 or 4),"Ramrock must replace both fists with its bomb grabber and resume its native chase cycle.");
            for (int wait=0;wait<240 && !(boss.Substate==3 && boss.Counter2==1);wait++) Step(1);
            FailIf(boss.Substate!=3 || boss.Counter2!=1,"Ramrock must reach the final chase update before its bomb grab animation.");
            Step(1);
            boss.HitCount=2; rom[0xd080+bossSlot*256+0x35]=2;
            var grabber=_entities.Entities<RamrockCharacter>().Single(a=>a.Record.Id==5);
            var bomb=_entities.Spawn<BombEffect>(new BombSpawn(_player,new BombDatabase().Data,5,_=>{}));
            bomb.Throw(_player,Vector2I.Zero,Vector2I.Zero,0,0);
            // Declare one live grounded ITEM$03 in the source grabber window.
            // The gameplay enemy pass performs the consume and phase signal.
            Vector2 bombPosition=grabber.Position.Floor();
            typeof(BombEffect).GetField("_precisePosition",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(bomb,bombPosition);
            typeof(BombEffect).GetField("_zFixed",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(bomb,0);
            typeof(BombEffect).GetField("_setupPending",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(bomb,false);
            bomb.Position=bombPosition;
            var bombEntity=active.Single(e=>e.Node==bomb); int bombAddress=_entities.DynamicItemSlotOf(bombEntity)*256;
            rom[bombAddress]=1; rom[bombAddress+1]=3; rom[bombAddress+4]=3; rom[bombAddress+9]=0xff;
            rom[bombAddress+11]=(byte)bombPosition.Y; rom[bombAddress+13]=(byte)bombPosition.X;
            rom[bombAddress+0x26]=rom[bombAddress+0x27]=4;
            Step(89);
            FailIf(bomb.Finished || boss.HitCount!=2,"Ramrock's bomb grab window must stay closed for the first 89 updates of animation$02.");
            Step(1);
            FailIf(!bomb.Finished || (rom[bombAddress+0x2f]&0x10)==0 || boss.HitCount!=3,
                $"Ramrock bomb exchange: runtime finished={bomb.Finished}/state={bomb.State}/XY={bomb.Position}/Z={bomb.ZFixed}, boss={boss.State}/{boss.Substate}/sub{boss.Subid}/hits{boss.HitCount}/inv{boss.InvincibilityCounter}, grabber={grabber.Position}/sub{grabber.Substate}; ROM flag=${rom[bombAddress+0x2f]:x2}, bomb=${bombAddress:x4}/XY{bombPosition}.");
            for(int offset=0;offset<64;offset++) rom[bombAddress+offset]=0;
            for(int wait=0;wait<600 && !(boss.State==11 && boss.Substate==2);wait++) Step(1);
            FailIf(boss.State!=11 || boss.Substate!=2 || slots.Count!=3,
                "The third bomb must retire the grabber and initialize both native shield hands in seed form.");
            Step(650);
            boss.HitCount=2; rom[0xd080+bossSlot*256+0x35]=2;
            var bossEntity=slots.Single(pair=>pair.Value==bossSlot).Key;
            void SeedHit()
            {
                rom.HitWithSword(0x1b,0,bossSlot);
                FailIf(!((ISeedCollisionTarget)bossEntity).ApplySeedCollision(boss.CollisionBounds,boss.Position-Vector2.Down,
                    new SeedSatchelDatabase().Ember,0x1b,[]).Contact,"Ramrock's exposed seed body must admit a live seed collision.");
            }
            SeedHit(); Step(1);
            FailIf(boss.State!=11 || boss.HitCount!=3,"The third seed hit must retain seed form; the source checks$03 before incrementing var35.");
            Step(60); SeedHit(); Step(1);
            FailIf(boss.State!=12 || boss.HitCount!=0 || boss.Subid!=16,"The fourth seed hit must transition Ramrock to glove setup.");
            Step(400);
            _inventory.GiveTreasure(TreasureId.Bracelet,2); _inventory.SetScriptedEquippedItems(0,TreasureId.Bracelet);
            _player.Face(Vector2I.Up);
            gameplayRandomBase=rom.RandomCalls;
            gameplayRom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)_player.Position.X,(int)_player.Position.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            gameplayRom.InitializeLinkGameplay(); gameplayRom.InitializeLinkWalkingAnimation();
            gameplayRom.Word(0xd00a,(int)(_player.PrecisePosition.Y*256));
            gameplayRom.Word(0xd00c,(int)(_player.PrecisePosition.X*256));
            for(int page=0xd0;page<=0xdf;page++)
                for(int offset=0x40;offset<256;offset++) gameplayRom[page*256+offset]=rom[page*256+offset];
            for(int address=0xcc08;address<0xcc1c;address++) gameplayRom[address]=rom[address];
            gameplayRom[0xcc39]=8; gameplayRom[0xcdd1]=rom[0xcdd1];
            void Play(int count,Vector2 direction,bool hold=false,bool press=false)
            {
                inputAngle=direction==Vector2.Zero?0xff:CarriedObjectMotion.DirectionIndex((Vector2I)direction)*8;
                inputHeld=(hold?1:0)|(inputAngle switch{0=>0x40,8=>0x10,16=>0x80,24=>0x20,_=>0});
                inputPressed=press?1:0;
                StepGameplayUpdates(count,direction,hold?["attack"]:[],press?["attack"]:[],batch,Compare);
            }
            RamrockPart GrabIdleGlove()
            {
                var buffer=SomariaPrivate<List<INativeBraceletRoomEntity>>(_entities,"_grabbableObjects");
                for(int walk=0;walk<1800;walk++)
                {
                    var glove=_entities.Entities<RamrockPart>().FirstOrDefault(p=>p.Id==0x35 && p.Subid is 0 or 16 &&
                        p.State==4 && p.Counter1==0 && boss.Subid==18 && (boss.AttackParameter&32)==0 && p.Subid!=boss.AttackParameter);
                    if(glove is null) {Play(1,Vector2.Zero);continue;}
                    if(Math.Abs(_player.Position.X-glove.Position.X)>1)
                    {Play(1,_player.Position.X<glove.Position.X?Vector2.Right:Vector2.Left);continue;}
                    if(_player.Position.Y>glove.Position.Y+14) {Play(1,Vector2.Up);continue;}
                    if(_player.Position.Y<glove.Position.Y+11) {Play(1,Vector2.Down);continue;}
                    if(_player.FacingVector!=Vector2I.Up) {Play(1,Vector2.Up);continue;}
                    if(!buffer.Contains(glove)) {Play(1,Vector2.Zero);continue;}
                    FailIf(_collision.Collides(_player.Position),"Ramrock glove approach must remain on the original room floor.");
                    Play(1,Vector2.Zero,true,true);
                    FailIf(glove.State!=2,"Bracelet input must grab the source-published idle arm through reachable room geometry.");
                    return glove;
                }
                throw new InvalidOperationException("Ramrock did not expose a reachable idle glove within the bounded approach.");
            }
            var cancelledGlove=GrabIdleGlove();
            Play(24,Vector2.Down,true);
            FailIf(cancelledGlove.Counter2==0,"The short cancellation trace must stop before the native 96-count pull completes.");
            Play(1,Vector2.Zero);
            Play(60,Vector2.Zero);
            FailIf(boss.HitCount!=0 || cancelledGlove.State==2,"Cancelling a short pull must release Link and return the glove without boss damage.");
            var idleGlove=GrabIdleGlove();
            boss.HitCount=2; gameplayRom[0xd080+bossSlot*256+0x35]=2;
            Play(160,Vector2.Down,true);
            Play(1,Vector2.Zero);
            Play(220,Vector2.Zero);
            for(int wait=0;wait<400 && !_saveData.HasRoomFlag(5,0x78,0x80);wait++) Play(1,Vector2.Zero);
            FailIf(!boss.IsDead || _entities.RoomEnemyCount!=0 || !_saveData.HasRoomFlag(5,0x78,0x80),
                $"Ramrock final glove: dead={boss.IsDead}, state={boss.State}/{boss.Substate}/sub{boss.Subid}, hp={boss.Health}/hits={boss.HitCount}/signal={boss.GloveSignal}, count={_entities.RoomEnemyCount}, flag$80={_saveData.HasRoomFlag(5,0x78,0x80)}, glove={idleGlove.State}/{idleGlove.Substate}/counters{idleGlove.Counter1},{idleGlove.Counter2}.");
        }
        GD.Print("Validated clean-US Ramrock intro, two ordered hands, native phase signals and global RNG through gameplay.");
    }
}
