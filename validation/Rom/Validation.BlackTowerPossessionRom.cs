using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerPossessionRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            for(int heart=0;heart<11;heart++)_inventory.GiveTreasure(TreasureId.HeartContainer,4);
            _inventory.RefillHealth();_statusBar.SynchronizeHealth();LoadValidationRoom(4,0xfc);
            _entities.LoadRoom(4,_currentRoom,EnemyPlacementContext.Scrolling(Vector2I.Up,0xa7));
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x78,0xa8));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position),"Ambi must enter room $4:fc through its substituted source shutter at $a7.");
            var random=_random.CaptureState();
            var rom=new SomariaRom(_saveData,random,_currentRoom,0,0x78,0xa8)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=0xd0;
            rom[0xcd16]=0;rom[0xcd17]=0xd0;rom[0xffaa]=48;rom[0xffac]=40;
            var door=_entities.EntityAdapters<DungeonDoorRoomEntity>().Single();
            int doorAddress=0xd040+_entities.InteractionSlot(door)*256;
            rom[doorAddress]=1;rom[doorAddress+1]=0x1e;rom[doorAddress+2]=0x0a;rom[doorAddress+11]=0xa7;
            var ambi=_entities.EntityAdapters<BlackTowerPossessionActor>().Single();
            int ambiAddress=0xd040+_entities.InteractionSlot(ambi.Node)*256;
            rom[ambiAddress]=1;rom[ambiAddress+1]=0x4d;rom[ambiAddress+2]=6;rom[ambiAddress+11]=0x48;rom[ambiAddress+13]=0x78;
            int update=0,generation=0,hold=0,texts=0;
            bool sawSpider=false,sawProjectile=false;
            var sounds=_sound.AttachPlayRequestAudit();
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.UpdateGameplay(0,0,0xff,++update);rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                foreach(var actor in _entities.EntityAdapters<BlackTowerPossessionActor>())
                {
                    int address=0xd040+_entities.InteractionSlot(actor.Node)*256;
                    FailIf(actor.State!=rom[address+4]||actor.Substate!=rom[address+5]||
                        actor.Id==0x3e&&actor.SubId==2&&actor.Counter!=rom[address+6]||
                        actor.PrecisePosition!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f),
                        $"Black Tower INTERAC ${actor.Id:x2}:${actor.SubId:x2} update {update}: port={actor.State}/{actor.PrecisePosition}, ROM={rom[address+4]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}.");
                }
                foreach(var enemy in _entities.EntityAdapters<PossessedNayruRoomEntity>())
                {
                    int address=0xd080+enemy.Slot*256;var actor=enemy.Actor;
                    FailIf(actor.State!=rom[address+4]||actor.Counter!=rom[address+6]||actor.Counter2!=rom[address+7]||
                        actor.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f)||actor.SubId<3&&actor.State>=9&&actor.RemainingHits!=rom[address+0x33],
                        $"Ambi possession $61:${actor.SubId:x2} update {update}: port={actor.State}/{actor.Counter}/{actor.Counter2}/{actor.Position}/{actor.RemainingHits}, ROM={rom[address+4]}/{rom[address+6]}/{rom[address+7]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{rom[address+0x33]}.");
                }
                foreach(var fairy in _entities.EntityAdapters<VeranFairyRoomEntity>())
                {
                    int address=0xd080+fairy.Slot*256;var actor=fairy.Actor;
                    FailIf(actor.State!=rom[address+4]||actor.Substate!=rom[address+5]||actor.Counter!=rom[address+6]||actor.Counter2!=rom[address+7]||
                        actor.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f)||
                        actor.ZFixed!=unchecked((short)rom.Word(address+14))||actor.Speed!=rom[address+16]||actor.Visible!=((rom[address+0x1a]&128)!=0),
                        $"Fairy Veran reveal update {update}: port={actor.State}/{actor.Substate}/{actor.Counter}/{actor.Counter2}/{actor.Position}/{actor.ZFixed}/{actor.Speed}/{actor.Visible}; " +
                        $"ROM={rom[address+4]}/{rom[address+5]}/{rom[address+6]}/{rom[address+7]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{unchecked((short)rom.Word(address+14))}/{rom[address+16]}/{rom[address+0x1a]:x2}.");
                }
                sawSpider|=_entities.EntityAdapters<VeranSpiderRoomEntity>().Any();
                sawProjectile|=_entities.EntityAdapters<VeranProjectileRoomEntity>().Any();
                var actual=_random.CaptureState();
                FailIf(actual.Rng1!=rom[0xff94]||actual.Rng2!=rom[0xff95]||actual.Calls-random.Calls!=rom.RandomCalls,
                    $"Ambi shared RNG differs update {update}: port={actual.Calls-random.Calls}, ROM={rom.RandomCalls}; " +
                    $"fairy={string.Join(',',_entities.EntityAdapters<VeranFairyRoomEntity>().Select(f=>$"{f.Actor.State}:{f.Actor.Substate}/{f.Actor.Counter}"))}, " +
                    $"parts={string.Join(',',Enumerable.Range(0,16).Where(s=>rom[0xd0c0+s*256]!=0).Select(s=>$"{s:x2}:{rom[0xd0c1+s*256]:x2}/{rom[0xd0c4+s*256]}"))}.");
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    _runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=rom[WramAddress.wDisabledObjects]||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled)!=rom[WramAddress.wMenuDisabled]||
                    _entities.BossEntrySignal!=rom[0xcc93]||_entities.RoomEnemyCount!=rom[0xcdd1]||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Ambi scene handoff update {update}: Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, shutter={_entities.BossEntrySignal:x2}/{rom[0xcc93]:x2}, " +
                    $"masks={_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/{rom[WramAddress.wDisabledObjects]:x2},{_runtimeState.ReadWramByte(WramAddress.wMenuDisabled):x2}/{rom[WramAddress.wMenuDisabled]:x2}, enemies={_entities.RoomEnemyCount}/{rom[0xcdd1]}, " +
                    $"sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(generation!=rom.TextGeneration)
                {
                    int[] expected=[0x1715,0x5a11,0x1718,0x1719,0x5a0e,0x5a0f,0x5a10];
                    FailIf(texts>=expected.Length||rom.Word(0xcba2)!=expected[texts]||!_dialogue.IsOpen,
                        $"Ambi text {texts} differs: native=${rom.Word(0xcba2):x4}; the Ambi ghost must not show Nayru's taunt.");
                    generation=rom.TextGeneration;texts++;hold=3;
                }
                if(hold>0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
            }
            for(int bound=0;bound<2400&&(!sawSpider||!sawProjectile);bound+=4)
                StepGameplayUpdates(4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(texts!=1||!sawSpider||!sawProjectile||_entities.PlayerMenusDisabled,
                "Ambi introduction must close the real shutter, release Link and exercise both projectile and spider attacks.");
            var boss=_entities.EntityAdapters<PossessedNayruRoomEntity>().Single();
            _inventory.GiveTreasure(TreasureId.SwitchHook,1);_inventory.EquipA(TreasureId.SwitchHook);
            for(int address=0xc5b0;address<0xcb00;address++)rom[address]=_saveData.ReadWramByte(address);
            FailIf(!new SeedSatchelDatabase().TryGet(ItemId.MysterySeed,out var mystery),"Ambi exposure requires the imported Mystery seed.");
            var spawns=new List<RoomEntitySpawn>();
            for(int remaining=3;remaining>0;remaining--)
            {
                for(int wait=0;wait<600&&!boss.Actor.CollisionEnabled;wait++)StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
                rom.HitEnemyWithItem(boss.Slot,0x1a,0,ItemId.MysterySeed);
                FailIf(!boss.ApplySeedCollision(boss.Actor.CollisionBounds,boss.Actor.Position-Vector2.Right,mystery,0x1a,spawns).Contact,
                    "Mystery must collapse possessed Ambi through the shared collision owner.");
                StepGameplayUpdates(42,Vector2.Zero,batched:batched,afterUpdate:Compare);
                var ghost=_entities.EntityAdapters<PossessedNayruRoomEntity>().Single(enemy=>enemy.Actor.SubId==2);
                var approach=new[]{Vector2I.Down,Vector2I.Up,Vector2I.Right,Vector2I.Left}.FirstOrDefault(direction=>
                    Enumerable.Range(12,13).All(distance=>Enumerable.Range(-5,11).All(side=>
                        !_currentRoom.IsSolid(ghost.Actor.Position.Floor()+(Vector2)direction*distance+new Vector2(-direction.Y,direction.X)*side))));
                FailIf(approach==Vector2I.Zero,"Ambi's emerged ghost needs a clear source-floor corridor for Link's Switch Hook.");
                Vector2 origin=ghost.Actor.Position.Floor()+(Vector2)approach*24;
                _player.WarpTo(origin);_player.Face(-approach);
                int hookDirection=approach==Vector2I.Down?0:approach==Vector2I.Up?2:approach==Vector2I.Right?3:1;
                rom.Word(0xd00c,(int)origin.X<<8);rom.Word(0xd00a,(int)origin.Y<<8);rom[0xd008]=(byte)hookDirection;rom[0xd009]=(byte)(hookDirection*8);
                // WarpTo clears Link's live collision counters; preserve that
                // same explicit floor-position setup in the ROM fixture.
                rom[0xd02b]=rom[0xd02d]=rom[0xd02a]=rom[0xd028]=0;
                int input=1;
                void HookCompare()
                {
                    int address=0xd080+ghost.Slot*256;
                    rom.AdvanceWarpPalette();rom.UpdateGameplay(input,input,0xff,++update);rom.AdvanceCameraPosition();input=0;
                    FailIf(ghost.Actor.State!=rom[address+4]||ghost.Actor.Substate!=rom[address+5]||
                        ghost.Actor.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f)||
                        _player.IsUsingSwitchHook!=(rom[0xd200]!=0)||_entities.SwitchHook!.ExchangeState!=rom[0xccdd],
                        $"Ambi ghost actual Switch Hook handoff differs update {update}: port={ghost.Actor.State}/{ghost.Actor.Substate}/{ghost.Actor.Position}, ROM={rom[address+4]}/{rom[address+5]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}.");
                }
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],afterUpdate:HookCompare);
                for(int wait=0;wait<100&&(ghost.Actor.State!=11||_player.IsUsingSwitchHook);wait++)StepGameplayUpdates(1,Vector2.Zero,afterUpdate:HookCompare);
                FailIf(ghost.Actor.State!=11||_player.IsUsingSwitchHook,"Ambi's emerged ghost must exchange, land and release Link.");
                StepGameplayUpdates(40,Vector2.Zero,batched:batched,afterUpdate:Compare);
                ghost.SetLinkSwordState(SwordActionState.Swing,1);rom.HitEnemyWithItem(ghost.Slot,4,1);
                FailIf(!ghost.ApplySwordHit(ghost.Actor.CollisionBounds,ghost.Actor.Position-Vector2.Right,1,EnemyKnockbackStrength.Low,spawns),"Human Veran must accept the post-hook sword hit.");
                StepGameplayUpdates(2,Vector2.Zero,batched:batched,afterUpdate:Compare);
                FailIf(ghost.Actor.RemainingHits!=remaining-1,"Ambi's shared hit counter must decrement once per exposure.");
                if(remaining>1)StepGameplayUpdates(70,Vector2.Zero,batched:batched,afterUpdate:Compare);
            }
            for(int wait=0;wait<240&&!_entities.EntityAdapters<BlackTowerPossessionActor>().Any(actor=>actor.SubId==7);wait++)
                StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            FailIf(texts!=2||!_entities.EntityAdapters<BlackTowerPossessionActor>().Any(actor=>actor.Id==0x4d&&actor.SubId==7)||
                _entities.EntityAdapters<VeranSpiderRoomEntity>().Any(),
                "Third Ambi exposure must show TX_5611, clear spiders, fade through collapsed $61:$03 and allocate rescued Ambi $4d:$07.");
            BlackTowerPossessionActor? rescueGhost=null;
            for(int wait=0;wait<360&&(rescueGhost is null||rescueGhost.Substate!=1);wait++)
            {
                StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
                rescueGhost=_entities.EntityAdapters<BlackTowerPossessionActor>().SingleOrDefault(actor=>actor.Id==0x3e&&actor.SubId==2);
            }
            FailIf(texts!=4||rescueGhost is null||rescueGhost.Substate!=1||rescueGhost.Counter!=60||
                _entities.EntityAdapters<BlackTowerPossessionActor>().Any(actor=>actor.Id==0x4d),
                "Ambi's rescue must show TX_1318/TX_1319, request Link state $09, walk out and allocate ghost Veran $3e:$02.");
            StepGameplayUpdates(59,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(rescueGhost!.Counter!=1||rom[0xd004]!=8,
                "Ghost Veran's 60-update wait must leave Link in the shared state $08 hold after his rescue jump.");
            for(int wait=0;wait<500&&!_entities.EntityAdapters<VeranFairyRoomEntity>().Any(fairy=>fairy.Actor.State==2);wait++)
                StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            FailIf(texts!=7||!_entities.EntityAdapters<VeranFairyRoomEntity>().Any(fairy=>fairy.Actor.State==2)||
                _entities.EntityAdapters<ArmMimicRoomEntity>().Count()!=4,
                "Fairy Veran must replace ghost $3e:$02, strike six lightning bolts, remove pillars and reveal four Link Mimics before releasing controls.");
            foreach(int packed in new[]{0x23,0x33,0x63,0x73,0x45,0x55,0x49,0x59,0x2b,0x3b,0x6b,0x7b})
                FailIf(_currentRoom.GetMetatile(new((packed&15)*16+8,(packed&0xf0)+8))!=0xa5||rom[0xcf00+packed]!=0xa5,
                    $"Fairy Veran's source pillar ${packed:x2} must become tile $a5.");
            LoadValidationRoom(4,0xfd);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<BlackTowerPossessionActor>().Any()||_player.CutsceneControlled||
                _entities.PlayerMenusDisabled||_runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0,
                "Cancelling Ambi's rescue must release its actor slots, Link hold and input masks.");
        }
    }

    private void ValidateBlackTowerPossessionCancellation()
    {
        foreach(int cancelAt in new[]{1,24,90,250})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xfc);
            _entities.LoadRoom(4,_currentRoom,EnemyPlacementContext.Scrolling(Vector2I.Up,0xa7));
            _player.WarpTo(new(0x78,0xa8));_player.Face(Vector2I.Up);
            for(int update=0;update<cancelAt;update++)
            {StepGameplayUpdates(1,Vector2.Zero);if(_dialogue.IsOpen)_dialogue.Close();}
            LoadValidationRoom(4,0xfd);StepGameplayUpdates(4,Vector2.Zero);
            FailIf(_entities.EntityAdapters<BlackTowerPossessionActor>().Any()||
                _entities.EntityAdapters<PossessedNayruRoomEntity>().Any()||_entities.EntityAdapters<VeranSpiderRoomEntity>().Any()||
                _entities.PlayerMenusDisabled||_player.CutsceneControlled||_transitions.NativePaletteFadeActive,
                $"Leaving Ambi $4:fc during update {cancelAt} retained a cutscene actor, enemy, mask or palette thread.");
            LoadValidationRoom(4,0xfc);StepGameplayUpdates(1,Vector2.Zero);
            FailIf(!_entities.EntityAdapters<BlackTowerPossessionActor>().Any(actor=>actor.Id==0x4d&&actor.SubId==6),
                "Leaving an unfinished Ambi encounter must retain its source re-entry behavior.");
        }
        ReinitializeGameplayForValidation();_saveData.SetRoomFlag(4,0xfc,0x80);LoadValidationRoom(4,0xfc);
        StepGameplayUpdates(2,Vector2.Zero);
        FailIf(_entities.EntityAdapters<BlackTowerPossessionActor>().Any()||_entities.EntityAdapters<PossessedNayruRoomEntity>().Any()||
            _entities.PlayerMenusDisabled,
            "Room $4:fc flag $80 must suppress Ambi $4d:$06 before its ghost, count or input initialization.");
    }
}
