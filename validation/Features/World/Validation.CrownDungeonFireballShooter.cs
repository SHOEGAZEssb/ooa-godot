using Godot;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownDungeonFireballShooterSourceData()
    {
        var data = EnemyBehaviorTables.Shared.FireballShooter;
        // Independent transcription of fireballShooter.s, not derived from
        // the generated rows consumed by the runtime reader.
        int[] timingOffsets = [0x4e, 0x7e, 0xae, 0xde];
        FailIf(!data.TimingOffsets.Select(value => value.Value).SequenceEqual(timingOffsets) ||
            data.LinkDistance != 0x24 || data.CooldownMask != 7 || data.CooldownBase != 0xc0 ||
            data.TileYOffset != 6 || data.TileXOffset != 8 || data.RoomClearSubId != 0x81,
            "ENEMY_FIREBALL_SHOOTER $50 source cooldown, tile offsets or room-clear subid changed.");
        FailIf(data.TimingOffsets.Any(value => !value.Source.StartsWith(
            "object_code/common/enemies/fireballShooter.s:fireballShooter_timingOffsets")),
            "ENEMY_FIREBALL_SHOOTER $50 timing table lost its source identity.");
        GD.Print("Validated fireball-shooter $50 source operands and ordered timing offsets.");
    }

    private void ValidateCrownDungeonFireballShooter()
    {
        var spawns = new List<RoomEntitySpawn>();
        var random = new OracleRandom();
        int roomCount = 3;
        bool partAvailable = true;
        // Independent source layout room0522.bin: tile $09 at $34,$3a,$74,$7a.
        var scanner = new FireballShooterRoomEntity(_world.LoadRoom(5, 0x22),
            new Vector2(0,9), 1, 0, random, count => count <= 4, () => partAvailable, () => roomCount);
        var frame = new RoomEntityFrame(_player, 0, false);
        scanner.PrepareForScreenTransition(spawns);
        FailIf(scanner.State != 1 || scanner.Visible || random.Calls != 1 || spawns.Count != 0,
            "$50 state0 must stop at hidden scanner state1 during scrolling preload.");
        scanner.UpdateFrame(frame, spawns);
        Vector2[] expected = [new(0x48,0x36), new(0xa8,0x36), new(0x48,0x76), new(0xa8,0x76)];
        var requests = spawns.Cast<FireballShooterChildSpawn>().ToArray();
        FailIf(!scanner.Finished || scanner.Position != Vector2.Zero || requests.Length != 4 ||
            !requests.Select(x => x.Position).SequenceEqual(expected) ||
            !requests.Select(x => x.TimingIndex).SequenceEqual(new[] { 1,2,3,0 }),
            "$50 scanner changed source tile order, child centers or cyclic timing indices.");
        var children = requests.Select(x => x.Parent.CreateChild(x.Position, x.TimingIndex)).ToArray();
        int[] timers = [126,174,222,78];
        spawns.Clear();
        for (int i = 0; i < children.Length; i++)
        {
            children[i].UpdateFrame(frame, spawns);
            FailIf(children[i].State != 8 || children[i].SubId != 0x81,
                "$50 child state0 must retain native subid $81 and stop at state8.");
            children[i].UpdateFrame(frame, spawns);
            FailIf(children[i].State != 9 || children[i].Counter1 != timers[i],
                "$50 child state8 must load its source timing offset without decrementing.");
        }
        FailIf(random.Calls != 5, "$50 parent and four native children must consume one initialization RNG value each.");
        var original = _player.Position;
        try
        {
            var child = children[0];
            // Manhattan distance $23 freezes; $24 permits decrement.
            _player.WarpTo(child.Position + new Vector2(17,18), recordSafe: false);
            child.UpdateFrame(frame, spawns);
            FailIf(child.Counter1 != 126, "$50 proximity gate must be strict Manhattan distance < $24.");
            _player.WarpTo(child.Position + new Vector2(18,18), recordSafe: false);
            child.UpdateFrame(frame, spawns);
            FailIf(child.Counter1 != 125, "$50 distance exactly $24 must advance cooldown.");
            for (int i = 0; i < 124; i++) child.UpdateFrame(frame, spawns);
            FailIf(spawns.Count != 0 || child.Counter1 != 1, "$50 fired before countdown zero.");
            var predictor = new OracleRandom();
            for (int i = 0; i < 5; i++) predictor.Next();
            int cooldown = 0xc0 + (predictor.Next().Value & 7);
            child.UpdateFrame(frame, spawns);
            FailIf(spawns is not [ZoraFireSpawn { PartId: 0x31 }] || child.Counter1 != cooldown || random.Calls != 6,
                "$50 must spawn PART $31 and consume exactly one cooldown RNG value.");
            spawns.Clear(); partAvailable = false;
            int next = 0xc0 + (predictor.Next().Value & 7);
            for (int i = 0; i < cooldown; i++) child.UpdateFrame(frame, spawns);
            FailIf(spawns.Count != 0 || child.Counter1 != next || random.Calls != 7,
                "$50 failed part allocation must still reset cooldown and consume RNG.");
            roomCount = 0;
            child.UpdateFrame(frame, spawns);
            FailIf(!child.Finished || child.Position != Vector2.Zero || child.Counter1 != 0xff ||
                !child.RetainsCounter1AfterDeletion || random.Calls != 7 || spawns.Count != 0,
                "$50 clean-US deletion fallthrough must clear XY, wrap the freed counter and skip projectile/RNG.");
            _player.WarpTo(new Vector2(8,8), recordSafe: false);
            children[1].UpdateFrame(frame, spawns);
            FailIf(!children[1].Finished || children[1].Counter1 != 0,
                "$50 deletion fallthrough must leave zero counter when Link is near the cleared origin.");
        }
        finally { _player.WarpTo(original, recordSafe: false); }
        foreach (var child in children) child.Free();
        scanner.Free();
        var limited = new FireballShooterRoomEntity(_world.LoadRoom(5,0x22), new Vector2(0,9),
            1,0,new OracleRandom(), count => count <= 2, () => true, () => 3);
        spawns.Clear(); limited.UpdateFrame(frame,spawns); limited.UpdateFrame(frame,spawns);
        FailIf(!limited.Finished || spawns.Count != 2, "$50 tile scan must stop at first failed enemy allocation.");
        limited.Free();

        using (var fixture = RoomEntityValidationFixture.ForRoot(this))
        {
            var room = _world.LoadRoom(5,0x22);
            fixture.Manager.LoadCutsceneRoom(5,room,includeTimePortals:false);
            var parent = new FireballShooterRoomEntity(room,new Vector2(0,9),1,0,
                new OracleRandom(), _ => true, () => true, () => 0);
            _player.WarpTo(new Vector2(100,100),recordSafe:false);
            try
            {
                var native = fixture.Manager.Spawn<FireballShooterRoomEntity>(new FireballShooterChildSpawn(parent,new Vector2(40,40),1));
                for (int i=0;i<3;i++) fixture.Manager.Update(1.0/60.0,_player);
                FailIf(!native.Finished || fixture.Manager.Entities<FireballShooterRoomEntity>().Count != 0,
                    "$50 native shutdown must release its actual enemy slot.");
                var reused = fixture.Manager.Spawn<FireballShooterRoomEntity>(new FireballShooterChildSpawn(parent,new Vector2(40,40),1));
                FailIf(reused.Counter1 != 0xff, "$50 reallocation lost the freed slot's dirty counter byte.");
                fixture.Manager.Update(1.0/60.0,_player);
                FailIf(reused.State != 8 || reused.Counter1 != 0xff, "$50 state0 must preserve an inherited counter byte.");
                fixture.Manager.Update(1.0/60.0,_player);
                FailIf(reused.Counter1 != 126, "$50 state8 must overwrite inherited counter1 from its timing table.");
                fixture.Manager.LoadCutsceneRoom(5,room,includeTimePortals:false);
                var cleared = fixture.Manager.Spawn<FireballShooterRoomEntity>(new FireballShooterChildSpawn(parent,new Vector2(40,40),1));
                FailIf(cleared.Counter1 != 0, "Room loading must clear native enemy counter residue.");
            }
            finally { _player.WarpTo(original,recordSafe:false); parent.Free(); }
        }

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach (bool batch in new[] { false,true })
        {
            LoadValidationRoom(4,0xb6);
            var placed = _entities.Entities<FireballShooterRoomEntity>().Single();
            FailIf(placed.Position != new Vector2(0,9) || _entities.RoomEnemyCount != 3,
                "$4:$b6 must preserve its uncounted scanner and three counted Arrow Moblins.");
            // room04b6.bin contains no $09 tiles: this original scanner creates
            // no children. Do not invent statues to justify its placement.
            input.CaptureForValidation([],[],Vector2.Zero);
            if (batch) scheduler.Advance(2.0/60.0,update);
            else { scheduler.Advance(1.0/60.0,update); scheduler.Advance(1.0/60.0,update); }
            FailIf(_entities.Entities<FireballShooterRoomEntity>().Count != 0 || _entities.RoomEnemyCount != 3,
                "$4:$b6 source scanner must delete without children or changing the room enemy count.");

            LoadValidationRoom(5,0x22);
            _player.WarpTo(new Vector2(120,88),recordSafe:false);
            void Step(int count) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            Step(1);
            FailIf(_entities.Entities<FireballShooterRoomEntity>().Single().State != 1,
                "$50 scanner must not run during its state0 update.");
            Step(1);
            var liveChildren = _entities.Entities<FireballShooterRoomEntity>();
            FailIf(liveChildren.Count != 4 || liveChildren.Any(x => x.State != 8) ||
                !liveChildren.Select(x => x.Position).SequenceEqual(expected) || _entities.RoomEnemyCount != 4,
                "$5:$22 children must initialize in later enemy slots on the scanner update without changing four Rope counts.");
            Step(1);
            var firstShooter = liveChildren[3];
            FailIf(firstShooter.Counter1 != 78, "$5:$22 fourth shooter must start at source timing index0.");
            Step(77);
            FailIf(firstShooter.Counter1 != 1 || _entities.Entities<ZoraFireProjectile>().Count != 0,
                "$5:$22 shooter fired before its 78-update cooldown completed.");
            Step(1);
            var fire = _entities.Entities<ZoraFireProjectile>().Single();
            Vector2 origin = firstShooter.Position;
            FailIf(fire.PartId != 0x31 || fire.State != 1 || fire.Counter != 8 || fire.Position != origin,
                "$50 must initialize PART $31 in the later native part pass without moving it.");
            _player.WarpTo(origin + Vector2.Right*40,recordSafe:false);
            Step(7);
            FailIf(fire.State != 1 || fire.Counter != 1 || fire.Position != origin,
                "$31 projectile moved before its independent eight-update aim delay.");
            Step(1);
            FailIf(fire.State != 2 || fire.Angle != 8 || fire.Position != origin,
                "$31 must acquire Link at delay completion and wait another update before moving.");
            Step(1);
            FailIf(fire.Position != origin + Vector2.Right,
                "$31 SPEED_180 first move must preserve its fractional byte and render only the high byte.");
            LoadValidationRoom(4,0xb6);
            FailIf(_entities.Entities<ZoraFireProjectile>().Count != 0,
                "Room replacement must cancel surviving native fireballs.");
        }
        GD.Print("Validated fireball shooter source scan/order, child timers, Manhattan gate, RNG, failed allocations, clean-US deletion fallthrough and $4:$b6 empty scan in single/batched gameplay updates.");
    }

    private void ValidateNativeFireballCollisions()
    {
        var data = new ZoraFireDatabase();
        foreach (int partId in new[] { 0x19,0x31 })
        for (int shield = 1; shield <= 3; shield++)
        {
            var inventory = new InventoryState(_treasures, OracleSaveData.CreateStandardGame());
            inventory.GiveTreasure(_treasures.GetObject($"TREASURE_OBJECT_SHIELD_0{shield-1}"));
            inventory.EquipA(InventoryState.ItemShield);
            var player = new Player(); AddChild(player);
            player.Initialize(new ValidationRingPlayerWorld(),inventory,new Vector2(72,72),new OracleRandom());
            player.Face(Vector2I.Right); player.UpdateShieldForValidation(true,false);
            Vector2 shieldEdge = new(player.ShieldCollisionBounds.End.X + 1, player.ShieldCollisionBounds.GetCenter().Y);
            var fire = new ZoraFireProjectile(new(shieldEdge,partId),data,p => p + Vector2.Down*16);
            var frame = new RoomEntityFrame(player,0,false);
            fire.UpdateFrame(frame);
            FailIf(player.OverlapsEnemyCollision(fire.CollisionBounds), "Fireball shield fixture must exclude Link's body.");
            fire.HandleLinkContact(player);
            FailIf(fire.Finished, $"PART ${partId:x2} shield collision must wait for the next native part update.");
            fire.UpdateFrame(frame);
            FailIf(fire.Finished != (shield >= 2), $"PART ${partId:x2} shield level {shield} violated active mask $0d.");
            fire.Free(); player.Free();
        }
        foreach (int partId in new[] { 0x19,0x31 })
        foreach (RingId ring in new[] { RingId.BlueHoly,RingId.RedHoly })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6cc,1); save.WriteWramByte(0xc6c6,(byte)ring);
            var inventory = new InventoryState(_treasures,save);
            FailIf(!inventory.EquipRingAt(0), "Could not equip fireball ring fixture.");
            var player = new Player(); AddChild(player);
            player.Initialize(new ValidationRingPlayerWorld(),inventory,new Vector2(72,72),new OracleRandom());
            var fire = new ZoraFireProjectile(new(player.Position,partId),data,p => p+Vector2.Down*16);
            var frame = new RoomEntityFrame(player,0,false);
            int health = player.HealthQuarters;
            fire.UpdateFrame(frame); fire.HandleLinkContact(player);
            bool protectedByRing = partId == 0x19 && ring == RingId.BlueHoly;
            FailIf(player.HealthQuarters != health - (protectedByRing ? 0 : 2) || fire.Finished,
                $"PART ${partId:x2} ring ${((int)ring):x2} lost its native collision mode or delayed deletion.");
            fire.UpdateFrame(frame);
            FailIf(!fire.Finished, $"PART ${partId:x2} must delete after contact status, including ring protection.");
            fire.Free(); player.Free();
        }
        GD.Print("Validated native PART $19/$31 post-object contact, shield masks, Blue/Red Holy Ring distinctions and next-update deletion.");
    }
}
