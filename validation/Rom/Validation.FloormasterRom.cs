using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFloormasterRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (int room in new[] { 0x19, 0xa7 })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, room);
            FailIf(_runtimeState.ReadWramByte(WramAddress.wDungeonWallmasterDestRoom) != (room == 0x19 ? 0x26 : 0xaa),
                "dungeonData06/08 must publish their original $26/$aa return rooms when their layouts load.");
            var parent = _entities.Entities<FloormasterCharacter>().Single();
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_enemySlots", flags)!.GetValue(_entities)!;
            int parentSlot = room == 0x19 ? 6 : 0; // Six ordered ITEM_DROP reservations precede $5:$19's spawner.
            FailIf(parent.Position != new Vector2(room == 0x19 ? 0 : 16, 48) ||
                slots.Single(pair => pair.Key.Node == parent).Value != parentSlot,
                $"Floormaster $5:${room:x2} must reuse its original ordered room placement and encoded YX=$30/$00 or $10; actual XY={parent.Position}, slot={slots.Single(pair => pair.Key.Node == parent).Value}.");
            // A focused enemy fixture retains the actual room collision map.
            // $5:$19 also runs its ordered red Wizzrobe in slot$07. Keep
            // that neighbor in both object walks and the shared RNG stream.
            Vector2 linkPosition = default;
            for (int y = 40; y < _currentRoom.Height - 24 && linkPosition == default; y += 16)
            for (int x = 40; x < _currentRoom.Width - 24; x += 16)
                if (!_collision.Collides(new(x, y)) && _currentRoom.GetTerrainInfo(new(x, y)).Hazard == HazardType.None)
                { linkPosition = new(x, y); break; }
            FailIf(linkPosition == default, "Floormaster fixture needs real accessible floor.");
            _player.WarpTo(linkPosition);
            typeof(Player).GetField("_enemyInvincibilityFrames", flags)!.SetValue(_player, 100000f);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x35 * 4];
            int parentAddress = 0xd080 + parentSlot * 256;
            rom[parentAddress] = (byte)(room == 0x19 ? 0x71 : 0x11); rom[parentAddress + 1] = 0x35;
            rom.Word(parentAddress + 10, 0x3000); rom.Word(parentAddress + 12, room == 0x19 ? 0 : 0x1000);
            if (room == 0x19)
            {
                var wizard = _entities.Entities<WizzrobeCharacter>().Single();
                rom[0xcc0a] = 0x9f;
                rom[0xd780] = 0x81; rom[0xd781] = 0x40; rom[0xd782] = 1;
                rom.Word(0xd78a, (int)(wizard.Position.Y * 256));
                rom.Word(0xd78c, (int)(wizard.Position.X * 256));
            }
            rom[0xd004] = 1; rom[0xd024] = 0x80; rom[0xd009] = 0xff;
            rom[0xcc6e] = 1;
            rom[0xcdd1] = (byte)_entities.RoomEnemyCount;
            var reserved = (HashSet<int>)typeof(RoomEntityManager).GetField("_reservedEnemySlots", flags)!.GetValue(_entities)!;
            foreach (int slot in reserved.Where(slot => slot != parentSlot && !(room == 0x19 && slot == 7)))
            {
                int address = 0xd080 + slot * 256;
                rom[address] = 3; rom[address + 1] = 0x35; rom[address + 2] = 1;
                rom[address + 4] = 2; rom[address + 0x29] = 17;
            }
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            SeedItemDropProducersRom(rom);
            bool text = true;
            _entities.TextActiveSource = () => text;
            int update = 0;
            void Compare()
            {
                rom[0xcba0] = (byte)(text ? 1 : 0);
                rom[0xd009] = (byte)_player.LinkMovementAngle;
                rom[0xffaa] = (byte)(-(int)_entities.ToScreen(Vector2.Zero).Y);
                rom[0xffac] = (byte)(-(int)_entities.ToScreen(Vector2.Zero).X);
                rom.Update(_entities.FrameCounter, _player.Position);
                foreach (var (adapter, slot) in slots.Where(pair => pair.Key.Node is FloormasterCharacter))
                {
                    var hand = (FloormasterCharacter)adapter.Node;
                    int address = 0xd080 + slot * 256;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(hand.Animation)!;
                    FailIf(rom[address] == 0 || hand.Record.SubId != rom[address + 2] || hand.State != rom[address + 4] ||
                        hand.Counter != rom[address + 6] || hand.Angle != rom[address + 9] || hand.Speed != rom[address + 16] ||
                        hand.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        hand.ZFixed != unchecked((short)rom.Word(address + 14)) || hand.Health != rom[address + 0x29] ||
                        hand.InvincibilityCounter != unchecked((sbyte)rom[address + 0x2b]) || hand.KnockbackCounter != rom[address + 0x2d] ||
                        hand.CollisionEnabled != ((rom[address + 0x24] & 0x80) != 0) ||
                        hand.Visible != ((rom[address + 0x1a] & 0x80) != 0) || timer != rom[address + 0x20] || hand.AnimationParameter != rom[address + 0x21],
                        $"Floormaster $5:${room:x2}, update={update}, slot${slot:x2}, batch={batched}: " +
                        $"runtime state/counter/angle/speed=${hand.State:x2}/{hand.Counter}/${hand.Angle:x2}/{hand.Speed}, XY={hand.Position}, Z={hand.ZFixed}, HP={hand.Health}, inv/recoil={hand.InvincibilityCounter}/{hand.KnockbackCounter}, anim={timer}/{hand.AnimationParameter}; " +
                        $"ROM=${rom[address + 4]:x2}/{rom[address + 6]}/${rom[address + 9]:x2}/{rom[address + 16]}, XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, Z=${rom.Word(address + 14):x4}, HP={rom[address + 0x29]}, inv/recoil=${rom[address + 0x2b]:x2}/{rom[address + 0x2d]}, anim={rom[address + 0x20]}/{rom[address + 0x21]}.");
                    if (hand.Record.SubId == 0)
                        FailIf(hand.LiveChildren != rom[address + 0x30] || hand.RemainingChildren != rom[address + 0x33] ||
                            hand.ChildSubId != rom[address + 0x34] || hand.LastLinkPosition != new Vector2(rom[address + 0x32], rom[address + 0x31]),
                            "Floormaster parent quota/live count/last Link coordinates must match native child publications.");
                }
                var actual = _random.CaptureState();
                FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || actual.Calls - seed.Calls != rom.RandomCalls ||
                    _entities.RoomEnemyCount != rom[0xcdd1] || _entities.ActiveRoomDefeatBitset != rom[0xcdc1],
                    $"Floormaster room${room:x2} update={update}: RNG=${actual.Rng2:x2}{actual.Rng1:x2}/{actual.Calls - seed.Calls} vs native=${rom[0xff95]:x2}{rom[0xff94]:x2}/{rom.RandomCalls}; count={_entities.RoomEnemyCount}/{rom[0xcdd1]}, defeats=${_entities.ActiveRoomDefeatBitset:x2}/${rom[0xcdc1]:x2}.");
                update++;
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step(3);
            FailIf(parent.State != 1 || parent.Counter != 60 || parent.RemainingChildren != 3 || parent.LiveChildren != 0,
                "Floormaster state0 initializes under text; its 60-update countdown waits for normal enemy eligibility.");
            text = false;
            Step(59); FailIf(parent.LiveChildren != 0, "Floormaster must not spawn before counter60 reaches zero.");
            Step(1); FailIf(parent.LiveChildren != 1 || parent.Counter != 128,
                "Floormaster must allocate one uncounted hand and initialize its higher native slot in the same enemy pass.");
            text = true; Step(4); text = false;
            Step(440);
            FailIf(parent.RemainingChildren != 3 || parent.LiveChildren is < 1 or > 3,
                "Retreating hands must release only live count and permit respawn without reducing the kill quota.");
            int killed = 0, guard = 0;
            while (!parent.IsDead && guard++ < 600)
            {
                var pair = slots.FirstOrDefault(pair => pair.Key.Node is FloormasterCharacter { State: 11, InvincibilityCounter: 0, Health: > 0 });
                if (pair.Key is null) { Step(1); continue; }
                var hand = (FloormasterCharacter)pair.Key.Node;
                rom.HitWithSword(ItemCollisionType.L1Sword, 17, pair.Value);
                FailIf(!((ISwordHittableRoomEntity)pair.Key).ApplySwordHit(hand.CollisionBounds,
                    hand.Position.Floor() - Vector2.Right, 17, EnemyKnockbackStrength.Low, new List<RoomEntitySpawn>()),
                    "A chasing Floormaster must accept its native lethal sword collision.");
                killed++; Step(20);
            }
            FailIf(!parent.IsDead || killed != 3 || parent.RemainingChildren != 0 ||
                _entities.Entities<FloormasterCharacter>().Count != 0 || _entities.RoomEnemyCount != (room == 0x19 ? 1 : 0) ||
                _entities.ActiveRoomDefeatBitset != (room == 0x19 ? 0x80 : 2),
                "Three uncounted child deaths must exhaust the spawner, silently decrement room count and mark its original defeat index.");
            _entities.LoadRoom(5, _currentRoom);
            FailIf(_entities.Entities<FloormasterCharacter>().Count != 0,
                "The defeated Floormaster spawner must remain suppressed on immediate room re-entry.");
        }
        ValidateFloormasterGaleRom();
        foreach (bool batched in new[] { false, true }) ValidateFloormasterGrabRom(batched);
        GD.Print("Validated Floormaster clean-US lifecycle in $5:$19 and $5:$a7 through individual/batched gameplay: native spawner/child order/RNG, hovering/chasing/retreating, respawn quota, lethal recoil/defeat, and actual Link collision, grab animation and dungeon-return handoff.");
    }

    private void ValidateFloormasterGaleRom()
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0xa7);
        _player.WarpTo(new(0x78, 0x78));
        _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
        var parent = _entities.Entities<FloormasterCharacter>().Single();
        var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
        rom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x35 * 4];
        rom[0xd080] = 0x11; rom[0xd081] = 0x35;
        rom.Word(0xd08a, 0x3000); rom.Word(0xd08c, 0x1000);
        rom[0xd009] = 0xff;
        var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
        void Step() => StepGameplayUpdates(1, Vector2.Zero, batched: true, afterUpdate: () =>
        {
            rom[0xd009] = (byte)_player.LinkMovementAngle;
            // Declared camera input follows the original room viewport.
            rom[0xffaa] = (byte)(-(int)_entities.ToScreen(Vector2.Zero).Y);
            rom.Update(_entities.FrameCounter, _player.Position);
            var random = _random.CaptureState();
            FailIf(parent.LiveChildren != rom[0xd0b0] || parent.RemainingChildren != rom[0xd0b3] ||
                parent.Counter != rom[0xd086] || _entities.RoomEnemyCount != rom[0xcdd1] ||
                random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                $"Floormaster Gale frame={_entities.FrameCounter}: live/quota/counter runtime={parent.LiveChildren}/{parent.RemainingChildren}/{parent.Counter}, native={rom[0xd0b0]}/{rom[0xd0b3]}/{rom[0xd086]}; count={_entities.RoomEnemyCount}/{rom[0xcdd1]}, RNG=${random.Rng2:x2}{random.Rng1:x2}/{random.Calls - seed.Calls} vs ${rom[0xff95]:x2}{rom[0xff94]:x2}/{rom.RandomCalls}; native child state/timer/Z=${rom[0xd184]:x2}/{rom[0xd187]}/${rom.Word(0xd18e):x4}.");
        });
        int guard = 0;
        while (!_entities.Entities<FloormasterCharacter>().Any(hand => hand.State == 11) && guard++ < 180) Step();
        var target = _entities.EntityAdapters<FloormasterRoomEntity>().Single(hand => ((FloormasterCharacter)hand.Node).State == 11);
        var actor = (FloormasterCharacter)target.Node;
        int kills = _saveData.ReadWramByte(WramAddress.wTotalEnemiesKilled);
        new SeedSatchelDatabase().TryGet(ItemId.GaleSeed, out var gale);
        rom.HitWithSword(ItemCollisionType.GaleSeed, 0, target.Slot);
        FailIf(rom[0xd184] != 5, $"Declared native Gale collision must install state5, got${rom[0xd184]:x2}.");
        FailIf(!target.ApplySeedCollision(actor.CollisionBounds, actor.Position.Floor() - Vector2.Right, gale,
            ItemCollisionType.GaleSeed, new List<RoomEntitySpawn>()).Contact || actor.State != 5,
            "Floormaster Gale collision must install native state5 and use its shared ascent owner.");
        guard = 0;
        while (!actor.IsDead && guard++ < 180) Step();
        FailIf(!actor.IsDead || parent.RemainingChildren != 2 || parent.LiveChildren != 0 ||
            _entities.RoomEnemyCount != 1 || _entities.ActiveRoomDefeatBitset != 0 ||
            _saveData.ReadWramByte(WramAddress.wTotalEnemiesKilled) != kills,
            "Gale must delete the uncounted hand and decrement live/quota, without puff, kill-counter or recent-defeat effects.");
    }

    private void ValidateFloormasterGrabRom(bool batched)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(5, 0xa7);
        _player.WarpTo(new(0x78, 0x78));
        FailIf(_collision.Collides(_player.Position), "$5:$a7 grab approach must begin on real floor.");
        var enemy = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
        enemy[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x35 * 4];
        enemy[0xd080] = 0x11; enemy[0xd081] = 0x35;
        enemy.Word(0xd08a, 0x3000); enemy.Word(0xd08c, 0x1000);
        var seed = _random.CaptureState(); enemy[0xff94] = seed.Rng1; enemy[0xff95] = seed.Rng2;
        var link = new SomariaRom(_saveData, seed, _currentRoom, 0, 0x78, 0x78);
        link.InitializeLinkGameplay(); link.InitializeLinkWalkingAnimation();
        for (int address = 0xd000; address < 0xd040; address++) enemy[address] = link[address];
        link[0xcc3e] = 0x91;
        _runtimeState.SetWramByte(WramAddress.wDungeonWallmasterDestRoom, 0x91);
        int update = 0;
        bool requested = false;
        bool text = false;
        _entities.TextActiveSource = () => text;
        Warp destination = default;
        _entities.RoomWarpRequested += warp => { requested = true; destination = warp; };
        void Compare()
        {
            link[0xcc4f] = enemy[0xcc4f];
            for (int address = 0xd000; address < 0xd040; address++) link[address] = enemy[address];
            link.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            for (int address = 0xd000; address < 0xd040; address++) enemy[address] = link[address];
            enemy[0xcc4f] = link[0xcc4f]; enemy[0xcc6e] = link[0xcc6e];
            enemy[0xcba0] = link[0xcba0];
            enemy.Update(_entities.FrameCounter, _player.Position);
            enemy.ResolveLinkCollisions();
            foreach (var hand in _entities.EntityAdapters<FloormasterRoomEntity>())
            {
                var actor = (FloormasterCharacter)hand.Node;
                int address = 0xd080 + hand.Slot * 256;
                int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor.Animation)!;
                FailIf(actor.State != enemy[address + 4] || actor.AnimationParameter != enemy[address + 0x21] ||
                    timer != enemy[address + 0x20],
                    $"Floormaster grab update={update} hand slot${hand.Slot:x2}: runtime state/animation=${actor.State:x2}/{actor.AnimationParameter}/{timer}, native=${enemy[address + 4]:x2}/{enemy[address + 0x21]}/{enemy[address + 0x20]}, text={text}.");
            }
            FailIf(_player.WallmasterGrabPending != (enemy[0xcc4f] == 12) ||
                _player.WallmasterGrabActive != (link[0xd004] == 12) ||
                _player.WallmasterGrabActive && _player.WallmasterGrabSubstate != enemy[0xd005] ||
                _player.PatchCollisionsEnabled != ((link[0xd024] & 0x80) != 0) ||
                _player.Visible != ((enemy[0xd01a] & 0x80) != 0) ||
                _runtimeState.ReadWramByte(WramAddress.wWarpsDisabled) != link[0xcc6e],
                $"Floormaster grab update={update}, batch={batched}: runtime pending/active/sub/visible={_player.WallmasterGrabPending}/{_player.WallmasterGrabActive}/{_player.WallmasterGrabSubstate}/{_player.Visible}; native force/state/sub/visible=${enemy[0xcc4f]:x2}/${link[0xd004]:x2}/{enemy[0xd005]}/${enemy[0xd01a]:x2}.");
            update++;
        }
        void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
        int guard = 0;
        while (!_player.WallmasterGrabPending && guard++ < 500) Step(1);
        FailIf(!_player.WallmasterGrabPending || _player.WallmasterGrabActive || !_player.PatchCollisionsEnabled,
            "Actual Floormaster chase must publish state$0c without consuming it in the collision pass.");
        Step(1);
        FailIf(!_player.WallmasterGrabActive || _player.WallmasterGrabSubstate != 1 || _player.PatchCollisionsEnabled,
            "Link must consume and immediately initialize state$0c before the grabbing hand's next update.");
        // Link state$0c keeps running under text; initialized enemy animations freeze.
        _dialogue.ShowMessage("Floormaster grab pause.", _player.Position.Y); link[0xcba0] = 1; text = true;
        Step(4); _dialogue.Close(); link[0xcba0] = 0; text = false;
        guard = 0;
        while (!requested && guard++ < 150) Step(1);
        FailIf(!requested || link[0xcc47] != 0x85 || link[0xcc48] != 0x91 || link[0xcc49] != 5 ||
            link[0xcc4a] != 0x87 || link[0xcc4b] != 3 || destination.DestinationGroup != 5 ||
            destination.DestinationRoom != 0x91 || destination.DestinationPosition != 0x87 ||
            destination.DestinationTransition != WarpDestinationTransition.Fall || !destination.DirectFadeOut,
            "Terminal grab must defer the native $85:$91/$87 falling return warp until Link's following update.");
        _player.WarpTo(new(0x78, 0x78));
        FailIf(_player.WallmasterGrabActive || _player.WallmasterGrabPending || !_player.Visible ||
            !_player.PatchCollisionsEnabled || _runtimeState.ReadWramByte(WramAddress.wWarpsDisabled) != 0,
            "Room reload must cancel the captured Link owner and restore ordinary visibility/collision/warp admission.");
    }
}
