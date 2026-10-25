using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMissingEnemyPlacements()
    {
        // objects/ages/enemyData.s: all formerly suppressed placed variants.
        var placements = new (int Group,int Room,int Id,int Subid,int Count)[]
        {
            (1,0x70,0x1b,0,1),(1,0x90,0x1b,0,1),(1,0x1b,0x1b,0,1),
            (1,0x1d,0x1b,0,1),(0,0x2b,0x1b,0,1),(0,0x4d,0x1b,0,1),
            (0,0x1a,0x1b,3,1),(0,0x2c,0x1b,3,1),
            (0,0x2b,0x41,1,2),(0,0x3b,0x41,1,2),(0,0x0c,0x41,1,2),(0,0x1d,0x41,1,2),
            (5,0x95,0x0e,5,2),(5,0xa4,0x17,1,3),(5,0xa8,0x17,1,4),(5,0xa5,0x17,1,4)
        };
        int total=0;
        foreach(var placement in placements)
        {
            InitializeCompletedIntroRoom(placement.Group,placement.Room,clearEntities:false);
            int count=placement.Id switch
            {
                0x1b=>_entities.Entities<SpinyBeetleCharacter>().Count(e=>e.Record.SubId==placement.Subid),
                0x41=>_entities.Entities<CrowCharacter>().Count(e=>e.Record.SubId==placement.Subid),
                0x17=>_entities.Entities<GhiniCharacter>().Count(e=>e.Record.SubId==placement.Subid),
                _=>_entities.Entities<BladeTrapCharacter>().Count(e=>e.Record.SubId==placement.Subid)
            };
            FailIf(count!=placement.Count,$"Room ${placement.Group:x1}:${placement.Room:x2} must instantiate {placement.Count} ENEMY${placement.Id:x2}:${placement.Subid:x2}, got {count}.");
            total+=count;
        }
        FailIf(total!=29,"The 17 source placement records must produce all 29 previously omitted enemy instances.");
        GD.Print("Validated all 29 formerly omitted enemy instances from their 17 clean-US placement records through ordinary room construction.");
    }

    private void ValidateMissingEnemyVariantsRom()
    {
        foreach (int id in new[] { 0x41, 0x17, 0x0e })
        foreach (bool batched in new[] { false, true })
        {
            InitializeCompletedIntroRoom(id == 0x0e ? 5 : 0, id == 0x0e ? 0x95 : 0x2b);
            _inventory.SetScriptedEquippedItems(0,0); _player.WarpTo(id==0x0e?new(104,120):new(96,64)); _player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            var data = EnemyDatabase.Shared;
            int subid = id == 0x0e ? 5 : 1;
            var row = data.GetRoomObjects(id == 0x0e ? 5 : id == 0x17 ? 5 : 0,
                id == 0x0e ? 0x95 : id == 0x17 ? 0xa4 : 0x2b).First(r => r.Id == id && r.SubId == subid);
            var source = data.EnemyHandlers.ResolveHandler(row).CombatSource(row,0);
            EnemyCharacter actor;
            IRoomEntity adapter;
            if (id == 0x41)
            {
                data.TryGetCrowDefinition(row, out var record);
                var crow = new CrowCharacter();
                crow.Initialize(record,_currentRoom,new(40,64),_random,
                    () => _entities.Entities<CrowCharacter>().Count(c => c.OccupiesAttackLane));
                actor = crow; adapter = new CrowRoomEntity(crow,source,_sound.PlaySound);
            }
            else if (id == 0x17)
            {
                var ghini = new GhiniCharacter(); ghini.Initialize(data.ImportedEnemy(id,subid),_currentRoom,new(40,64),_random);
                actor = ghini; adapter = new SpawnGhiniRoomEntity(ghini,source,_sound.PlaySound,
                    () => _random.Next().Value, () => _entities.PartSlotAvailable);
            }
            else
            {
                var trap = new BladeTrapCharacter(); trap.Initialize(data.ImportedEnemy(id,subid),_currentRoom,new(56,120),_random,_sound.PlaySound);
                actor = trap; adapter = new BladeTrapRoomEntity(trap,source,_sound.PlaySound);
            }
            _entities.RegisterEnemySlot(adapter,0); _entities.AddEntity(adapter);
            var seed = _random.CaptureState(); var rom = new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            rom[0xff94]=seed.Rng1; rom[0xff95]=seed.Rng2;
            rom[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+id*4];
            const int at=0xd080; rom[at]=1;rom[at+1]=(byte)id;rom[at+2]=(byte)subid;rom[at+11]=(byte)actor.Position.Y;rom[at+13]=(byte)actor.Position.X;
            bool frozen=true; int updates=0,cycles=0,lastState=0; bool sawPastCenter=false;
            _entities.TextActiveSource=()=>frozen;
            void Compare()
            {
                rom[0xcba0]=(byte)(frozen?1:0);rom.Update(_entities.FrameCounter,_player.Position);updates++;
                int state=actor switch { CrowCharacter crow=>crow.SourceState,GhiniCharacter ghini=>(int)ghini.State,_=>(int)((BladeTrapCharacter)actor).State };
                int counter=actor switch { CrowCharacter crow=>crow.Counter1,GhiniCharacter ghini=>ghini.Counter,_=>((BladeTrapCharacter)actor).Counter };
                int angle=actor switch { CrowCharacter crow=>crow.Angle,GhiniCharacter ghini=>ghini.Angle,_=>((BladeTrapCharacter)actor).Angle };
                Vector2 position=actor is CrowCharacter c?c.PrecisePosition:actor.Position;
                if(state!=lastState){cycles++;lastState=state;}
                if(actor is BladeTrapCharacter {State:BladeTrapState.Charging}&&actor.Position.X>135)sawPastCenter=true;
                FailIf(state!=rom[at+4]||counter!=rom[at+6]||angle!=rom[at+9]||
                    position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                    actor.Visible!=((rom[at+0x1a]&128)!=0)||
                    actor.AnimationParameter!=rom[at+0x21]||actor.Health!=rom[at+0x29]||
                    actor is GhiniCharacter g && (g.Speed!=rom[at+16]||g.ZFixed!=unchecked((short)rom.Word(at+14))||g.StunCounter!=rom[at+0x2e]),
                    $"ENEMY${id:x2}:${subid:x2} update{updates}/batch{batched}: state{state:x2}/{rom[at+4]:x2}, counter{counter}/{rom[at+6]}, angle{angle}/{rom[at+9]}, XY{position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}, visible{actor.Visible}/{rom[at+0x1a]:x2}, anim{actor.AnimationParameter}/{rom[at+0x21]}.");
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls),
                    $"ENEMY${id:x2}:${subid:x2} update{updates}: common and species RNG differs ({_random.Calls-seed.Calls}/{rom.RandomCalls}).");
            }
            StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
            frozen=false;StepGameplayUpdates(55,Vector2.Zero,batched:batched,afterUpdate:Compare);
            frozen=true;StepGameplayUpdates(4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            frozen=false;StepGameplayUpdates(id==0x17?1200:650,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(cycles<5||id==0x0e&&!sawPastCenter,$"ENEMY${id:x2}:${subid:x2} must exercise repeated cycles and the green trap's unlimited range (cycles{cycles}, past-center{sawPastCenter}).");
            if (actor is GhiniCharacter ghiniActor)
            {
                rom.HitWithSword(ItemCollisionType.L1Sword,1);
                FailIf(!((IItemCollisionHittableRoomEntity)adapter).ApplyItemCollision(
                    (RoomEntityItemCollision)ItemCollisionType.L1Sword,actor.CollisionBounds,
                    actor.Position.Floor()-Vector2.Right,1,new System.Collections.Generic.List<RoomEntitySpawn>()),
                    "$17:$01 must accept source sword damage and recoil after spawning.");
                StepGameplayUpdates(40,Vector2.Zero,batched:batched,afterUpdate:Compare);
                rom.HitWithSword(ItemCollisionType.L1Boomerang,0);
                ghiniActor.ApplyBoomerangStun(120);ghiniActor.InvincibilityCounter=-16;
                StepGameplayUpdates(245,Vector2.Zero,batched:batched,afterUpdate:Compare);
            }
            _entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
        GD.Print("Validated $41:$01, $17:$01 and $0e:$05 complete repeated state cycles, exact waits, motion, animation, RNG and text freezes through split/batched gameplay against the clean US ROM.");
    }
}
