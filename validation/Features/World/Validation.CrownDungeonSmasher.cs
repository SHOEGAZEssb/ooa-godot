using Godot;
using System.Linq;
using System;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownDungeonSmasherHeldExpiration()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] {false,true})
        {
            _saveData.SetRoomFlag(4,0xb4,0x80,false);
            LoadValidationRoom(4,0xb4); _player.WarpTo(new(64,88));
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet,1); _inventory.EquipA(InventoryState.ItemBracelet);
            void Step(int count,Vector2 movement = default,bool press = false) =>
                StepGameplayUpdates(count, movement, press ? ["attack"] : [], press ? ["attack"] : [], batched: batch);
            Step(2); Step(12,Vector2.Right); Step(1,press:true); Step(13);
            var ball = _entities.Entities<SmasherCharacter>().Single(actor => actor.IsBall);
            FailIf(!_player.IsCarryingObject || ball.State != 2 || ball.GrabSubstate != 1,
                "$74 expiration fixture must reach and lift the ball through room collision geometry before the parent takes it.");
            if ((_entities.FrameCounter & 1) != 0) Step(1);
            // Start immediately before the source's 180th even tick. This
            // isolates expiration rather than running a long survival fight.
            typeof(SmasherCharacter).GetProperty("ExpirationCounter",flags)!.SetValue(ball,179);
            Step(1);
            FailIf(!_player.IsCarryingObject || ball.State != 2 || ball.ExpirationCounter != 179,
                "$74 held ball must survive the odd update immediately before expiration.");
            Vector2 heldPosition = ball.Position;
            Step(1);
            FailIf(_player.IsCarryingObject || ball.State != 14 || ball.Counter1 != 60 || ball.Visible ||
                ball.ExpirationCounter != 0 || _entities.ReservedBraceletChildActive,
                "$74 even tick180 must drop Link's held object and enter hidden state$0e with counter60, without a thrown child.");
            var puff = _entities.Entities<PuzzlePuffEffect>().Single();
            FailIf(puff.Position != heldPosition.Floor() || puff.ZHigh != -14,
                "$74 held expiration puff must retain the high carry position and weight2 Z-14.");
            Step(59);
            FailIf(ball.State != 14 || ball.Counter1 != 1 || _player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled,
                "$74 respawn delay must stay hidden through update59 without restoring the carry pose.");
            Step(1);
            FailIf(ball.State != 15 || !ball.Visible || ball.ZFixed >> 8 != -32 || _player.IsCarryingObject,
                "$74 update60 must respawn above the room, keeping Link released.");
        }
        GD.Print("Validated live Smasher pickup, odd/even held expiration, puff carry height, 60-update respawn and persistent carry cancellation in individual/batched updates.");
    }

    private void ValidateCrownDungeonSmasherRewardHandoff()
    {
        foreach (bool batch in new[] {false,true})
        {
            _saveData.SetRoomFlag(4,0xb4,0x80,false);
            LoadValidationRoom(4,0xb4); _player.WarpTo(new(48,136));
            void Step(int count) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            bool Completed() => _saveData.HasRoomFlag(4,0xb4,0x80);
            Step(2);
            var parent = _entities.Entities<SmasherCharacter>().Single(actor => !actor.IsBall);
            parent.Health = 0; // Isolate lethal-state handoff; no fight/progression script.
            Step(119);
            FailIf(parent.Counter1 != 1 || _entities.RoomEnemyCount != 1 || Completed() || !_entities.LinkCollisionsAndMenuDisabled,
                "$74 death update119 must retain its count and keep the room incomplete with Link collision/menu locked.");
            Step(1);
            FailIf(_entities.Entities<SmasherCharacter>().Count != 0 || _entities.Entities<BossDeathExplosionEffect>().Count != 1 ||
                _entities.RoomEnemyCount != 1 || Completed(), "$74 update120 must hand its count to the explosion, not complete the room early.");
            Step(78);
            FailIf(_entities.RoomEnemyCount != 1 || Completed(), "$74 terminal explosion animation must retain the room count through update78.");
            Step(1);
            FailIf(_entities.RoomEnemyCount != 0 || !Completed() || !_entities.LinkCollisionsAndMenuDisabled ||
                _entities.Entities<MinibossPortal>().Count != 0 || _entities.Entities<ItemDropEffect>().Count != 1,
                "$74 explosion retirement must set room flag$80, create the fairy and begin the reward wait before any portal.");
            Step(19);
            FailIf(_entities.Entities<MinibossPortal>().Count != 0 || !_entities.LinkCollisionsAndMenuDisabled,
                "dungeonScript_minibossDeath must wait the complete 20 updates before spawning the portal and unlocking Link.");
            Step(1);
            FailIf(_entities.Entities<MinibossPortal>().Count != 1 || _entities.LinkCollisionsAndMenuDisabled,
                "$4:$b4 reward update20 must spawn one portal and restore Link collision/menu access.");
            Step(1);
            FailIf(!_entities.Entities<MinibossPortal>().Single().Visible, "$4:$b4 dynamically spawned portal must initialize on its next interaction update.");
            LoadValidationRoom(4,0xb4); Step(2);
            FailIf(_entities.Entities<SmasherCharacter>().Count != 0 || _entities.Entities<MinibossPortal>().Count != 1 ||
                !_entities.Entities<MinibossPortal>().Single().Visible || _entities.RoomEnemyCount != 0,
                "$4:$b4 completed re-entry must retain exactly one visible portal and no miniboss.");
        }
        GD.Print("Validated isolated factory Smasher death/explosion count transfer, room flag, 20-update reward/portal handoff and completed re-entry in individual/batched updates.");
    }

    private void ValidateCrownDungeonSmasherRoomEntry()
    {
        var records = new CrownDungeonDatabase().GetRoomRecords(4,0xb4);
        FailIf(records.Count != 2 || records[0].Order != 0 || records[0].Kind != DungeonObjectKind.MinibossReward ||
            records[1].Order != 4 || records[1].Kind != DungeonObjectKind.Smasher || records[1].Id != 0x74 ||
            records[1].SubId != 0 || records[1].Position != new Vector2(120,88) ||
            records.Any(record => record.Predicate != DungeonObjectCondition.Flag80Clear),
            "$4:$b4 main stream must retain reward order0 and BeforeEvent Smasher order4 at $58,$78, both gated by room flag$80.");
        void Step(Vector2 movement = default) =>
            StepGameplayUpdates(1, movement, [], [], batched: true);
        _saveData.SetRoomFlag(4,0xb4,0x80,false);
        // Isolate entry from key-door progression: use its already-open flag.
        _saveData.SetRoomFlag(4,0xb3,0x02,true);
        LoadValidationRoom(4,0xb3); _player.WarpTo(new(200,136));
        FailIf(_currentRoom.IsSolid(_player.Position), "$4:$b3 Smasher approach must begin on actual floor.");
        for (int i = 0; !IsTransitioning && i < 80; i++) Step(Vector2.Right);
        FailIf(!IsTransitioning, "$4:$b3 open east doorway must reach Smasher's room.");
        int frozen = 0;
        for (int i = 0; IsTransitioning && i < 160; i++)
        {
            var actors = _entities.Entities<SmasherCharacter>();
            if (actors.Count != 0)
            {
                FailIf(actors.Count != 2 || actors.Any(actor => actor.State != 8 || !actor.Visible) ||
                    actors.Single(actor => actor.IsBall).Position != new Vector2(88,88) ||
                    actors.Single(actor => !actor.IsBall).Position != new Vector2(120,88) || _entities.RoomEnemyCount != 1,
                    "$4:$b4 scroll preload must allocate both linked native slots and freeze their state8/coordinates with one enemy count.");
                frozen++;
            }
            Step();
        }
        FailIf(IsTransitioning || _currentRoom.Id != 0xb4 || frozen == 0,
            "$4:$b4 entry must complete with observable frozen destination actors.");
        Step(); Step();
        FailIf(_entities.Entities<SmasherCharacter>().Count != 2 ||
            _entities.Entities<SmasherCharacter>().Any(actor => actor.State < 9) || _entities.RoomEnemyCount != 1,
            "$74 linked handlers must resume after scrolling without double-counting the miniboss.");
        _saveData.SetRoomFlag(4,0xb4,0x80,true);
        LoadValidationRoom(4,0xb4);
        FailIf(_entities.Entities<SmasherCharacter>().Count != 0 || _entities.RoomEnemyCount != 0,
            "$4:$b4 completed-room entry must omit the BeforeEvent Smasher and reward watcher.");
        _saveData.SetRoomFlag(4,0xb4,0x80,false);
        GD.Print("Validated Crown Smasher source placement, real adjacent-room entry, linked preload/freeze, one enemy count, resumed dispatch and completed-room suppression.");
    }

    private void ValidateCrownDungeonSmasherSwitchHook()
    {
        var room = Room060MovementFixture(); var db = new EnemyDatabase();
        foreach (bool isBall in new[] {false,true})
        foreach (int z in new[] {-8,-7,6,7})
        {
            var parent = new SmasherCharacter(); var ball = new SmasherCharacter();
            var random = new OracleRandom();
            ball.InitializeLinked(db.ImportedEnemy(0x74,0),room,new(80,64),random,parent);
            parent.InitializeLinked(db.ImportedEnemy(0x74,1),room,new(112,64),random,ball);
            ball.UpdateNormalFrame(Vector2.Zero,1,_ => true,() => { },_ => { });
            int sounds = 0;
            var hook = new SwitchHookItem(new SwitchHookDatabase(),1,new(64,64),1,_ => sounds++);
            hook.UpdateItem(room,new(64,64),false,() => true);
            var actor = isBall ? ball : parent;
            actor.CopyCarriedPosition(hook.Position,z);
            var world = new SmasherRoomEnvironment(_ => null,() => { },() => { },() => true,() => true,
                () => { },() => { },_ => sounds++,1,true);
            var adapter = new SmasherRoomEntity(actor,world,!isBall);
            bool contact = adapter.ApplySwitchHookHit(hook,new(64,64));
            bool expected = z is -7 or 6;
            FailIf(contact != expected || actor.PendingCollision != (expected && !isBall) || actor.Health != 5 ||
                actor.InvincibilityCounter != 0 || actor.KnockbackCounter != 0 || hook.State != 1 || sounds != 0,
                $"$74 Switch Hook {(isBall ? "ball" : "parent")} at Z{z} lost effect00/1c, signed height edge or deferred retract.");
            if (expected && !isBall)
                FailIf(adapter.ApplySwitchHookHit(hook,Vector2.Zero), "$74 pending JUST_HIT must reject a second hook collision.");
            hook.UpdateItem(room,new(64,64),false,() => true);
            FailIf(hook.State != (expected && !isBall ? 2 : 1) || sounds != 0,
                "$74 parent must retract the hook on its next update; the ball must allow it to continue without a clink.");
            hook.Free(); ball.Free(); parent.Free();
        }
        GD.Print("Validated Smasher parent/ball Switch Hook effects, signed Z edges, pending-contact rejection and next-item-update retraction without damage or clink.");
    }

    private void ValidateCrownDungeonSmasherReservedCollision()
    {
        LoadValidationRoom(4,0xb4); _entities.Clear();
        var db = new EnemyDatabase(); var random = new OracleRandom();
        var ball = new SmasherCharacter(); var parent = new SmasherCharacter();
        var world = new SmasherRoomEnvironment(_ => parent, () => { }, () => { },
            () => true, () => true, () => { }, () => { }, _ => { }, 1, true);
        SmasherRoomEntity Add(SmasherCharacter actor, int subId) => (SmasherRoomEntity)_entities.TryAllocateEnemy(slot =>
        {
            actor.InitializePending(db.ImportedEnemy(0x74,subId), _currentRoom,new(120,88),random,slot);
            return new SmasherRoomEntity(actor,world,subId == 0);
        })!;
        var ballAdapter = Add(ball,0); Add(parent,1);
        ball.UpdateInitializationFrame(1, () => { }, () => parent);
        parent.UpdateInitializationFrame(1, () => { }, () => null);
        ball.UpdateNormalFrame(Vector2.Zero,1,_ => true,() => { },_ => { });
        _player.WarpTo(new(100,88)); _player.Face(Vector2I.Left);
        // Isolate the collision pass. Live approach/lift timing is covered by
        // ValidateCrownDungeonSmasherBraceletLoop, not repeated here.
        FailIf(!ballAdapter.TryUseBracelet(_player,Vector2I.Zero), "$74 collision fixture could not grab its ball.");
        ballAdapter.UpdateFrame(new(_player,1,false),[]);
        ballAdapter.UpdateHeldPosition(_player);
        FailIf(ballAdapter.TryGetReservedBraceletCollision(out _), "$74 held ball must not create physical item collisions.");
        FailIf(!ballAdapter.TryUseBracelet(_player,Vector2I.Left), "$74 collision fixture could not release its ball.");
        ballAdapter.UpdateBraceletChild(_player);
        FailIf(!ballAdapter.TryGetReservedBraceletCollision(out var item) || item.Bounds.Size != new Vector2(12,12) ||
            item.Radius != 7 || item.Damage != 3 || item.Bounds.GetCenter() != ballAdapter.ReservedThrow!.Position.Floor(),
            "$74 physical Bracelet must use copied enemy radii, its own high XY/Z, radius7 and damage3.");
        foreach (int dz in new[] {-8,-7,6,7})
        {
            parent.UpdateNormalFrame(Vector2.Zero,1,_ => true,() => { },_ => { });
            parent.CopyCarriedPosition(item.Bounds.GetCenter(),item.Z + dz);
            _entities.ResolvePostObjectCollisions(_player);
            FailIf(parent.PendingCollision != (dz is -7 or 6) || parent.Health != 5 || parent.InvincibilityCounter != 0 ||
                parent.KnockbackCounter != 0 || ball.PendingCollision,
                $"$74 reserved item collision at Z delta{dz} must publish only parent effect$1c, with ball effect$00 and no boss HP damage.");
        }
        parent.UpdateNormalFrame(Vector2.Zero,1,_ => true,() => { },_ => { });
        parent.CopyCarriedPosition(item.Bounds.GetCenter(),item.Z);
        ball.FinishGrabBounce(); ballAdapter.UpdateBraceletChild(_player);
        _entities.ResolvePostObjectCollisions(_player);
        FailIf(ballAdapter.TryGetReservedBraceletCollision(out _) || parent.PendingCollision,
            "$74 retired reserved item must leave no queued collision on the following post-object pass.");
        _entities.Clear();
        GD.Print("Validated Smasher reserved Bracelet collision geometry, signed Z edges, parent effect$1c/ball effect$00 and deletion through the shared post-object pass.");
    }

    private void ValidateCrownDungeonSmasherBraceletLoop()
    {
        foreach (bool batch in new[] { false, true })
        foreach (bool directionless in new[] { false, true })
        {
            LoadValidationRoom(4,0xb4); _entities.Clear();
            _player.WarpTo(new(120,88));
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet,1);
            _inventory.EquipA(InventoryState.ItemBracelet);
            var db = new EnemyDatabase(); var random = new OracleRandom();
            var ball = new SmasherCharacter(); var parent = new SmasherCharacter();
            parent.InitializePending(db.ImportedEnemy(0x74,1), _currentRoom, new(120,88), random, 1);
            var world = new SmasherRoomEnvironment(_ => parent, () => { }, () => { },
                () => _entities.InteractionSlotAvailable, () => _entities.PartSlotAvailable,
                () => { }, () => { }, _ => { }, 1, true);
            var adapter = (SmasherRoomEntity)_entities.TryAllocateEnemy(slot =>
            {
                ball.InitializePending(db.ImportedEnemy(0x74,0), _currentRoom, new(120,88), random, slot);
                return new SmasherRoomEntity(ball, world, true);
            })!;
            void Step(int count, Vector2 movement = default, bool press = false) =>
                StepGameplayUpdates(count, movement, press ? ["attack"] : [], press ? ["attack"] : [], batched: batch);
            Step(2); Step(40, Vector2.Left);
            FailIf(_player.Position != new Vector2(100,88) || ball.State != 9,
                "$4:$b4 bracelet fixture must approach the ground ball through actual room geometry.");
            _entities.ClearSignalsAfterPlayer();
            FailIf(_entities.TryUseBracelet(_player,Vector2I.Zero),
                "$74 pickup must require an object-phase publication, not scan eligible actors directly.");
            Step(1);
            Step(1, press:true);
            FailIf(!_player.IsCarryingObject || ball.State != 2 || ball.GrabSubstate != 1 ||
                ball.Position != new Vector2(92,88) || ball.ZFixed >> 8 != 0 || !_player.BraceletLiftCollisionsDisabled,
                $"$74 pickup must consume the previous object buffer, initialize weight$20 and copy the low lift offset after objects (ball={ball.Position}, z={ball.ZFixed}, state={ball.State}:{ball.GrabSubstate}).");
            Step(7);
            FailIf(ball.Position != new Vector2(92,88) || ball.ZFixed >> 8 != 0,
                "$74 first seven lift updates must retain the low X-8/Z0 offset.");
            Step(1);
            FailIf(ball.Position != new Vector2(96,88) || ball.ZFixed >> 8 != -8,
                "$74 update8 must copy the middle lift's X-4/Z-8 high bytes.");
            Step(5);
            FailIf(!_player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled || ball.Position != new Vector2(100,88) ||
                ball.ZFixed >> 8 != -14, "$74 update13 must finish lifting and carry weight2 at Z-14 facing left.");
            Step(1, directionless ? Vector2.Zero : Vector2.Left, press:true);
            FailIf(_player.IsCarryingObject || !adapter.ReservedBraceletChildActive || ball.GrabSubstate != 2 ||
                adapter.ReservedThrow!.Speed != (directionless ? 0 : 0x41) || ball.Position != new Vector2(directionless ? 99 : 97,88),
                "$74 release must create reserved item C, offset X-1, and update its drop/throw motion before the native enemy.");
            FailIf(!adapter.TryGetReservedBraceletCollision(out var releasedCollision) ||
                releasedCollision.Bounds.GetCenter() != ball.Position || ball.PendingCollision,
                "$74 live release must expose physical item geometry while its own effect$00 leaves the ball's JUST_HIT clear.");
            Step(8);
            FailIf(!adapter.ReservedBraceletChildActive || _bracelet.TryUse(_player,true),
                "$74 reserved throw must reject a new bracelet parent after the throw animation ends.");
            int landingUpdates = 0;
            while (adapter.ReservedBraceletChildActive && landingUpdates++ < 80) Step(1);
            FailIf(adapter.ReservedBraceletChildActive || ball.State != 8 || ball.GrabSubstate != 3,
                "$74 final reserved-item bounce must return the enemy to state8 in the same update.");
            Step(16,Vector2.Right); Step(96,Vector2.Left);
            FailIf(ball.State != 9 || _player.Position.X != ball.Position.X + 12,
                "$74 landed ball must publish itself again and remain reachable through room geometry.");
            Step(1,press:true); Step(7);
            FailIf(!_player.IsCarryingObject || ball.State != 2, "$74 landed ball must support a second pickup.");
            _bracelet.Interrupt(_player,discard:false);
            Step(1);
            FailIf(_player.IsCarryingObject || ball.State != 8 || ball.GrabSubstate != 3 || adapter.ReservedBraceletChildActive,
                "$74 interrupted lift must drop the native object without creating a thrown child.");
            Step(2);
            FailIf(ball.State != 9 || _player.BraceletLiftCollisionsDisabled,
                "$74 cancelled pickup must return to ordinary ground dispatch without restoring Link's carry pose.");
            _entities.Clear(); parent.QueueFree();
        }
        GD.Print("Validated isolated Smasher approach, pickup, 7/13-update lift, reserved throw/drop, occupancy, landing, repeat pickup and cancellation in single/batched gameplay updates.");
    }

    private void ValidateCrownDungeonSmasherLinkResponses()
    {
        foreach (int enemyInvincibility in new[] { 0, -3, 3 })
        for (int shield = 0; shield <= 3; shield++)
        {
            var inventory = new InventoryState(_treasures, OracleSaveData.CreateStandardGame());
            if (shield != 0)
            {
                inventory.GiveTreasure(_treasures.GetObject($"TREASURE_OBJECT_SHIELD_0{shield-1}"));
                inventory.EquipA(InventoryState.ItemShield);
            }
            var player = new Player(); AddChild(player);
            player.Initialize(new ValidationRingPlayerWorld(), inventory, new(72,72), new OracleRandom());
            player.Face(Vector2I.Right);
            if (shield != 0) player.UpdateShieldForValidation(true,false);
            Vector2 position = shield == 0 ? new(80,72) : new(85,player.ShieldCollisionBounds.GetCenter().Y);
            var parent = new SmasherCharacter(); var ball = new SmasherCharacter();
            var db = new EnemyDatabase(); var room = Room060MovementFixture(); var random = new OracleRandom();
            ball.InitializeLinked(db.ImportedEnemy(0x74,0),room,new(32,32),random,parent);
            parent.InitializeLinked(db.ImportedEnemy(0x74,1),room,position,random,ball);
            int sounds = 0, health = player.HealthQuarters;
            var world = new SmasherRoomEnvironment(_ => null, () => { }, () => { }, () => true, () => true,
                () => { }, () => { }, sound => { FailIf(sound != OracleSoundEngine.SndBombLand,"$74 shield used the wrong sound."); sounds++; },1,true);
            var adapter = new SmasherRoomEntity(parent,world,false);
            parent.InvincibilityCounter = enemyInvincibility;
            if (shield != 0) FailIf(Player.EnemyCollisionOverlaps(player.Position,parent.CollisionBounds),"Shield fixture must exclude Link's body.");
            adapter.HandleLinkContact(player);
            int expectedInvincibility = shield == 0 ? 34 : shield == 1 ? 0 : -8;
            int expectedKnockback = shield == 0 ? 15 : shield == 1 ? 0 : 11;
            FailIf(player.HealthQuarters != health-(shield == 0 ? 2 : 0) ||
                player.InvincibilityFrames != expectedInvincibility || player.KnockbackFrames != expectedKnockback ||
                parent.PendingCollision != (shield != 1) || sounds != (shield >= 2 ? 1 : 0) ||
                parent.Health != 5 || parent.InvincibilityCounter != enemyInvincibility || parent.KnockbackCounter != 0,
                $"$74 Link response for shield level{shield} lost effect02/05 damage, signed invincibility, recoil, sound or enemy JUST_HIT.");
            adapter.HandleLinkContact(player);
            FailIf(player.HealthQuarters != health-(shield == 0 ? 2 : 0) || sounds != (shield >= 2 ? 1 : 0),
                "$74 repeated collision in the same update must not apply twice.");
            player.Free(); parent.Free(); ball.Free();
        }
        GD.Print("Validated isolated Smasher body damage and shield1 rejection/shield2-3 recoil, signed counters, sound and enemy contact without enemy damage.");
    }

    private void ValidateCrownDungeonSmasherGroundPush()
    {
        foreach (bool batch in new[] { false, true })
        {
            LoadValidationRoom(4,0xb4); _entities.Clear();
            _player.WarpTo(new(120,88));
            int health = _player.HealthQuarters;
            var db = new EnemyDatabase(); var random = new OracleRandom();
            var ball = new SmasherCharacter(); var parent = new SmasherCharacter();
            // Isolate the ground ball; its linked parent does not run AI and
            // therefore cannot pick it up during this contact check.
            parent.InitializePending(db.ImportedEnemy(0x74,1), _currentRoom, new(120,88), random, 1);
            var world = new SmasherRoomEnvironment(_ => parent, () => { }, () => { },
                () => _entities.InteractionSlotAvailable, () => _entities.PartSlotAvailable,
                () => { }, () => { }, _ => { }, 1, true);
            _entities.TryAllocateEnemy(slot =>
            {
                ball.InitializePending(db.ImportedEnemy(0x74,0), _currentRoom, new(120,88), random, slot);
                return new SmasherRoomEntity(ball, world, true);
            });
            void Step(int count, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            Step(2);
            Step(40, Vector2.Left);
            FailIf(_player.Position != new Vector2(100,88) || ball.Position != new Vector2(88,88) || ball.PendingCollision,
                $"$4:$b4 ground ball must push the approaching Link to the excluded +12 XY edge before damage (batch={batch}, Link={_player.Position}).");
            Step(10);
            FailIf(_player.Position != new Vector2(100,88), "$74 ground push must not move Link outside its positive collision edge.");
            Step(16, Vector2.Right);
            Step(32, Vector2.Left);
            FailIf(_player.Position != new Vector2(100,88) || ball.PendingCollision,
                "$74 repeated approach through actual room geometry must reach the same push boundary without contact damage.");
            ball.CopyCarriedPosition(ball.Position, -32);
            Step(4, Vector2.Left);
            FailIf(_player.Position != new Vector2(100,88), "$74 state9 native push is XY-only and must not acquire a height gate.");
            Step(20, Vector2.Down); Step(40, Vector2.Left); Step(20, Vector2.Up);
            Step(40, Vector2.Right);
            FailIf(_player.Position != new Vector2(75,88) || _player.HealthQuarters != health,
                $"$74 negative summed-radius edge is included, so the west approach must stop at X75 without damage (Link={_player.Position}).");
            _entities.Clear(); parent.QueueFree();
        }
        GD.Print("Validated ground Smasher ball approach and repeat approach through room4:b4 geometry, pre-collision one-pixel push and XY-only gating in single/batched application updates.");
    }

    private void ValidateCrownDungeonSmasherWeaponResponses()
    {
        var room = Room060MovementFixture(); var db = new EnemyDatabase(); var random = new OracleRandom();
        var ball = new SmasherCharacter(); var parent = new SmasherCharacter();
        ball.InitializeLinked(db.ImportedEnemy(0x74,0), room, new(80,64), random, parent);
        parent.InitializeLinked(db.ImportedEnemy(0x74,1), room, new(112,64), random, ball);
        var world = new SmasherRoomEnvironment(_ => null, () => { }, () => { }, () => true, () => true,
            () => { }, () => { }, _ => { }, 1, true);
        var ballAdapter = new SmasherRoomEntity(ball, world, true);
        var parentAdapter = new SmasherRoomEntity(parent, world, false);
        var spawns = new System.Collections.Generic.List<RoomEntitySpawn>();
        void Step(SmasherCharacter actor) => actor.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { });
        FailIf(!ballAdapter.ApplySwordHit(ball.CollisionBounds, Vector2.Zero, 99, EnemyKnockbackStrength.High, spawns) ||
            !ballAdapter.MeleeReportsContact || !ball.PendingCollision || ball.Health != 5,
            "$74 ball must retain parent mode$45 until state8 actually dispatches.");
        Step(ball);
        FailIf(ball.CollisionMode != 0x63 || ball.PendingCollision, "$74 ball state8 must select mode$63 and consume JUST_HIT.");
        foreach (var (state, level) in new[] { (SwordActionState.Held,1), (SwordActionState.Spin,1), (SwordActionState.Spin,2) })
        {
            bool active = state != SwordActionState.Spin; // sword.s:@state4 writes $08 at every level; its mask is clear.
            foreach (var adapter in new[] { parentAdapter, ballAdapter })
            {
                var actor = adapter.Character;
                adapter.SetLinkSwordState(state, level);
                bool contact = adapter.ApplySwordHit(actor.CollisionBounds, Vector2.Zero, 99, EnemyKnockbackStrength.High, spawns);
                FailIf(contact != active || adapter.MeleeReportsContact != (active && !actor.IsBall) ||
                    actor.PendingCollision != (active && !actor.IsBall) || actor.Health != 5 ||
                    actor.InvincibilityCounter != 0 || actor.KnockbackCounter != 0 || spawns.Count != 0,
                    $"$74 sword {state}/{level}, ball={actor.IsBall}: native no-damage/contact semantics changed.");
                Step(actor);
            }
        }
        foreach (var adapter in new[] { parentAdapter, ballAdapter })
        {
            var actor = adapter.Character;
            FailIf(adapter.ApplyExpertPunch(actor.CollisionBounds, Vector2.Zero, 99, spawns) ||
                adapter.ApplyItemCollision(RoomEntityItemCollision.Bomb, actor.CollisionBounds, Vector2.Zero, 99, spawns),
                "$74 active mask must reject expert punch$0b and bomb$18.");
            FailIf(!adapter.ApplyItemCollision(RoomEntityItemCollision.ThrownObject, actor.CollisionBounds, Vector2.Zero, 99, spawns) ||
                actor.PendingCollision != !actor.IsBall || actor.Health != 5,
                "$74 generic thrown object must use effect$1c for parent and effect00 for ball.");
            Step(actor);
            foreach (int item in new[] { 0x20, 0x21, 0x22, 0x23 })
            {
                FailIf(!new SeedSatchelDatabase().TryGet(item, out var seed), "Seed fixture missing native item.");
                var response = adapter.ApplySeedCollision(actor.CollisionBounds, Vector2.Zero, seed, seed.Collision & 0x7f, spawns);
                FailIf(!response.Contact || response.DisableCollision == actor.IsBall ||
                    response.Effect != (actor.IsBall ? SeedHitResult.None : SeedHitResult.Activate) ||
                    actor.PendingCollision || actor.Health != 5,
                    $"$74 seed ${item:x2} must be absorbed by parent and pass its no-op ball row without enemy status.");
            }
        }
        // enemyStandardUpdate checks var2a bit7 before health. A contact signal
        // therefore delays NO_HEALTH even though Smasher applies no weapon damage.
        parent.Health = 0;
        parent.PublishCollision();
        int deaths = 0;
        parent.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { }, handleDeath: () => deaths++);
        FailIf(deaths != 0 || parent.PendingCollision, "$74 JUST_HIT must precede NO_HEALTH for one dispatch.");
        parent.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { }, handleDeath: () => deaths++);
        FailIf(deaths != 1, "$74 health-zero death must dispatch once the contact signal is consumed.");
        ball.QueueFree(); parent.QueueFree();
        GD.Print("Validated isolated Smasher weapon masks, mode$45-to$63 handoff, harmless contact versus no-op overlap, seed absorption, and JUST_HIT/death priority.");
    }

    private void ValidateCrownDungeonSmasherRoomLifecycle()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        {
            LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(72,40));
            var active = (System.Collections.Generic.List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities", flags)!.GetValue(_entities)!;
            var room = Room060MovementFixture(); var random = new OracleRandom(); var db = new EnemyDatabase();
            SmasherRoomEnvironment world = null!;
            int initialized = 0, began = 0, disabled = 0, restored = 0, deathSounds = 0;
            SmasherRoomEntity Create(int slot, bool placed)
            {
                var actor = new SmasherCharacter();
                actor.InitializePending(db.ImportedEnemy(0x74, placed ? 0 : 1), room, new(120,88), random, slot);
                return new(actor, world, placed);
            }
            world = new(
                _ => ((SmasherRoomEntity?)_entities.TryAllocateEnemy(slot => Create(slot, false)))?.Character,
                () => initialized++, () => began++, () => _entities.InteractionSlotAvailable, () => _entities.PartSlotAvailable,
                () => disabled++, () => restored++, sound => { if (sound == OracleSoundEngine.SndBossDead) deathSounds++; },
                KillableEnemyIndex: 1, Counted: true);
            _entities.TryAllocateEnemy(slot => Create(slot, true));
            void Step(int count) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            FailIf(_entities.RoomEnemyCount != 1, "$74 uninitialized placement must retain one room enemy.");
            Step(2);
            var pair = active.OfType<SmasherRoomEntity>().ToArray();
            var ball = pair.Single(e => e.Character.IsBall).Character;
            var parent = pair.Single(e => !e.Character.IsBall).Character;
            FailIf(pair.Length != 2 || _entities.RoomEnemyCount != 1 || initialized != 1 || began != 1,
                "$74 linked initialization must transfer its single count to the parent and begin once.");
            ball.CopyCarriedPosition(ball.Position, -7);
            parent.CopyCarriedPosition(parent.Position, -12);
            parent.Health = 0;
            Step(1);
            FailIf(_entities.RoomEnemyCount != 1 || parent.Counter1 != 119 || ball.IsDead || ball.Health != 0 || disabled != 1 || deathSounds != 1,
                "$74 parent death must retain the count and defer deletion of the earlier ball slot.");
            Step(1);
            var puff = active.Select(e => e.Node).OfType<PuzzlePuffEffect>().Single();
            FailIf(active.OfType<SmasherRoomEntity>().Count() != 1 || puff.ZHigh != -7 || puff.Position != new Vector2(88,88) ||
                _entities.RoomEnemyCount != 1, "$74 ball death puff must keep world XY/Z separately and remain uncounted.");
            Step(117);
            FailIf(parent.Counter1 != 1 || restored != 0 || _entities.RoomEnemyCount != 1,
                "$74 room adapter must retain the parent until death update120.");
            Step(1);
            var explosion = active.Select(e => e.Node).OfType<BossDeathExplosionEffect>().Single();
            FailIf(active.OfType<SmasherRoomEntity>().Any() || _entities.RoomEnemyCount != 1 || restored != 1 ||
                explosion.ZHigh != -12 || explosion.Position != parent.Position.Floor(),
                "$74 successful death must transfer exactly one count and high XYZ to the native explosion.");
            var partSlots = (System.Collections.Generic.Dictionary<IRoomEntity,int>)typeof(RoomEntityManager).GetField("_partSlots",flags)!.GetValue(_entities)!;
            int explosionSlot = partSlots[active.OfType<BossDeathExplosionRoomEntity>().Single()];
            Step(explosion.AnimationDuration);
            FailIf(_entities.RoomEnemyCount != 1 || active.OfType<BossDeathExplosionRoomEntity>().Count() != 1,
                "$74 explosion must retain its slot and room count through the terminal animation frame.");
            Step(1);
            FailIf(_entities.RoomEnemyCount != 0 || active.OfType<BossDeathExplosionRoomEntity>().Any(),
                "$74 explosion completion must release the final room count.");
            var dropAdapter = active.OfType<ItemDropRoomEntity>().Single();
            var drop = (ItemDropEffect)dropAdapter.Node;
            // treasureAndDrops.s row $74=$ef: probability7 always succeeds;
            // setF contains only ITEM_DROP_FAIRY ($00).
            FailIf(partSlots[dropAdapter] != explosionSlot || drop.SubId != 0 || drop.ElapsedFrames != 0 ||
                drop.ZFixed != -0x0c00 || drop.Position != explosion.Position.Floor(),
                "$74 fairy replacement must reuse the explosion's slot and high XYZ without dispatching twice in one pass.");
            Step(1);
            FailIf(drop.ElapsedFrames != 1 || drop.ZFixed != -0x0c00 || drop.SpeedZ != -0x160,
                "$74 fairy state0 must retain inherited Z while initializing upward speed.");
            Step(1);
            FailIf(drop.ZFixed != -0x560 || drop.SpeedZ != -0x140 || drop.FairyCollisionDelayCounter != 5,
                "$74 inherited-height fairy must preserve fractional Z on the source $fa height clamp after its first motion update.");
        }
        _entities.Clear();
        GD.Print("Validated Smasher lifecycle in the application loop: linked count ownership, deferred ball death, puff/explosion height, same-slot fairy replacement and inherited-height motion.");
    }

    private void ValidateCrownDungeonSmasherLiveAllocation()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        foreach (bool batch in new[] { false, true })
        foreach (int freedSlot in new[] { 0, 15 })
        {
            LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(72,40));
            var room = Room060MovementFixture(); var random = new OracleRandom(); var db = new EnemyDatabase();
            var slots = new SmasherSlotValidationEntity[16];
            var visits = new System.Collections.Generic.List<int>();
            int roomInitializations = 0;
            SmasherCharacter? child = null;
            for (int index = 0; index < 16; index++)
            {
                int expectedSlot = index;
                slots[index] = (SmasherSlotValidationEntity)_entities.TryAllocateEnemy(slot =>
                {
                    FailIf(slot != expectedSlot, "Native ENEMY allocation must scan slots from low to high.");
                    var actor = new SmasherCharacter();
                    actor.InitializePending(db.ImportedEnemy(0x74,0), room, new(120,88), random, slot);
                    return new SmasherSlotValidationEntity(actor, (self, frame) =>
                    {
                        if (self.NativeSlot != 2) return; // Other slots only occupy the pool.
                        visits.Add(2);
                        if (self.State != 0) return;
                        self.UpdateInitializationFrame(frame.Counter, () => roomInitializations++, () =>
                        {
                            var allocated = _entities.TryAllocateEnemy(childSlot =>
                            {
                                child = new SmasherCharacter();
                                child.InitializePending(db.ImportedEnemy(0x74,1), room, Vector2.Zero, random, childSlot);
                                return new SmasherSlotValidationEntity(child, (linked, linkedFrame) =>
                                {
                                    visits.Add(linked.NativeSlot);
                                    if (linked.State == 0) linked.UpdateInitializationFrame(linkedFrame.Counter,
                                        () => throw new InvalidOperationException("Linked slot must not initialize the boss room."),
                                        () => throw new InvalidOperationException("Linked slot must not allocate another enemy."));
                                });
                            });
                            return allocated?.Node as SmasherCharacter;
                        });
                    });
                })!;
            }
            FailIf(_entities.TryAllocateEnemy(_ => throw new InvalidOperationException("Full pool must not call the factory.")) is not null,
                "A full native enemy pool must fail allocation without constructing an actor.");
            slots[freedSlot].Finished = true;
            input.CaptureForValidation([], [], Vector2.Zero);
            if (batch) scheduler.Advance(2.0 / 60.0, update);
            else { scheduler.Advance(1.0 / 60.0, update); scheduler.Advance(1.0 / 60.0, update); }
            int[] expectedVisits = freedSlot == 0 ? [2,0,2] : [2,2,15];
            FailIf(!visits.SequenceEqual(expectedVisits) || child is null || child.State != 8 ||
                child.NativeSubId != (freedSlot == 0 ? 0 : 1) ||
                roomInitializations != (freedSlot == 0 ? 1 : 2) || random.Calls != (freedSlot == 0 ? 2 : 3),
                $"Smasher allocation lost live-slot order/retry RNG (freed=${freedSlot:x2}, batch={batch}, visits={string.Join(',', visits)}).");
        }
        _entities.Clear();
        GD.Print("Validated Smasher linked allocation through single/batched application updates: full pool, later-slot release, same-pass higher child and next-pass lower child.");
    }

    private void ValidateCrownDungeonSmasherInitialization()
    {
        foreach (int allocatedSlot in new[] { 2, 9 })
        {
            var room = Room060MovementFixture(); var random = new OracleRandom(); var db = new EnemyDatabase();
            var placed = new SmasherCharacter(); var child = new SmasherCharacter();
            placed.InitializePending(db.ImportedEnemy(0x74,0), room, new(120.5f,88.25f), random, 5);
            FailIf(placed.Visible || placed.CollisionEnabled || random.Calls != 0, "$74 pending slot must not load properties before dispatch.");
            int roomInitializations = 0, allocationAttempts = 0;
            void InitializeRoom() => roomInitializations++;
            for (int frame = 1; frame <= 3; frame++)
            {
                placed.Health = 0;
                placed.UpdateInitializationFrame(frame, InitializeRoom, () => { allocationAttempts++; return null; });
                FailIf(placed.State != 0 || placed.Visible || !placed.CollisionEnabled || placed.Health != 5 ||
                    random.Calls != frame || roomInitializations != frame || allocationAttempts != frame,
                    "$74 allocation retry must reload health/collision, remain hidden, consume RNG and repeat room initialization.");
            }
            placed.UpdateInitializationFrame(4, InitializeRoom, () =>
            {
                allocationAttempts++;
                child.InitializePending(db.ImportedEnemy(0x74,1), room, new(0.75f,0.5f), random, allocatedSlot);
                return child;
            });
            bool swapped = allocatedSlot < 5;
            FailIf(placed.State != 8 || child.State != 0 || child.Visible || random.Calls != 4 ||
                placed.IsBall == swapped || child.NativeSubId != (swapped ? 0x80 : 1) ||
                child.Position != new Vector2(120.75f,88.5f) || placed.ExpirationCounter != 2,
                "$74 allocation must copy coordinate high bytes and swap roles only for a lower child slot.");
            child.UpdateInitializationFrame(swapped ? 5 : 4, () => throw new InvalidOperationException("Linked child cannot initialize the boss room again."),
                () => throw new InvalidOperationException("Linked child cannot allocate another parent."));
            var ball = swapped ? child : placed;
            var parent = swapped ? placed : child;
            FailIf(ball.NativeSubId != 0 || parent.NativeSubId != 1 || ball.State != 8 || parent.State != 8 ||
                ball.Position.X != (swapped ? 88.75f : 88.5f) || parent.Position.X != (swapped ? 120.5f : 120.75f) ||
                ball.Speed != 0x0f || parent.Speed != 0 || ball.Palette != 1 || parent.Palette != 3 ||
                random.Calls != 5 || roomInitializations != 4 || allocationAttempts != 4 ||
                ball.ExpirationCounter != (swapped ? 0 : 2),
                "$74 normalized linked slots lost source order, palette, register-derived speed, RNG or slot-local expiration counter.");
            placed.QueueFree(); child.QueueFree();
        }
        GD.Print("Validated isolated Smasher state-zero retries, property/RNG reload, high-byte copies and lower/higher slot role normalization; live allocation adapter pending.");
    }

    private void ValidateCrownDungeonSmasherDeath()
    {
        var room = Room060MovementFixture(); var random = new OracleRandom();
        var database = new EnemyDatabase();
        var ball = new SmasherCharacter(); var parent = new SmasherCharacter();
        ball.InitializeLinked(database.ImportedEnemy(0x74,0), room, new(80,64), random, parent);
        parent.InitializeLinked(database.ImportedEnemy(0x74,1), room, new(112,64), random, ball);
        ball.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { });
        ball.BeginGrab();
        parent.Health = 0;
        int locks = 0, sounds = 0, attempts = 0, marked = 0, restored = 0, drops = 0, puffs = 0;
        bool available = false;
        void ParentDeath() => parent.UpdateDeathFrame(_ => throw new InvalidOperationException("Parent must use explosion."),
            position => { attempts++; FailIf(position != parent.Position, "$74 explosion lost parent XY."); return available; },
            () => locks++, () => marked++, () => { FailIf(marked != 1, "$74 music must follow room-kill marking."); restored++; },
            sound => { FailIf(sound != OracleSoundEngine.SndBossDead, "$74 wrong death sound."); sounds++; },
            () => throw new InvalidOperationException("Parent cannot directly drop Link."));
        void ParentStep(int frame) => parent.UpdateNormalFrame(Vector2.Zero, frame, _ => true, () => { }, _ => { }, handleDeath: ParentDeath);
        ParentStep(1);
        FailIf(parent.Counter1 != 119 || parent.Visible || parent.CollisionEnabled || ball.Health != 0 || ball.IsDead ||
            ball.CollisionEnabled || drops != 0 || locks != 1 || sounds != 1 || attempts != 0,
            "$74 first death update must kill linked ball lazily, lock Link and decrement/flicker immediately.");
        ball.UpdateNormalFrame(Vector2.Zero, 2, _ => true, () => { }, _ => { }, handleDeath: () =>
            ball.UpdateDeathFrame(_ => { puffs++; return false; }, _ => throw new InvalidOperationException("Ball cannot explode."),
                () => { }, () => { }, () => { }, _ => { }, () => drops++));
        FailIf(!ball.IsDead || drops != 1 || puffs != 1, "$74 held ball must drop and delete even when puff allocation fails.");
        for (int frame = 2; frame <= 119; frame++) ParentStep(frame);
        FailIf(parent.Counter1 != 1 || parent.Visible || attempts != 0 || marked != 0,
            "$74 death must perform 119 flickers before its first explosion attempt.");
        ParentStep(120); ParentStep(121);
        FailIf(parent.IsDead || parent.Counter1 != 1 || parent.Visible || attempts != 2 || marked != 0 || restored != 0 || locks != 1 || sounds != 1,
            "$74 exhausted PART slots must retry without repeated sound, lock, flicker, marking or music restore.");
        available = true; ParentStep(122); ParentStep(123);
        FailIf(!parent.IsDead || attempts != 3 || marked != 1 || restored != 1,
            "$74 successful explosion allocation must mark and restore music exactly once before deletion.");
        ball.QueueFree(); parent.QueueFree();
        GD.Print("Validated isolated Smasher linked-ball death, held drop, failed puff, 120-update flicker and explosion allocation retry.");
    }

    private void ValidateCrownDungeonBraceletLiftCancellation()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        foreach (int cancelAfter in new[] { 1, 7, 12 })
        {
            LoadValidationRoom(0,0x60);
            // Isolate the item-parent lifecycle from the room's enemies.
            _entities.Clear();
            _player.WarpTo(new(72,40));
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet,1);
            _inventory.EquipA(InventoryState.ItemBracelet);
            void Step(int count) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            void BeginLift()
            {
                // Fixture starts at the entity-to-bracelet handoff. Pickup
                // reachability is separate from cancellation of this parent.
                _player.BeginCarriedObjectPose();
                typeof(BraceletController).GetMethod("BeginEntityLift", flags)!.Invoke(_bracelet, [_player, true]);
            }
            BeginLift(); Step(cancelAfter);
            FailIf(!_player.IsCarryingObject || !_player.BraceletLiftCollisionsDisabled,
                $"Bracelet cancellation fixture must still be lifting on update{cancelAfter}.");
            _player.EndCarriedObjectPose(); // Native object expiration clears grab ownership.
            Step(1);
            FailIf(_player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled || _player.BraceletEntityOffset is not null,
                "Cleared entity grab state must release the bracelet parent on its next application update.");
            Step(16);
            FailIf(_player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled,
                "Cancelled entity lift must not recreate a carrying pose at the old animation boundary.");
            BeginLift(); Step(13);
            FailIf(!_player.IsCarryingObject || _player.BraceletLiftCollisionsDisabled || _player.BraceletEntityOffset is not null,
                $"A fresh entity lift after cancellation must finish its normal 13-update animation (batch={batch}, cancelAfter={cancelAfter}).");
            _player.EndCarriedObjectPose();
        }
        GD.Print("Validated entity-lift cancellation at all three bracelet animation stages and repeat completion through single/batched application updates.");
    }

    private void ValidateCrownDungeonSmasherThrowMotion()
    {
        foreach (bool toss in new[] { false, true })
        {
            var room = Room060MovementFixture(); var database = new EnemyDatabase(); var random = new OracleRandom();
            var ball = new SmasherCharacter(); var parent = new SmasherCharacter();
            ball.InitializeLinked(database.ImportedEnemy(0x74,0), room, new(80.5f,64.25f), random, parent);
            var memory = new OracleRuntimeState();
            ball.BindMovementMemory(memory);
            parent.InitializeLinked(database.ImportedEnemy(0x74,1), room, new(140,32), random, ball);
            ball.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { }); ball.BeginGrab();
            var motion = new SmasherBraceletThrow(ball, room, new BraceletWeightDatabase(), new BombDatabase().Data);
            motion.Hold(new(80.75f,64.5f), -4, 2, 1);
            FailIf(ball.Position != new Vector2(80.5f,64.25f) || ball.ZFixed != -18*256 || motion.Active,
                "$74 held copy must include LinkZ and retain enemy fractions without creating the throw item.");
            motion.Begin(1,8,toss);
            FailIf(motion.Position != new Vector2(81,64) || motion.ZFixed != -18*256 ||
                motion.SpeedZ != -0xe0 || motion.Speed != (toss ? 0x64 : 0x41) || ball.GrabSubstate != 2,
                "$74 new reserved item must zero fractions, apply the facing offset and select weight2/Toss launch values.");
            int landings = 0;
            motion.Update(_ => landings++);
            float firstX = toss ? 83.5f : 82.625f;
            FailIf(memory.ReadWramByte(0xcec0) != 0 || memory.ReadWramByte(0xcec1) != 0 ||
                memory.ReadWramByte(0xcec2) != (toss ? 0x80 : 0xa0) ||
                memory.ReadWramByte(0xcec3) != (toss ? 2 : 1),
                "The reserved Smasher throw must publish its actual weight2/Toss X velocity before copying position to the enemy.");
            FailIf(motion.Position != new Vector2(firstX,64) || motion.ZFixed != -18*256-0xe0 || motion.SpeedZ != -0xb8 ||
                ball.Position != new Vector2(Mathf.Floor(firstX)+0.5f,64.25f) || ball.ZFixed != -19*256,
                "$74 first reserved-item update must apply weight2 motion before copying high bytes to its enemy.");
            motion.SetAngle(24); motion.Update(_ => landings++);
            FailIf(motion.Position != new Vector2(81,64) || ball.Angle != 8,
                "$74 rebound must change item direction without mirroring it into the enemy's angle.");
            room.SetPositionTileAndCollision(new(80,64), 0xff, 15, 0);
            motion.SetAngle(8); motion.Update(_ => landings++);
            FailIf(motion.Angle != 0xff || motion.Speed != (toss ? 0x64 : 0x41) || motion.Position != new Vector2(81,64),
                "Native item wall collision must clear angle but retain speed and position.");
            FailIf(Enumerable.Range(0, 4).Any(offset => memory.ReadWramByte(0xcec0 + offset) != 0),
                "The first blocked throw must fall through objectApplySpeed with angle $ff and clear scratch.");
            for (int offset = 0; offset < 4; offset++) memory.SetWramByte(0xcec0 + offset, 0xa5);
            motion.Update(_ => landings++);
            FailIf(Enumerable.Range(0, 4).Any(offset => memory.ReadWramByte(0xcec0 + offset) != 0xa5),
                "A later angle-$ff throw must return before movement and preserve scratch.");
            motion.SetAngle(24); motion.Update(_ => landings++);
            FailIf(motion.Position.X != (toss ? 78.5f : 79.375f),
                "Native item rebound must resume movement using the retained speed.");
            for (int i = 0; i < 128 && motion.Active; i++)
            {
                Vector2 previous = ball.Position; int previousZ = ball.ZFixed;
                motion.Update(_ => landings++);
                if (!motion.Active) FailIf(ball.Position != previous || ball.ZFixed != previousZ,
                    "ITEM_BRACELET final bounce releases before copying position to relatedObj2.");
            }
            FailIf(motion.Active || ball.GrabSubstate != 3 || landings < 2,
                "$74 weight2 throw must bounce then publish at-rest substate3 to the later enemy pass.");
            ball.Free(); parent.Free();
        }
        GD.Print("Validated Smasher reserved throw-item weight2/Toss motion, high-byte carry copies, wall stop/rebound and final bounce handoff; room bracelet routing pending.");
    }

    private void ValidateCrownDungeonSmasherGrabProtocol()
    {
        var database = new EnemyDatabase();
        foreach (var (x, z, hit) in new[] { (8,-8,true),(8,8,true),(8,-9,false),(8,9,false),(-12,0,false),(12,0,true) })
        {
            var ball = new SmasherCharacter(); var parent = new SmasherCharacter();
            var room = Room060MovementFixture(); var random = new OracleRandom();
            ball.InitializeLinked(database.ImportedEnemy(0x74,0), room, new(80,64), random, parent);
            parent.InitializeLinked(database.ImportedEnemy(0x74,1), room, new(80,64), random, ball);
            ball.UpdateNormalFrame(Vector2.Zero, 1, _ => true, () => { }, _ => { });
            ball.BeginGrab();
            int grab = 0, angle = -1, sounds = 0;
            void Ball(int frame) => ball.UpdateNormalFrame(Vector2.Zero, frame, _ => false, () => { }, _ => sounds++,
                value => angle = value, value => grab = value);
            Ball(2);
            FailIf(ball.GrabSubstate != 1 || grab != 0x20 || ball.ZIndex != NpcCharacter.InFrontOfLinkZIndex,
                "$74 just-grabbed dispatch must set wLinkGrabState2=$20 and visible$c1.");
            ball.CopyCarriedPosition(new(80+x,64), z);
            ball.ReleaseGrab(0);
            Ball(3);
            FailIf(parent.Health != (hit ? 4 : 5) || parent.KnockbackCounter != (hit ? 16 : 0) ||
                parent.InvincibilityCounter != (hit ? 32 : 0) || sounds != (hit ? 1 : 0) || ball.Angle != 0,
                $"$74 released ball Xdelta{x}/Zdelta{z} lost source byte collision edges or direct-hit writes.");
            if (hit)
            {
                FailIf(angle != 8 || parent.KnockbackAngle != 24,
                    "$74 ball east of parent must knock parent west and reverse only reserved item C east.");
                Ball(4);
                FailIf(parent.Health != 4 || sounds != 1, "$74 parent invincibility must suppress repeated ball damage.");
                parent.Health = 0; // Isolate lethal knockback priority without a whole fight.
                for (int i = 0; i < 16; i++) parent.UpdateNormalFrame(Vector2.Zero, 5+i, _ => true, () => { }, _ => { });
                FailIf(parent.KnockbackCounter != 0 || parent.InvincibilityCounter != 16 || parent.IsDead ||
                    parent.Position != new Vector2(48,64) || parent.State != 8,
                    "$74 lethal hit must finish16 SPEED_200 knockback updates before no-health dispatch, without AI or Z movement.");
            }
            ball.FinishGrabBounce(); Ball(30);
            FailIf(ball.State != 8 || ball.ZIndex != NpcCharacter.BehindLinkZIndex,
                "$74 at-rest substate must restore state8/visible$c2 on its next enemy dispatch.");
            ball.Free(); parent.Free();
        }
        var held = new SmasherCharacter(); var owner = new SmasherCharacter();
        var fixture = Room060MovementFixture(); var rng = new OracleRandom();
        held.InitializeLinked(database.ImportedEnemy(0x74,0), fixture, new(80,64), rng, owner);
        owner.InitializeLinked(database.ImportedEnemy(0x74,1), fixture, new(80,64), rng, held);
        held.UpdateNormalFrame(Vector2.Zero, 1, _ => false, () => { }, _ => { }); held.BeginGrab();
        int drops = 0;
        for (int frame = 2; frame <= 360; frame++)
        {
            held.UpdateNormalFrame(Vector2.Zero, frame, _ => false, () => { }, _ => { }, _ => { }, _ => { }, () => drops++);
            if (frame == 359) FailIf(drops != 0 || held.State != 2 || held.ExpirationCounter != 179,
                "$74 held ball must remain owned until the180th even-frame expiration tick.");
        }
        FailIf(drops != 1 || held.State != 13 || !held.Visible,
            "$74 held expiration must force exactly one drop before retrying failed puff allocation.");
        held.Free(); owner.Free();
        GD.Print("Validated isolated Smasher grabbed/released/rest protocol, collision boundaries, reserved-item reversal, lethal knockback priority and held expiration; live bracelet integration pending.");
    }

    private void ValidateCrownDungeonSmasherMotion()
    {
        var database = new EnemyDatabase();
        var ball = new SmasherCharacter();
        var parent = new SmasherCharacter();
        var random = new OracleRandom();
        var room = Room060MovementFixture();
        ball.InitializeLinked(database.ImportedEnemy(0x74, 0), room, new(120,88), random, parent);
        parent.InitializeLinked(database.ImportedEnemy(0x74, 1), room, new(120,88), random, ball);
        int frame = 0, begins = 0, puffs = 0;
        bool puffAvailable = true;
        void Tick()
        {
            frame++;
            bool Puff(Vector2 _) { if (!puffAvailable) return false; puffs++; return true; }
            ball.UpdateNormalFrame(new(120,120), frame, Puff, () => begins++, _ => { });
            parent.UpdateNormalFrame(new(120,120), frame, Puff, () => begins++, _ => { });
        }
        FailIf(ball.Position != new Vector2(88,88) || ball.Palette != 1 || ball.AnimationIndex != 4,
            "$74 ball post-allocation setup must offset X by$20 and select palette1/pose4.");
        Tick();
        FailIf(ball.State != 9 || parent.State != 10 || parent.Speed != 0x28 || begins != 1,
            "$74 ball-before-parent update must select ground-ball pursuit in the first parent state8 dispatch.");
        for (int i = 0; i < 40 && parent.State == 10; i++) Tick();
        FailIf(parent.State != 11 || ball.State != 10 || parent.Position != new Vector2(104,88) || parent.Palette != 2,
            "$74 parent must accept X target102 from distance2, then signal pickup after the earlier ball pass.");
        Tick();
        FailIf(ball.ZFixed != -0x80 || ball.Position != new Vector2(88.625f,88),
            "$74 pickup must lift by half a pixel while moving independently at SPEED_a0.");
        for (int i = 0; i < 40 && ball.State == 10; i++) Tick();
        FailIf(ball.State != 11 || parent.State != 12 || parent.Counter2 != 30 || ball.ZFixed != -0xb80 ||
            ball.Position.X != 104.25f || parent.SpeedZ != -0xc0 || parent.Palette != 3,
            "$74 lift must stop when Z high reaches$f4, preserving low$80 and the XY overshoot fraction.");
        for (int i = 0; i < 60 && parent.State == 12; i++) Tick();
        FailIf(parent.State != 13 || parent.Counter2 != 0 || parent.ZFixed != 0 || parent.SpeedZ != -0x1e0,
            "$74 carry countdown must wait for landing before starting the high throw jump.");
        for (int i = 0; i < 14; i++) Tick();
        FailIf(ball.State != 11 || parent.SpeedZ != -0x20,
            "$74 must retain the ball while the parent is still rising on jump update14.");
        Tick();
        FailIf(parent.SpeedZ != 0 || parent.ZFixed != -0xf00 || ball.State != 12 || parent.Palette != 3,
            "$74 must release only at the exact zero-speed peak on jump update15.");
        Tick();
        FailIf((ball.ZFixed >> 8) != (parent.ZFixed >> 8) - 12,
            "$74 descending parent must still overwrite the released ball's Z high byte in its later update.");
        puffAvailable = false;
        while (frame < 360) Tick();
        FailIf(ball.State != 13 || ball.ExpirationCounter != 0 || !ball.Visible || puffs != 0,
            "$74 ball expiration must count180 even frames and retry a failed disappearing puff without hiding.");
        Tick();
        FailIf(ball.State != 13 || ball.ExpirationCounter != 0, "$74 disappearing retries must stop consuming expiration ticks.");
        puffAvailable = true; Tick();
        FailIf(ball.State != 14 || ball.Visible || ball.Counter1 != 60 || puffs != 1,
            "$74 successful disappearance must start the full60-update hidden interval.");
        for (int i = 0; i < 59; i++) Tick();
        FailIf(ball.State != 14 || ball.Counter1 != 1 || ball.Visible, "$74 ball respawn was one update early.");
        Tick();
        FailIf(ball.State != 15 || !ball.Visible || (ball.ZFixed >> 8) != -32 || ball.SpeedZ != 0 || puffs != 2 || begins != 1,
            "$74 respawn must start falling at Z high$e0 without replaying miniboss initialization.");
        ball.Free(); parent.Free();
        GD.Print("Validated isolated Smasher pursuit, half-pixel pickup, carry/peak throw, parent-to-ball Z ordering, even-frame expiration and puff/respawn timing; room/bracelet integration pending.");
    }

    private void ValidateCrownDungeonSmasherSourceData()
    {
        var drop = EnemyBehaviorTables.Shared.Smasher;
        FailIf(!drop.DropWallProbes.Select(value => value.Value).SequenceEqual(new[] {0x7c,0xa0,0x47,0x7d,0xa1,0x4f,0xaf,0xc9,0,8}),
            "$74 directionless throw must preserve bank$0f's raw table-overread bytes.");
        Vector2[] dropPoints = [new(3,212),new(128,27),new(207,188),new(152,107)];
        for (int blocked = 0; blocked < 16; blocked++)
        {
            int probe = 0;
            int angle = drop.BounceDroppedBall(new(99,88), point =>
            {
                FailIf(point != dropPoints[probe], "$74 angle$ff raw probes must accumulate wrapping high-byte coordinates.");
                return (blocked & (8 >> probe++)) != 0;
            });
            int expected = (blocked & 3) != 0 ? ((blocked & 12) != 0 ? 15 : 8) : (blocked & 12) != 0 ? 0 : 255;
            FailIf(angle != expected, "$74 dropped-ball reflection must retain the clean-US raw bounce table reads.");
        }
        var grab = new BraceletGrabGeometry();
        Vector2[] projectedLink = [new(80,58),new(85,64),new(80,69),new(74,64)];
        Rect2 Bounds(Vector2 center) => new(center-new Vector2(6,6),new Vector2(12,12));
        for (int direction = 0; direction < 4; direction++)
        {
            foreach (int edge in new[] { -13,-12,11,12 })
            foreach (Vector2 axis in new[] { Vector2.Right,Vector2.Down })
            {
                bool hit = grab.Overlaps(Bounds(new(80.75f,64.5f)),0,direction,
                    Bounds(projectedLink[direction]+axis*edge),0,false);
                FailIf(hit != (edge is -12 or 11),
                    $"Native grab direction{direction}, edge{edge}/{axis} lost high-byte projection or object-minus-Link edge orientation.");
            }
            foreach (int z in new[] { -10,-9,4,5 })
                FailIf(grab.Overlaps(Bounds(new(80,64)),0,direction,Bounds(projectedLink[direction]),z,false) != (z is -9 or 4),
                    $"Native grab direction{direction}, Z{z} lost Link.zh-$03 and wrapped [-7,+7) check.");
            FailIf(grab.Overlaps(Bounds(new(80,64)),0,direction,Bounds(projectedLink[direction]),0,true),
                "Native grab must reject a target with pending var2a bit7.");
        }
        FailIf(!grab.Overlaps(Bounds(new(255,64)),0,1,Bounds(new(252,64)),0,false) ||
            grab.Overlaps(Bounds(new(255,64)),0,1,Bounds(new(16,64)),0,false),
            "Native grab projection and XY arithmetic must wrap at the byte seam.");
        var data = EnemyBehaviorTables.Shared.Smasher;
        FailIf(data.DeathFrames != 120, "commonBossCode.s enemyBoss_dead must retain its 120-update counter.");
        var weights = new BraceletWeightDatabase();
        BraceletWeight[] expectedWeights = [new(0x1c,-0xf0,0x3c,0x64),new(0x20,-0x100,0x14,0x28),
            new(0x28,-0xe0,0x41,0x64),new(0x20,-0x100,0x14,0x28),new(0x20,-0x20,0x32,0x3c),new(0x20,-0x100,0x14,0x28)];
        for (int weight = 0; weight < 6; weight++)
            FailIf(weights.Weight(weight) != expectedWeights[weight], $"Ages itemWeights[{weight}] lost its native throw profile.");
        int[] smasherLift = [-8,0,0,7,6,0,0,-8,-6,0,-8,3,4,0,-8,-4,
            -13,0,-14,0,-13,0,-14,0,-13,0,-13,0,-13,0,-13,0];
        for (int frame = 0; frame < 4; frame++)
        for (int direction = 0; direction < 4; direction++)
        {
            int offset = frame * 8 + direction * 2;
            FailIf(weights.LiftOffset(2,frame,direction) != new Vector2I(smasherLift[offset+1],smasherLift[offset]),
                $"Smasher weight2 lift frame{frame}/direction{direction} lost Ages Z/X offsets.");
        }
        int[] parentEffects = [2,0,5,5,0x1c,0x1c,0x1c,0x1c,0,0x1c,0,0,0,0x1c,0x1c,0,
            0,0,0,0,0,0x2d,0x1c,0x1c,0,0x20,0x20,0x20,0x20,0x20,0x20,0];
        int[] ballEffects = [2,0,5,5,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0x2d,0,0,0,0,0,0,0,0,0,0];
        FailIf(!data.ParentEffects.Select(v => v.Value).SequenceEqual(parentEffects) ||
            !data.BallEffects.Select(v => v.Value).SequenceEqual(ballEffects) ||
            string.Concat(data.ActiveCollisions.Select(v => v.Value)) != "10111111010001100000011101111110",
            "$74 source collision modes $45/$63 or common active mask changed.");
        string[] animations = [
            "127@8,252,0,0;8,4,2,0;8,12,4,0;248,12,12,0",
            "127@8,252,6,0;8,4,8,0;8,12,10,0;248,15,12,0",
            "127@8,252,4,32;8,4,2,32;8,12,0,32;248,252,12,32",
            "127@8,252,10,32;8,4,8,32;8,12,6,32;248,249,12,32",
            "127@8,0,14,0;8,8,14,32"];
        var enemies = new EnemyDatabase();
        foreach (int subid in new[] { 0, 1 })
        {
            var record = enemies.ImportedEnemy(0x74, subid);
            FailIf(record.Health != 5 || record.DamageQuarters != 2 || record.RadiusX != 6 || record.RadiusY != 6 ||
                record.TileBase != 0 || record.Palette != 3 || !record.SourceGrayscaleInverted ||
                !record.Sprites.SequenceEqual(new[] { "spr_smasher" }) || !record.Animations.SequenceEqual(animations),
                $"$74:${subid:x2} must retain shared base properties, all four parent poses and ball pose4.");
        }
        // Independent source operands from object_code/ages/enemies/smasher.s.
        FailIf(data.ExpirationEvenTicks != 180 || data.RespawnFrames != 60 || data.RespawnZ != -32 ||
            data.Gravity != 0x20 || data.WanderFrames != 60 || data.CarryFrames != 30 ||
            data.HopSpeedZ != -0xc0 || data.ThrowJumpSpeedZ != -0x1e0 ||
            data.PickupOffsetX != 0x0e || data.PickupMinimumX != 0x18 || data.PickupSpanX != 0xc0 ||
            data.ArrivalBias != 2 || data.ArrivalSpan != 5 || data.CarriedZOffset != -12 ||
            data.LiftSpeedZ != 0x80 || data.InitialBallOffsetX != 0x20 ||
            data.HitInvincibility != 0x20 || data.HitKnockback != 0x10 || data.HitZBias != 8 || data.HitZSpan != 0x11,
            "ENEMY_SMASHER $74 imported expiration, respawn, pickup, jump or ball-hit operands changed.");
        Vector2[] positions = [new(0x38,0x38),new(0x38,0x78),new(0x78,0x38),new(0x78,0x78),
            new(0xb8,0x38),new(0xb8,0x78),new(0x58,0x58),new(0x98,0x58)];
        int[] angles = [6,10,22,26];
        for (int random = 0; random < 256; random++)
            FailIf(data.RespawnPosition((byte)random) != positions[(random & 14) / 2] ||
                data.WanderAngle((byte)random) != angles[random & 3],
                $"ENEMY_SMASHER $74 random byte ${random:x2} lost ordered Y/X respawn pairs or wander angles.");
        GD.Print("Validated Smasher source timers, signed jump/lift operands, pickup/hit boundaries and all random-byte table selections; runtime handler remains pending.");
    }
}
