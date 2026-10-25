using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSpinyVariantsRom()
    {
        foreach (int subid in new[] { 0, 3 })
        foreach (bool batched in new[] { false,true })
        {
            int group=subid==0?1:0, room=subid==0?0x70:0x1a;
            InitializeCompletedIntroRoom(group,room);
            var data=EnemyDatabase.Shared;
            var row=data.GetRoomObjects(group,room).First(r=>r.Id==0x1b&&r.SubId==subid);
            var actor=new SpinyBeetleCharacter();
            actor.Initialize(data.ImportedEnemy(0x1b,subid),_currentRoom,new(row.X,row.Y),_random,
                new BraceletDatabase().Data,new BombDatabase().Data,_entities.ApplyThrownObjectHit,
                parent=>_entities.TryAllocateEnemy(_=>new SpinyBeetleCoverRoomEntity(new(parent,_random,_currentRoom,
                    new BraceletDatabase().Data,new BombDatabase().Data,_entities.ApplyThrownObjectHit),_sound.PlaySound,()=>_entities.PartSlotAvailable)) is not null);
            var adapter=new SpinyBeetleRoomEntity(actor,data.EnemyHandlers.ResolveHandler(row).CombatSource(row,0),_sound.PlaySound);
            _entities.RegisterEnemySlot(adapter,0);_entities.AddEntity(adapter);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(row.X+32,row.Y));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            var seed=_random.CaptureState();var rom=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            rom[0xff94]=seed.Rng1;rom[0xff95]=seed.Rng2;rom[0xcc08]=0x94;
            const int at=0xd080;rom[at]=1;rom[at+1]=0x1b;rom[at+2]=(byte)subid;rom[at+11]=(byte)row.Y;rom[at+13]=(byte)row.X;
            int updates=0;bool frozen=true;_entities.TextActiveSource=()=>frozen;
            void Compare()
            {
                rom[0xcba0]=(byte)(frozen?1:0);rom.Update(_entities.FrameCounter,_player.Position);updates++;
                var cover=_entities.Entities<SpinyBeetleCoverCharacter>().SingleOrDefault();
                FailIf((int)actor.State!=rom[at+4]||actor.Counter1!=rom[at+6]||actor.Angle!=rom[at+9]||
                    actor.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                    actor.ParentVisible!=((rom[at+0x1a]&128)!=0)||(cover is null)!=(rom[0xd180]==0)||
                    cover is not null && (cover.Position!=new Vector2(rom.Word(0xd18c)/256f,rom.Word(0xd18a)/256f)||
                    cover.ZHigh!=unchecked((sbyte)rom[0xd18f])||cover.Health!=rom[0xd1a9]),
                    $"Spiny$1b:${subid:x2} update{updates}: parent state{actor.State}/{rom[at+4]}, counter{actor.Counter1}/{rom[at+6]}, angle{actor.Angle}/{rom[at+9]}, XY{actor.Position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}, parent-visible{actor.ParentVisible}/{rom[at+0x1a]:x2}, cover{cover?.Position}/{rom.Word(0xd18c)/256f},{rom.Word(0xd18a)/256f}, Z{cover?.ZHigh}/{unchecked((sbyte)rom[0xd18f])}.");
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls),$"Spiny$1b:${subid:x2} update{updates}: independent parent/child RNG differs.");
            }
            StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
            frozen=false;StepGameplayUpdates(180,Vector2.Zero,batched:batched,afterUpdate:Compare);
            frozen=true;StepGameplayUpdates(4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            frozen=false;StepGameplayUpdates(100,Vector2.Zero,batched:batched,afterUpdate:Compare);
            var child = _entities.EntityAdapters<SpinyBeetleCoverRoomEntity>().Single();
            var coverActor = _entities.Entities<SpinyBeetleCoverCharacter>().Single();
            // Strike the outer cover edge: its radius is six, while the
            // hidden parent has radius three and must remain outside this hit.
            var edge = new Rect2(coverActor.Position.Floor()+new Vector2(4,-6),new Vector2(12,12));
            if(subid==0&&batched)
            {
                rom[0xd701]=ItemId.EmberSeed;rom.HitWithSword(ItemCollisionType.EmberSeed,2,enemySlot:1,itemOffsetX:10);
                new SeedSatchelDatabase().TryGet(ItemId.EmberSeed,out var ember);
                var spawns=new System.Collections.Generic.List<RoomEntitySpawn>();
                FailIf(!child.ApplySeedCollision(edge,coverActor.Position+Vector2.Right*10,ember,ItemCollisionType.EmberSeed,spawns).Contact,
                    "$58:$00 must accept the native Ember burn and checked PART$12 allocation.");
                foreach(var spawn in spawns)_entities.TryCreatePart(spawn);
                StepGameplayUpdates(65,Vector2.Zero,batched:batched,afterUpdate:Compare);
            }
            else
            {
                rom.HitWithSword(ItemCollisionType.L1Sword,3,enemySlot:1,itemOffsetX:10);
                child.ApplyItemCollision((RoomEntityItemCollision)ItemCollisionType.L1Sword,edge,
                    coverActor.Position+Vector2.Right*10,3,new System.Collections.Generic.List<RoomEntitySpawn>());
                StepGameplayUpdates(5,Vector2.Zero,batched:batched,afterUpdate:Compare);
            }
            FailIf(subid==0&&actor.CoverProtects||subid==3&&!actor.CoverProtects,
                "Sword must destroy only the $00 bush; the $03 rock protects its parent.");
            if (subid==3)
            {
                rom[0xd701]=ItemId.EmberSeed;rom.HitWithSword(ItemCollisionType.EmberSeed,2,enemySlot:1,itemOffsetX:10);
                new SeedSatchelDatabase().TryGet(ItemId.EmberSeed,out var ember);
                var response=child.ApplySeedCollision(new Rect2(coverActor.Position.Floor()+new Vector2(4,-6),new Vector2(12,12)),
                    coverActor.Position+Vector2.Right*10,ember,ItemCollisionType.EmberSeed,new System.Collections.Generic.List<RoomEntitySpawn>());
                FailIf(!response.Contact||!response.DisableCollision||response.Effect!=SeedHitResult.Activate,
                    "$58:$03 effect$20 must disable the projectile collision without burning or damaging the rock.");
                StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
                _player.EndCutsceneControl();
                _inventory.GiveTreasure(TreasureId.Bracelet,1);_inventory.SetScriptedEquippedItems(0,TreasureId.Bracelet);
                int wait=0;
                while(actor.State!=SpinyBeetleState.CoveredWaiting&&wait++<60)
                    StepGameplayUpdates(1,Vector2.Zero,batched:batched);
                // Stage on clear floor outside the cover, then approach it
                // through the unchanged room's collision geometry.
                Vector2 approach = new[] { Vector2.Down,Vector2.Right,Vector2.Left,Vector2.Up }.First(direction =>
                    Enumerable.Range(8,17).All(distance => !_collision.Collides(coverActor.Position-direction*distance)));
                Vector2 start=coverActor.Position-approach*24;
                _player.WarpTo(start);StepGameplayUpdates(8,approach,batched:batched);
                FailIf(_collision.Collides(_player.Position),"Spiny rock approach crossed solid geometry.");
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
                StepGameplayUpdates(14,Vector2.Zero,batched:batched);
                FailIf(!_player.IsCarryingObject||actor.CoverProtects||actor.State!=SpinyBeetleState.ExposedWaiting,
                    $"Reachable $58:$03 pickup must reveal $1b:$03 through the real item/Link/enemy loop: Link{_player.Position}, cover{coverActor.Position}, carry{_player.IsCarryingObject}, state{actor.State}.");
                _transitions.BeginScroll(_player,Vector2I.Right,0x1b);
                int scroll=0;
                while(_transitions.IsTransitioning&&scroll++<200) StepGameplayUpdates(1,Vector2.Zero,batched:batched);
                FailIf(_transitions.IsTransitioning||!_player.IsCarryingObject||!_entities.Entities<SpinyBeetleCoverCharacter>().Contains(coverActor),
                    "The held $58:$03 allocation must survive the actual $0:1a->$0:1b scroll and retain Link's carrying state.");
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
                StepGameplayUpdates(70,Vector2.Zero,batched:batched);
                FailIf(_player.IsCarryingObject||_entities.Entities<SpinyBeetleCoverCharacter>().Contains(coverActor),
                    "Thrown $58:$03 must release Link and its physical enemy slot after landing.");
            }
            _entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
        InitializeCompletedIntroRoom(1,0x70);
        var database=EnemyDatabase.Shared;
        var sourceRow=database.GetRoomObjects(1,0x70).Single(row=>row.Id==0x1b);
        var pending=new SpinyBeetleCharacter();
        pending.Initialize(database.ImportedEnemy(0x1b,0),_currentRoom,new(sourceRow.X,sourceRow.Y),_random,
            new BraceletDatabase().Data,new BombDatabase().Data,_entities.ApplyThrownObjectHit,
            parent=>_entities.TryAllocateEnemy(_=>new SpinyBeetleCoverRoomEntity(new(parent,_random,_currentRoom,
                new BraceletDatabase().Data,new BombDatabase().Data,_entities.ApplyThrownObjectHit),_sound.PlaySound,()=>_entities.PartSlotAvailable)) is not null);
        var pendingAdapter=new SpinyBeetleRoomEntity(pending,database.EnemyHandlers.ResolveHandler(sourceRow).CombatSource(sourceRow,0),_sound.PlaySound);
        _entities.RegisterEnemySlot(pendingAdapter,0);_entities.AddEntity(pendingAdapter);
        EnemyAiSlotReservation? release=null;
        for(int slot=1;slot<16;slot++)
        {
            var reservation=new EnemyAiSlotReservation();_entities.RegisterEnemySlot(reservation,slot);_entities.AddEntity(reservation);
            if(slot==1)release=reservation;
        }
        _player.BeginCutsceneControl();_entities.DisableLinkCollisionsAndMenu();
        int calls=_random.Calls;
        StepGameplayUpdates(3,Vector2.Zero);
        FailIf(pending.State!=SpinyBeetleState.Uninitialized||_entities.Entities<SpinyBeetleCoverCharacter>().Count!=0||_random.Calls!=calls+3,
            "$1b state0 must retry a full ENEMY pool and repeat exactly its enemyStandardUpdate RNG draw on each attempt.");
        _entities.RetireEntity(release!);calls=_random.Calls;
        StepGameplayUpdates(1,Vector2.Zero);
        FailIf(pending.State!=SpinyBeetleState.CoveredWaiting||_entities.ResolveEnemySlot(1) is not SpinyBeetleCoverRoomEntity||_random.Calls!=calls+2,
            "$1b must allocate its uncounted cover in the released physical slot; both parent and later child initialize in this update.");
        _entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        GD.Print("Validated Spiny Beetle $1b:$00/$03 independent $58 cover slots, exact full-pool retry/RNG, charge/wait boundaries, freezes, bush destruction and gameplay rock lift/throw.");
    }
}
