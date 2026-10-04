using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMinecartApproachRom() => ValidateMinecartApproachControlRom(false);
    private void ValidateMinecartSwordMountRom() => ValidateMinecartApproachControlRom(true);

    private void ValidateMinecartApproachControlRom(bool sword)
    {
        int cases = 0, airborneSwordUpdates = 0, dismountSwordUpdates = 0;
        Vector2I[] directions = [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
        Vector2I[] inputs = [Vector2I.Up, new(1, -1), Vector2I.Right, new(1, 1),
            Vector2I.Down, new(-1, 1), Vector2I.Left, new(-1, -1)];
        int hostCase1 = 0;
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int angle in Enumerable.Range(0, 8).Select(index => index * 4))
        foreach (int buttons in sword ? new[] { 1, 2 } : angle % 8 == 0 ? new[] { 0, 1, 2, 3 } : new[] { 0 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x00); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (sword)
            {
                _inventory.GiveTreasure(TreasureId.Sword, 1);
                if (buttons == 1) _inventory.EquipA(TreasureId.Sword);
                else _inventory.EquipB(TreasureId.Sword);
            }
            Vector2 center = new(88, 72);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            _currentRoom.SetPositionTileAndCollision(center, (byte)(direction % 2 == 0 ? 0x5e : 0x5d), 0, 0);
            _currentRoom.SetPositionTileAndCollision(center + (Vector2)directions[direction] * 16,
                (byte)(direction % 2 == 0 ? 0x5e : 0x5d), 0, 0);
            _currentRoom.SetPositionTileAndCollision(center - (Vector2)directions[direction] * 16, 0x5f, 0, 0);
            MinecartRuntimeState.Reset(_runtimeState, [new MinecartStaticRecord(0, 0x00, 72, 88, "ROM approach fixture")]);
            var cart = new MinecartRoomEntity(new ActiveMinecart(0, 0x00, 72, 88, -1, false),
                _currentRoom, new DungeonInteractionDatabase(), _runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart"), _sound.PlaySound);
            _entities.AddEntity(cart);
            Vector2 start = center - (Vector2)inputs[angle / 4] * 24 + new Vector2(0.25f, 0.5f);
            _player.WarpTo(start); _player.Face(directions[angle / 8]);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, angle / 8, (int)start.X, (int)start.Y)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            const int slot = 0xd240;
            for (int address = 0xcd80; address < 0xcdc0; address++) rom[address] = _runtimeState.ReadWramByte(address);
            rom[slot] = 1; rom[slot + 1] = 0x16;
            rom[slot + 0x0b] = 72; rom[slot + 0x0d] = 88;
            rom[slot + 0x16] = 0x80; rom[slot + 0x17] = 0xcd;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousHeld = 0;
            int Z() => (int)typeof(Player).GetField("_topDownAirZFixed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            void Step(int count = 1, int movementAngle = 0xff, int itemButtons = 0)
            {
                Vector2 movement = movementAngle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(movementAngle);
                int held = itemButtons | (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int edge = held & ~previousHeld; previousHeld = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, movementAngle, _entities.FrameCounter - 1); edge = 0; update++;
                    string context = $"Minecart approach rail={direction}, angle=${angle:x2}, buttons=${buttons:x2}, sword={sword}, update={update}, batch={batched}";
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(_player.PrecisePosition != expected || Z() != unchecked((short)rom.Word(0xd00e)) ||
                        _player.MinecartRideActive != (rom[0xcc2c] == 0xd1),
                        context + $": fixed Link XY/Z/ride differs: runtime={_player.PrecisePosition}, native={expected}, Z={Z()}/{unchecked((short)rom.Word(0xd00e))}, interaction=${rom[slot + 4]:x2}.");
                    if (rom[slot] != 0 && rom[slot + 4] == 1)
                        FailIf(cart.PushCounter != rom[slot + 6] || cart.Mounting,
                            context + $": centered push gate differs: runtime counter={cart.PushCounter}, native={rom[slot + 6]}, mounting={cart.Mounting}, eligible={_player.IsAttemptingMinecartPush}, passNPC={_player.PassesNpcs}, XY={_player.PrecisePosition}, native radii={rom[0xd026]}/{rom[0xd027]}/{rom[slot + 0x26]}/{rom[slot + 0x27]}, air=${rom[0xcc5c]:x2}.");
                    if (rom[slot] != 0 && rom[slot + 4] == 2)
                        FailIf(!cart.Mounting, context + ": native eligible approach must start boarding on this update.");
                    if (rom[0xd100] != 0 && rom[0xd104] == 1)
                        FailIf(cart.Position != new Vector2(rom[0xd10d], rom[0xd10b]) || cart.Direction != rom[0xd108],
                            context + ": cart position/direction differs.");
                    if (sword)
                    {
                        SwordActionState expectedSword = rom[0xd200] == 0 ? SwordActionState.None : rom[0xd204] switch
                        {
                            1 => SwordActionState.Swing, 2 => SwordActionState.Held,
                            3 => SwordActionState.Charged, 4 => SwordActionState.Spin,
                            5 or 6 => SwordActionState.Poke,
                            _ => throw new System.InvalidOperationException(context + $": unexpected Sword parent state ${rom[0xd204]:x2}.")
                        };
                        FailIf(_player.SwordState != expectedSword,
                            context + $": Sword parent differs across boarding: runtime={_player.SwordState}, native=${rom[0xd200]:x2}/${rom[0xd204]:x2}.");
                        bool collision = (rom[0xd624] & 0x80) != 0;
                        Rect2 hitbox = _player.GetSwordHitbox();
                        FailIf((hitbox.Size != Vector2.Zero) != collision, context + ": Sword child collision eligibility differs.");
                        if (collision)
                        {
                            Rect2 expectedHitbox = new(new(rom[0xd60d] - rom[0xd627], rom[0xd60b] - rom[0xd626]),
                                new(2 * rom[0xd627], 2 * rom[0xd626]));
                            FailIf(hitbox != expectedHitbox || _player.SwordDamage != -unchecked((sbyte)rom[0xd628]),
                                context + $": Sword child geometry/damage differs: runtime={hitbox}, native={expectedHitbox}, arc={_player.SwordArcIndex}/${rom[0xd630]:x2}, frame={_player.SwordStateFrame}, raised={rom[0xcc69]}, ride={cart.Riding}, dismount={cart.Dismounting}.");
                            FailIf(_player.MeleeItemZ != unchecked((sbyte)rom[0xd60f]) ||
                                SwordCollision.Type(_player.SwordCollisionState, 1) != (rom[0xd624] & 0x7f),
                                context + ": Sword child Z/collision type differs.");
                        }
                        if (cart.Mounting && rom[0xd200] != 0 && Z() < 0) airborneSwordUpdates++;
                        if (cart.Dismounting && rom[0xd200] != 0 && Z() < 0) dismountSwordUpdates++;
                    }
                    for (int address = 0xcd80; address < 0xcdc0; address++)
                        FailIf(_runtimeState.ReadWramByte(address) != rom[address], context + $": static byte ${address:x4} differs.");
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds), context + ": sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            if (sword)
            {
                for (int approach = 0; cart.PushCounter == 4 && approach < 64; approach++) Step(1, angle);
                FailIf(cart.PushCounter != 3 || cart.Mounting, "Sword boarding fixture must reach the cart through its collision geometry before swinging.");
                Step(1, angle, buttons);
            }
            else if (buttons != 0)
            {
                Step(24, angle, buttons);
                FailIf(cart.Mounting || cart.PushCounter != 4 || rom[slot + 4] != 1,
                    "Held A/B must reset the native centered minecart push even with no item equipped.");
            }
            for (int approach = 0; !cart.Mounting && approach < 64; approach++) Step(1, angle);
            FailIf(!cart.Mounting, "A reachable cardinal or diagonal approach must begin native minecart boarding.");
            if (sword)
            {
                // Air bit 7 skips new A/B allocations but existing parents
                // still run. Exercise both a fresh edge and its release.
                Step(3, 0xff, buttons);
                _dialogue.ShowMessage("Boarding Sword pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(3);
            }
            for (int wait = 0; !cart.Riding && wait < 64; wait++) Step();
            FailIf(!cart.Riding, "Eligible boarding must initialize the native ridden cart.");
            if (sword)
            {
                Step(25);
                FailIf(!cart.Riding, "Sword dismount fixture must swing before reaching the platform.");
                Step(1, 0xff, buttons);
            }
            for (int wait = 0; !cart.Dismounting && wait < 64; wait++) Step();
            FailIf(!cart.Dismounting, "Bounded rail must reverse and stop at the adjacent platform.");
            Step(48);
            for (int approach = 0; !cart.Mounting && approach < 64; approach++) Step(1, direction * 8);
            FailIf(!cart.Mounting, "Finished boarding and dismount must leave a cart reachable for a second approach.");
            for (int wait = 0; !cart.Riding && wait < 64; wait++) Step();
            FailIf(!cart.Riding, "Repeated reachable approach must initialize the ridden cart again.");
            cases++;
        }
        FailIf(sword && airborneSwordUpdates == 0, "Sword boarding comparison must execute an existing parent during the actual airborne handoff.");
        FailIf(sword && dismountSwordUpdates == 0, "Sword dismount comparison must execute an existing parent during the actual airborne handoff.");
        GD.Print(sword
            ? $"Validated {cases} clean-US A/B Sword/minecart handoffs through reachable approaches, release, airborne allocation/parent gates, dialogue pause, child XY/Z/arc/damage, native cart-swing timing retained through dismount, companion initialization, repeat, static bytes and sounds/RNG in split/batched gameplay."
            : $"Validated {cases} clean-US minecart approaches: four platform orientations, eight movement angles, held A/B/both rejection and release, full fixed XY/Z, push counters, companion/static handoffs, bounded dismount, repeat, sounds/RNG and split/batched gameplay.");
    }
}
