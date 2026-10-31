using Godot;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMinecartMountRom() => ValidateMinecartMountGameplayRom(false);

    private void ValidateMinecartMountGameplayRom(bool bombchu, int heartRing = 0xff)
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x78); _entities.Clear();
            // This cart fixture excludes placed gate/sensor producers. Declare
            // their open $1b:$01 track input, independently compared in the
            // gate signal fixture, rather than relying on constructor writes.
            _currentRoom.SetPositionTileAndCollision(new(88,104),0x00,0x0c,0,preserveRenderedTile:true);
            _currentRoom.SetPositionTileAndCollision(new(104,104),_currentRoom.GetMetatile(new(104,104)),0x0a,0,preserveRenderedTile:true);
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (heartRing != 0xff) EquipHeartRingVehicle(heartRing);
            if (bombchu)
            {
                _inventory.GiveTreasure(TreasureId.Bombchus, 0x10);
                if (batched) _inventory.EquipB(TreasureId.Bombchus);
                else _inventory.EquipA(TreasureId.Bombchus);
            }
            var records = new StaticDungeonObjectDatabase(AgesGameContent.AssetRoot).Minecarts(4);
            ResetMinecartsForValidation(_runtimeState, records);
            var interaction = new MinecartInteractionRoomEntity(new MinecartInteractionSpawn(new(0x78, 0x88), StaticSlot: 2),
                _currentRoom, _runtimeState,
                new DungeonInteractionVisualDatabase(AgesGameContent.AssetRoot).Visual("minecart"), _entities);
            _entities.AddEntity(interaction);
            var cart = new MinecartValidationView(_entities, _player);
            Vector2 start = new(136.25f, 136.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Left);
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Skull 4:78 minecart boarding must begin on the original safe platform.");
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 3, 136, 136)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            if (bombchu) rom[0xc6b3] = 0x10;
            const int slot = 0xd240;
            for (int address = 0xcd80; address < 0xcdc0; address++) rom[address] = _runtimeState.ReadWramByte(address);
            rom[slot] = 1; rom[slot + 1] = 0x16;
            rom[slot + 0x0b] = 0x88; rom[slot + 0x0d] = 0x78;
            rom[slot + 0x16] = 0x90; rom[slot + 0x17] = 0xcd;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            _entities.UpdateNativeInteractionPass(_player, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            byte[] linkScratch = new byte[4], sourceLinkScratch = new byte[4];
            int jumpScratchVisits = 0;
            if (!bombchu)
            {
                // No physical ITEMs are equipped in this route. Observe Link
                // before the later interaction cart can overwrite movement.
                var probe = new PartMovementScratchProbe(() =>
                {
                    for (int i = 0; i < 4; i++) linkScratch[i] = _runtimeState.ReadWramByte(0xcec0 + i);
                });
                _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            }
            int update = 0, previousDirections = 0;
            int Field(string name) => (int)typeof(Player).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            void Step(int count = 1, int angle = 0xff, bool useItem = false)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = (angle == 0x18 ? 0x20 : 0) | (useItem ? batched ? 2 : 1 : 0);
                int edge = held & ~previousDirections; previousDirections = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _saveData.ReadField("wPlaytimeCounter"), afterLink: () =>
                    {
                        for (int i = 0; i < 4; i++) sourceLinkScratch[i] = rom[0xcec0 + i];
                    }); edge = 0; update++;
                    string context = $"Minecart 4:78 update={update} batch={batched} Bombchu={bombchu}";
                    int[] nativeInteractions = Enumerable.Range(2, 14).Where(page =>
                        rom[0xd040 + page * 256] != 0 && rom[0xd041 + page * 256] == 0x16).ToArray();
                    int[] portInteractions = _entities.EntityAdapters<MinecartInteractionRoomEntity>()
                        .Where(actor => !actor.Finished).Select(_entities.NativeInteractionSlot).Order().ToArray();
                    FailIf(!portInteractions.SequenceEqual(nativeInteractions), context + ": independent INTERAC $16 occupancy/lifetime differs.");
                    foreach (int page in nativeInteractions)
                    foreach (int offset in new[] { 0, 1, 2, 4, 6, 8, 9, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
                        0x16, 0x17, 0x1a, 0x1e, 0x1f, 0x20, 0x21, 0x22, 0x23, 0x26, 0x27 })
                    {
                        int address = 0xd040 + page * 256 + offset;
                        FailIf(_runtimeState.ReadWramByte(address) != rom[address],
                            $"{context}: INTERAC $16 byte${address:x4}: port${_runtimeState.ReadWramByte(address):x2}, native${rom[address]:x2}.");
                    }
                    if (rom[0xd100] != 0 && rom[0xd101] == 0x0a)
                        for (int address = 0xd100; address < 0xd140; address++)
                            FailIf(_runtimeState.ReadWramByte(address) != rom[address],
                                $"{context}: D1 byte${address:x4}: port${_runtimeState.ReadWramByte(address):x2}, native${rom[address]:x2}.");
                    int[] nativeItems = Enumerable.Range(0xd7, 5).Where(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x1d).ToArray();
                    int[] portItems = _entities.EntityAdapters<MinecartCollisionRoomEntity>().Where(item => !item.Finished)
                        .Select(_entities.DynamicItemSlotOf).Order().ToArray();
                    FailIf(!portItems.SequenceEqual(nativeItems), context + ": ITEM $1d occupancy/lifetime differs.");
                    foreach (int page in nativeItems)
                        for (int offset = 0; offset < 0x40; offset++)
                        {
                            int address = (page << 8) + offset;
                            FailIf(_runtimeState.ReadWramByte(address) != rom[address],
                                $"{context}: ITEM $1d byte${address:x4}: port${_runtimeState.ReadWramByte(address):x2}, native${rom[address]:x2}.");
                        }
                    if (!bombchu && _player.MinecartJumpActive && (rom[0xcc5c] & 0x80) != 0)
                    {
                        jumpScratchVisits++;
                        FailIf(!linkScratch.SequenceEqual(sourceLinkScratch), context + ": airborne Link movement must publish source scratch before ITEM/ENEMY/PART/INTERACTION consumers.");
                    }
                    if (heartRing != 0xff) CompareHeartRingVehicle(rom, context);
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    int z = unchecked((short)rom.Word(0xd00e));
                    FailIf(_player.PrecisePosition != expected || Field("_topDownAirZFixed") != z ||
                        _player.MinecartRideActive != (rom[0xcc2c] == 0xd1) ||
                        _player.HealthQuarters != rom[0xc6aa] ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        $"{context}: fixed Link XY/Z/ride/facing differs: runtime={_player.PrecisePosition}, native={expected}, Z={Field("_topDownAirZFixed")}/{z}, inAir=${rom[0xcc5c]:x2}, native cart=${rom[0xd100]:x2}/${rom[0xd104]:x2}, interaction=${rom[slot + 4]:x2}.");
                    if (cart.Mounting && rom[slot] != 0)
                        FailIf(rom[slot + 4] != 2 || cart.PushCounter != rom[slot + 6],
                            $"{context}: push countdown/boarding state differs: runtime={cart.PushCounter}, native={rom[slot + 6]}, state=${rom[slot + 4]:x2}, Link={_player.PrecisePosition}.");
                    if (rom[0xd100] != 0 && rom[0xd104] == 1)
                        FailIf(cart.Position != new Vector2(rom[0xd10d], rom[0xd10b]) ||
                            cart.Direction != rom[0xd108] || cart.Angle != rom[0xd109],
                            context + ": native track position/direction differs.");
                    if (bombchu)
                    {
                        var parent = _entities.BombchuParent;
                        FailIf(parent.Active != (rom[0xd300] != 0) || parent.Active &&
                            (parent.Counter != rom[0xd320] || parent.Mode != rom[0xd330]), context + ": mounted throw parent differs.");
                        var item = _entities.Entities<BombchuItem>().SingleOrDefault();
                        int native = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                            .FirstOrDefault(slot => rom[slot] != 0 && rom[slot + 1] == 0x0d);
                        FailIf((item is null) != (native == 0) || _inventory.Bombchus != rom[0xc6b3], context + ": mounted child/ammo differs.");
                        if (item is not null)
                            FailIf(item.ItemState != rom[native + 4] || item.Counter2 != rom[native + 7] ||
                                item.Position != new Vector2(rom[native + 0xd], rom[native + 0xb]), context + ": mounted child movement/fuse differs.");
                    }
                    for (int address = 0xcd80; address < 0xcdc0; address++)
                        FailIf(_runtimeState.ReadWramByte(address) != rom[address],
                            $"{context}: static-cart byte ${address:x4} differs: runtime=${_runtimeState.ReadWramByte(address):x2}, native=${rom[address]:x2}.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText &&
                        (heartRing == 0xff || id != SoundId.SndGainHeart)).SequenceEqual(rom.Sounds),
                        context + ": jump/cart sound order differs.");
                    FailIf(RandomDiffers(initialRandom, rom[0xff94], rom[0xff95], rom.RandomCalls), context + ": shared RNG differs.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int approach = 0; !cart.Mounting && approach < 40; approach++) Step(1, 0x18);
                FailIf(!cart.Mounting || rom[slot + 4] != 2, "Four-update reachable push must begin native minecart boarding.");
                FailIf(_runtimeState.ReadWramByte(0xd100) != 0 || rom[0xd100] != 0,
                    "Boarding must retain its interaction before publishing a separate D1 allocation.");
                // Source objectCopyPosition writes only yh/xh/zh, retaining
                // destination low bytes and unrelated counters through birth.
                foreach (var retained in new (int Offset, int Value)[] { (0x0a,0x41), (0x0c,0x83), (0x0e,0x57), (0x35,3), (0x3d,0x9b) })
                    _runtimeState.SetWramByte(0xd100 + retained.Offset, rom[0xd100 + retained.Offset] = (byte)retained.Value);
                int health = _inventory.HealthQuarters;
                if (heartRing != 0xff) SeedHeartRingVehicle(rom, heartRing);
                for (int wait = 0; !cart.Riding && wait < 64; wait++) Step();
                FailIf(!cart.Riding || rom[0xcc2c] != 0xd1, "Minecart boarding must transfer into the native companion slot.");
                Step(4);
                if (bombchu)
                {
                    Step(useItem: true);
                    FailIf(!_entities.BombchuParent.Active || _entities.BombchuParent.Mode != 0x25,
                        $"Minecart-rider Bombchu must initialize mode$25: repeat={repeat}, parent={_entities.BombchuParent.Active}/${_entities.BombchuParent.Mode:x2}, " +
                        $"native=${rom[0xd300]:x2}/${rom[0xd330]:x2}, air=${rom[0xcc5c]:x2}, state=${rom[0xd004]:x2}, item mask=${rom[0xcc95]:x2}.");
                }
                _dialogue.ShowMessage("Cart pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6); _dialogue.Close(); rom[0xcba0] = 0;
                for (int travel = 0; !cart.Dismounting && travel < 256; travel++) Step();
                FailIf(!cart.Dismounting || rom[0xcc2c] != 0xd0,
                    "Bounded Skull rail route must stop at its native platform and dismount.");
                Step(48);
                FailIf(cart.Dismounting || _player.MinecartJumpActive || _player.MinecartRideActive ||
                    _currentRoom.IsSolid(_player.Position),
                    "Completed cart dismount must land on reachable floor before another approach.");
                if (heartRing != 0xff)
                {
                    AssertHeartRingVehicleRetained(rom, heartRing, health);
                    Step(1, 0x18); AssertHeartRingVehicleHeal(heartRing, health);
                }
                if (bombchu) { _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems(); }
            }
            FailIf(!bombchu && jumpScratchVisits < 10, "The reachable cart route must observe active boarding/dismount Link scratch beyond their creation visits.");
        }
        GD.Print($"Validated clean-US collision-reachable Skull 4:78 minecart push, jump, full fixed XY/Z, companion handoff, bounded native rail route, pause, static-cart bytes, dismount/landing, sounds/RNG and repeated split/batched gameplay; Bombchu={bombchu}.");
    }
}
