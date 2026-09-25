using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateArmosWarriorFight()
    {
        RunArmosWarriorFight(batch: true, reserveSlots: true);
        RunArmosWarriorFight(batch: false, reserveSlots: false);
    }

    private void RunArmosWarriorFight(bool batch, bool reserveSlots)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1, Vector2 movement = default, bool fire = false)
        {
            input.CaptureForValidation(fire ? ["attack"] : [], fire ? ["attack"] : [], movement);
            if (batch) scheduler.Advance(count / 60.0, update);
            else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
        }
        var reservedEnemies = (HashSet<int>)typeof(RoomEntityManager).GetField("_reservedEnemySlots", flags)!.GetValue(_entities)!;
        var partSlots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
        List<IRoomEntity> ReserveParts()
        {
            var reservations = new List<IRoomEntity>();
            for (int slot = 0; slot < 16; slot++)
            {
                if (partSlots.Values.Contains(slot)) continue;
                var reservation = new ArmosSlotReservation();
                reservations.Add(reservation);
                partSlots.Add(reservation, slot);
            }
            return reservations;
        }
        void ReleaseParts(List<IRoomEntity> reservations)
        {
            foreach (var reservation in reservations)
            { partSlots.Remove(reservation); reservation.Node.Free(); }
        }
        for (int i = 0; i < 11; i++) _inventory.GiveTreasure(TreasureDatabase.TreasureHeartContainer, 4);
        _saveData.SetRoomFlag(4, 0x80, 0xff, false);
        LoadValidationRoom(4, 0x86);
        _player.WarpTo(new Vector2(120, 40));
        _inventory.RefillHealth();
        FailIf(_currentRoom.IsSolid(_player.Position), "Armos approach must start on real room4:86 floor.");
        for (int i = 0; !IsTransitioning && i < 80; i++) Step(movement: Vector2.Up);
        FailIf(!IsTransitioning, "Actual room4:86 north approach did not enter Armos Warrior's room.");
        for (int i = 0; IsTransitioning && i < 160; i++) Step();
        FailIf(_currentRoom.Id != 0x80 || IsTransitioning, "Armos entrance did not finish its native scroll.");
        if (reserveSlots)
        {
            var spawner = _entities.Entities<ArmosWarriorActor>().Single();
            var reservations = new List<int>();
            for (int slot = 0; slot < 16 && reservedEnemies.Count < 14; slot++)
                if (reservedEnemies.Add(slot)) reservations.Add(slot);
            Step(3);
            FailIf(spawner.State != 1 || spawner.IsDead || _entities.Entities<ArmosWarriorActor>().Count != 1,
                "Armos spawner must wait with fewer than three free enemy slots, preserving its counted slot.");
            foreach (int slot in reservations) reservedEnemies.Remove(slot);
            var enemySlots = (Dictionary<IRoomEntity,int>)typeof(RoomEntityManager).GetField("_enemySlots",flags)!.GetValue(_entities)!;
            int spawnerSlot = enemySlots.Single(pair=>pair.Key.Node==spawner).Value;
            int[] childSlots = Enumerable.Range(0,16).Except(reservedEnemies).Take(3).ToArray();
            int randomBefore = _entities.RandomCalls;
            Step();
            // Source spawns all three children before deleting the spawner.
            // updateEnemies only visits child slots above its current cursor
            // in this pass; lower slots freed after scrolling wait one pass.
            FailIf(!_entities.Entities<ArmosWarriorActor>().Select(actor=>actor.State)
                    .SequenceEqual(childSlots.Select(slot=>slot>spawnerSlot?8:0)) ||
                _entities.RandomCalls != randomBefore + childSlots.Count(slot=>slot>spawnerSlot),
                "Armos children must initialize only when their allocated slot is later than the spawner's native cursor.");
            Step();
            FailIf(_entities.Entities<ArmosWarriorActor>().Any(actor => actor.State == 0) ||
                _entities.RandomCalls != randomBefore + 3,
                "Armos children must initialize once each on the next update after lower-slot allocation.");
        }
        for (int i = 0; _entities.Entities<ArmosWarriorActor>().Count != 3 && i < 5; i++) Step();
        var actors = _entities.Entities<ArmosWarriorActor>();
        FailIf(actors.Count != 3 || !actors.Select(actor => actor.SubId).SequenceEqual(new[] { 1, 2, 3 }) ||
            _entities.RoomEnemyCount != 1, "Armos spawner did not transfer its count to three ordered child slots.");
        var body = actors.Single(actor => actor.SubId == 1);
        var shield = actors.Single(actor => actor.SubId == 2);
        var sword = actors.Single(actor => actor.SubId == 3);
        if (reserveSlots)
        {
            var reservations = ReserveParts();
            int z = body.ZFixed;
            Step(3);
            FailIf(body.State != 8 || body.ZFixed != z || _entities.Entities<BossShadowEffect>().Count != 0,
                "Armos body must wait above the screen while the shared part pool is full.");
            ReleaseParts(reservations);
        }
        FailIf(body.Shield != shield || body.Sword != sword || shield.Body != body || sword.Shield != shield,
            "Armos body/shield/sword native references were not linked.");
        for (int i = 0; !_dialogue.IsOpen && i < 180; i++) Step();
        FailIf(!_dialogue.IsOpen || body.State != 9 || body.Substate != 2 || body.ZFixed != 0 ||
            shield.ShieldHits != 3 || !_entities.PlayerMovementDisabled,
            $"Armos intro did not land and show TX_2f01: state={body.State}/{body.Substate}, z={body.ZFixed}, Link={_player.Position}.");
        var before = (body.Position, body.Counter, sword.Position, sword.Counter);
        FailIf(body.Animation.CurrentTexture.GetSize() != new Vector2(24, 32) ||
            body.Animation.CurrentOffset != new Vector2(-12, -18),
            "enemyOamData4df9c's six cells must retain their negative Y and full 24x32 extent.");
        Step(4);
        FailIf(before != (body.Position, body.Counter, sword.Position, sword.Counter), "Armos actors moved through intro text.");
        _dialogue.Close(); Step();
        FailIf(body.Substate != 3 || body.Counter != 30 || body.ControlsDisabled,
            "Armos TX_2f01 completion did not start the 30-update sword lift and enable controls.");
        Step(30);
        FailIf(body.Substate != 4 || body.Counter != 70, "Armos lift did not hand off at exactly30 updates.");
        Step(69);
        FailIf(body.State != 9 || body.Counter != 1, "Armos initial retreat ended early.");
        Step();
        FailIf(body.State != 10 || body.Angle != 24 || body.Turn != 8 || sword.State != 10 || sword.Counter != 1,
            "Armos parent/sword initial retreat handoff lost source enemy-slot order.");
        FailIf(body.Position != new Vector2(119.5f, 122.5f) || sword.Position.Floor() != new Vector2(113, 45),
            $"Armos intro lost its 69 downward half-pixels, last left half-pixel, or sword high-byte lift: {body.Position}, {sword.Position}.");
        int lastHits = shield.ShieldHits;
        for (int i = 0; body.State < 13 && i < 12000; i++)
        {
            float travel = Math.Clamp(sword.Position.DistanceTo(shield.Position) / 1.5f, 10, 55);
            Vector2 predicted = shield.Position + OracleObjectMovement.Shared.Delta(body.Speed, body.Angle) * travel;
            Vector2 away = (predicted - sword.Position).Normalized();
            Vector2 goal = predicted + away * 40;
            goal = new(Math.Clamp(goal.X, 32, 207), Math.Clamp(goal.Y, 32, 143));
            // Reposition through the arena center periodically so repeated
            // throws cannot pin this walking strategy against a wall.
            if (i % 1800 >= 1700) goal = new Vector2(120, 88);
            Vector2 delta = goal - _player.Position;
            Vector2 move = delta.Length() > 2 ? delta.Normalized() : Vector2.Zero;
            Step(movement: move);
            if (shield.ShieldHits != lastHits)
            {
                FailIf(shield.ShieldHits != lastHits - 1 || body.State != 12 || body.Counter != 60 ||
                    body.Speed != 0x78 || body.InvincibilityCounter != 24 || shield.InvincibilityCounter != 24,
                    "Armos sword/shield collision lost its later-slot hit, 60-update recoil or $18 invincibility write.");
                lastHits = shield.ShieldHits;
            }
            if ((i & 255) == 0) _inventory.RefillHealth();
        }
        FailIf(body.State < 13, $"Walking bait did not break Armos's shield: hits={shield.ShieldHits}, body={body.Position}/{body.State}, sword={sword.Position}/{sword.State}, Link={_player.Position}.");
        FailIf(body.State != 13 || body.Counter != 90 || body.InvincibilityCounter != 96 ||
            _entities.Entities<ArmosWarriorActor>().Count != 1,
            "Armos shield teardown must remove both children and set the body's $5a/$60 counters in shield slot order.");
        Step(89);
        FailIf(_dialogue.IsOpen || body.Counter != 1, "Armos shield debris wait ended before 90 updates.");
        Step();
        FailIf(!_dialogue.IsOpen || body.State != 14 || body.Counter != 30 || body.CollisionMode != 0x44 ||
            _entities.Entities<ArmosWarriorActor>().Count != 1 || body.Health != 10,
            "Three sword/shield hits did not remove both children and expose the body after90 updates.");
        _dialogue.Close();
        _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 1);
        _inventory.EquipA(InventoryState.ItemSword);
        _inventory.ApplyDamage(4); // Keep sword beams out of this melee timing regression.
        for (int i = 0; !body.Dying && i < 6000; i++)
        {
            Vector2 delta = body.Position - _player.Position;
            Vector2 move = delta.Length() > 13 ? delta.Normalized() : Vector2.Zero;
            if (delta.Length() <= 26)
                _player.Face(Math.Abs(delta.X) > Math.Abs(delta.Y) ? new Vector2I(Math.Sign(delta.X), 0) : new Vector2I(0, Math.Sign(delta.Y)));
            int previousHealth = body.Health;
            // Let each swing finish before chasing and attacking again.
            Step(movement: move, fire: delta.Length() <= 26 && !_player.IsAttacking);
            if (body.Health != previousHealth)
            {
                FailIf(body.Health != Math.Max(0, previousHealth - 2) || body.InvincibilityCounter != 32 ||
                    !body.JustHit || body.Dying,
                    "Armos melee must apply two damage and $20 invincibility after its handler, preserving JUST_HIT until the next enemy update.");
                if (body.Health == 0)
                {
                    Step();
                    FailIf(body.Dying || body.JustHit || body.InvincibilityCounter != 31,
                        "Armos zero-health JUST_HIT update must still run its handler before death begins.");
                    Step();
                    FailIf(!body.Dying || body.Counter != 119,
                        "Armos must enter common boss death on the update after zero-health JUST_HIT clears.");
                    break;
                }
            }
            if ((i & 255) == 0) { _inventory.RefillHealth(); _inventory.ApplyDamage(4); }
        }
        FailIf(!body.Dying || body.Health != 0 || _entities.RoomEnemyCount != 1,
            $"Actual sword attacks did not defeat exposed Armos: hp={body.Health}, state={body.State}, body={body.Position}, Link={_player.Position}, LinkHealth={_inventory.HealthQuarters}, dying={_player.IsDying}, equipped={_inventory.EquippedA}.");
        FailIf(body.Counter != 119 || !_entities.LinkCollisionsAndMenuDisabled,
            "Armos death must set120 then decrement119 on its entry update.");
        Step(118);
        FailIf(body.IsDead || body.Counter != 1, "Armos death flicker ended before the source120-update boundary.");
        if (reserveSlots)
        {
            var reservations = ReserveParts();
            Step(3);
            FailIf(body.IsDead || body.Counter != 1 || _entities.RoomEnemyCount != 1 ||
                _entities.Entities<BossDeathExplosionEffect>().Count != 0,
                "Armos death must retry a full part pool at counter1 without releasing its room count.");
            ReleaseParts(reservations);
        }
        Step();
        FailIf(_entities.Entities<ArmosWarriorActor>().Count != 0 || _entities.Entities<BossDeathExplosionEffect>().Count != 1 ||
            _entities.RoomEnemyCount != 1, "Armos did not transfer its live count to the boss explosion.");
        // partCode04 ignores its own DEAD status and retains the boss count
        // until its ordinary terminal animation dispatch.
        _entities.EntityAdapters<BossDeathExplosionRoomEntity>().Single().ClearHealthAndCollision();
        Step(78);
        FailIf(_saveData.HasRoomFlag(4, 0x80, OracleSaveData.RoomFlag80), "Armos reward ran before the explosion terminal dispatch.");
        Step();
        FailIf(!_saveData.HasRoomFlag(4, 0x80, OracleSaveData.RoomFlag80) || _entities.Entities<MinibossPortal>().Count != 0,
            "Armos explosion must publish zero enemies to its reward in the same update.");
        Step(19);
        FailIf(_entities.Entities<MinibossPortal>().Count != 0, "Armos portal appeared before reward update20.");
        Step();
        FailIf(_entities.Entities<MinibossPortal>().Count != 1 || _entities.LinkCollisionsAndMenuDisabled,
            "Armos completion did not create the portal and restore Link.");
        LoadValidationRoom(4, 0x80); Step(8);
        FailIf(_entities.Entities<ArmosWarriorActor>().Count != 0 || _entities.Entities<DungeonRewardRoomEntity>().Count != 0 ||
            _entities.Entities<MinibossPortal>().Count != 1,
            "Completed Armos room did not preserve its portal and suppress spawner/reward on re-entry.");
    }

    private sealed class ArmosSlotReservation : IRoomEntity
    {
        public Node2D Node { get; } = new();
        public void SetTransitionDrawOffset(Vector2 offset) { }
    }

    private void ValidateArmosWarriorSourceData()
    {
        var targetBounds = new Rect2(new Vector2(94.75f, 94.75f), new Vector2(12, 12));
        foreach (var (delta, overlaps) in new[] { (-11, false), (-10, true), (9, true), (10, false) })
        {
            var otherBounds = new Rect2(new Vector2(96.25f + delta, 96.25f), new Vector2(8, 8));
            FailIf(RoomEntityManager.ObjectCollisionXYOverlaps(targetBounds, otherBounds) != overlaps,
                "Native item/Armos overlap must discard fractional positions and preserve the asymmetric summed-radius edges.");
        }
        var bosses = new DungeonBossDatabase();
        var data = new ArmosWarriorDatabase();
        for (int subid = 0; subid < 4; subid++)
        {
            var record = bosses.Enemy(0x73, subid);
            FailIf(record.Sprites is not ["spr_armoswarrior", "spr_armoswarriorshield", "spr_armoswarriorsword"] ||
                record.Health != new[] { 10, 10, 3, 127 }[subid] || record.Palette != Math.Max(1, subid) ||
                record.RadiusY != new[] { 6, 6, 12, 0 }[subid] ||
                record.RadiusX != (subid == 3 ? 0 : 6) || record.DamageQuarters != 2 || record.Animations.Length != 12,
                $"Armos Warrior $73:${subid:x2} lost the source enemy73SubidData/extra-data or chained object GFX $b4-$b6.");
        }
        FailIf(data.ShieldOffset(0) != new Vector2I(3, -5) || data.ShieldOffset(1) != new Vector2I(7, -5),
            "Armos Warrior shield offset table changed.");
        int[] boxes = [-4,0,8,3, -2,-2,6,6, 0,-4,3,8, 2,-2,6,6, 4,-1,8,3, 2,2,6,6, 1,4,3,8, -2,2,6,6];
        int[] boundaries = [0x51,0xfe,0x51,0x98,0xfe,0x98,0x60,0x98,0x60,0xfe,0x60,0x51,0xfe,0x51,0x51,0x51];
        for (int frame = 0; frame < 8; frame++)
        {
            var box = data.SwordBox(frame);
            FailIf(box.Offset != new Vector2I(boxes[frame * 4 + 1], boxes[frame * 4]) ||
                box.RadiusY != boxes[frame * 4 + 2] || box.RadiusX != boxes[frame * 4 + 3],
                $"Armos sword frame{frame} lost its high-byte offsets/radii.");
        }
        for (int angle = 0; angle < 32; angle++)
        {
            int offset = (((angle + 2) & 31) / 4) * 2;
            FailIf(data.SwordBoundaries(angle) != new Vector2I(boundaries[offset + 1], boundaries[offset]),
                "Armos sword boundary octant selection lost add2/and1c/rrca.");
        }
        for (int counter = 1; counter <= 0x70; counter++)
            FailIf(data.SwordSpeed(counter) != new[] { 10, 20, 40, 50 }[counter / 32],
                "Armos sword deceleration lost source swap/rrca/and03 speed selection.");
        for (int hits = 0; hits < 3; hits++)
            FailIf(data.ParentSpeed(hits) != new[] { 60, 50, 40 }[hits], "Armos parent shield-hit speed table changed.");
        const string mask = "11111111111101100000011111111110";
        for (int item = 0; item < 32; item++)
            FailIf(data.CollisionEnabled(item) != (mask[item] == '1'), "Armos collision mask lost native item bit ordering.");
        foreach (var (mode, sword, hook, beam) in new[] { (0x44,0x21,0x21,0x21), (0x60,0x16,0x1b,0),
            (0x61,0x15,0x1b,0x20), (0x62,0x17,0,0x20) })
            FailIf(data.CollisionEffect(mode, 4) != sword || data.CollisionEffect(mode, 13) != hook ||
                data.CollisionEffect(mode, 25) != beam || data.CollisionEffect(mode, 0x1d) != (mode == 0x60 ? 0 : 0x20),
                "Armos protected/shield/sword/unprotected collision modes changed, including Pegasus pass-through versus absorption.");
        FailIf(data.Message(0x2f01).Position != 1 || data.Message(0x2f01).Text !=
            "My mighty sword\nand mighty\nshield shall\ncrush you!" ||
            data.Message(0x2f02).Position != 0 || data.Message(0x2f02).Text !=
            "NO!\\stop\nMy mighty sword\nis broken...\nYou'll pay for\nthis!!!",
            "Armos TX_2f01/TX_2f02 lost text, source line breaks, position or stop command.");

        var swordRecord = bosses.Enemy(0x73, 3);
        var launch = OracleGraphicsCache.GetAnimationDefinition(swordRecord.Animations[10]);
        var spin = OracleGraphicsCache.GetAnimationDefinition(swordRecord.Animations[11]);
        FailIf(launch.Frames.Length != 9 || launch.LoopStart != 1 || launch.Frames[0].Duration != 1 ||
            spin.Frames.Length != 8 || spin.LoopStart != 0 ||
            !spin.Frames.Select(frame => frame.Parameter).SequenceEqual(Enumerable.Range(0, 8)) ||
            spin.Frames.Any(frame => frame.Duration != 2) || !launch.Frames.Skip(1).SequenceEqual(spin.Frames),
            "enemyAnimation3788a must fall through to3788d, then loop only the eight spin frames.");
        var node = new Node2D();
        var animation = new EnemyAnimationPlayer(node, swordRecord.Animations.Length);
        animation.Load(EnemyCharacterConfiguration.FromImported(swordRecord).Source,
            swordRecord.Animations, swordRecord.TileBase, swordRecord.Palette);
        for (int repetition = 0; repetition < 2; repetition++)
        {
            animation.SetAnimation(10);
            FailIf(animation.FrameIndex != 0, "Armos sword launch did not restart its single intro frame.");
            animation.Advance();
            for (int update = 0; update < 48; update++)
            {
                FailIf(animation.FrameIndex != 1 + (update / 2) % 8 || animation.CurrentParameter != (update / 2) % 8,
                    "Armos sword launch/spin runtime replayed the intro or changed two-update frame timing.");
                animation.Advance();
            }
        }
        node.Free();
    }
}
