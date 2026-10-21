using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFireProjectileTargetsRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33); _entities.Clear();
            // Bounded source-handler comparison: ordinary clear floor and
            // declared projectile births, with the real Satchel/ITEM phases.
            FillValidationRoom(0xa0, width: 10, height: 8);
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.ScentSeeds, 0x20);
            _inventory.SelectSatchelSeeds(1);
            _inventory.EquipA(TreasureId.SeedSatchel); _inventory.EquipB(0);
            _player.WarpTo(new(80, 80)); _player.Face(Vector2I.Right);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 80, 80)
                { HostilePartsEnabled = true };
            native.InitializeLinkGameplay();
            byte[] scratch = new byte[4], sourceScratch = new byte[4];
            _entities.AddEntity(new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) scratch[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            }));
            ZoraFireProjectile[] shots = [];
            int update = 0, flightScratchVisits = 0;
            void Step(int count = 1, bool press = false)
            {
                int edge = press ? 1 : 0;
                bool FlightActive() => Enumerable.Range(0, 2).Any(index => native[0xd0c0 + index * 256] != 0 && native[0xd0c4 + index * 256] == 2);
                bool flightWasActive = FlightActive();
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(edge), MenuRomActions(edge), batched, () =>
                {
                    native.UpdateGameplay(edge, press ? 1 : 0, 0xff, _entities.FrameCounter, beforeInteractions: () =>
                    {
                        for (int i = 0; i < 4; i++) sourceScratch[i] = native[0xcec0 + i];
                    }); edge = 0;
                    update++;
                    if (flightWasActive)
                    {
                        flightScratchVisits++;
                        FailIf(!scratch.SequenceEqual(sourceScratch),
                            "PART$19/$31 flight must publish source velocity after ITEM writers and before INTERACTION consumers.");
                    }
                    flightWasActive = FlightActive();
                    for (int index = 0; index < shots.Length; index++)
                    {
                        int at = 0xd0c0 + index * 0x100;
                        var shot = shots[index];
                        FailIf(shot.Finished != (native[at] == 0) || native[at] != 0 &&
                            (shot.State != native[at + 4] || shot.Counter != native[at + 6] ||
                             shot.Angle != native[at + 9] || shot.Position != new Vector2(native[at + 13], native[at + 11])),
                            $"PART ${shot.PartId:x2}:${shot.SubId:x2} target/flight differs at update {update}, batch={batched}: " +
                            $"runtime state={shot.State}, counter={shot.Counter}, angle=${shot.Angle:x2}, XY={shot.Position}; " +
                            $"native=${native[at + 4]:x2}/${native[at + 6]:x2}/${native[at + 9]:x2}/({native[at + 13]},{native[at + 11]}).");
                    }
                });
            }
            Step(1, press: true);
            for (int wait = 0; _entities.ActiveScentSeedTarget() is null && wait < 50; wait++) Step();
            FailIf(_entities.ActiveScentSeedTarget() is null || native[0xccd9] == 0,
                $"Actual Satchel input must publish a distinct Scent target through ITEM$21 before comparing fire branches: " +
                $"runtime={_entities.ActiveScentSeedTarget()}, Link={_player.Position}, native active=${native[0xccd9]:x2}, target=({native[0xffb3]},{native[0xffb2]}).");
            for (int repeat = 0; repeat < 3; repeat++)
            {
                if (repeat == 2)
                {
                    for (int wait = 0; _entities.ActiveScentSeedTarget() is not null && wait < 400; wait++) Step();
                    FailIf(_entities.ActiveScentSeedTarget() is not null || native[0xccd9] != 0,
                        "Both Scent publications must expire before the no-Scent repeat.");
                }
                // Both IDs share partCode19/31; subid bit0 selects hFFB2/3
                // (Scent-or-Link) instead of hEnemyTargetY/X (always Link).
                shots = [
                    _entities.Spawn<ZoraFireProjectile>(new ZoraFireSpawn(new(64, 32), 0x19, 1)),
                    _entities.Spawn<ZoraFireProjectile>(new ZoraFireSpawn(new(64, 32), 0x31, 0))
                ];
                for (int index = 0; index < shots.Length; index++)
                {
                    int at = 0xd0c0 + index * 0x100;
                    native[at] = 1; native[at + 1] = (byte)(index == 0 ? 0x19 : 0x31);
                    native[at + 2] = (byte)(index == 0 ? 1 : 0);
                    native[at + 11] = 32; native[at + 13] = 64;
                }
                Step(1); Step(7);
                FailIf(shots.Any(shot => shot.State != 1 || shot.Counter != 1),
                    "PART $19/$31 must retain the complete eight-update aim delay after state zero.");
                if (repeat == 1)
                {
                    _dialogue.ShowGameplayMessage("Fire aim pause", 100); native[0xcba0] = 1;
                    Step(3);
                    _dialogue.Close(); native[0xcba0] = 0;
                }
                Step(1);
                FailIf((shots[0].Angle == shots[1].Angle) != (repeat == 2),
                    "Scent must affect odd-subid fire only; both source branches must return to Link after expiry.");
                Step(6);
                // Explicit room-cancellation boundary: retire both physical
                // slots and clear their bytes before the next birth/reuse.
                foreach (var shot in shots) _entities.RetireEntity(_entities.EntityAdapters<IRoomEntity>().Single(entity => entity.Node == shot));
                shots = [];
                for (int index = 0; index < 2; index++)
                for (int offset = 0; offset < 0x40; offset++) native[0xd0c0 + index * 0x100 + offset] = 0;
                Step();
                FailIf(_entities.Entities<ZoraFireProjectile>().Count != 0,
                    "Cancelled fire must not survive to reuse its PART slot or target angle.");
            }
            FailIf(flightScratchVisits != 18, "All three six-update flight segments must compare shared movement publication.");
        }
        CompareFireProjectileRingIdentityRom();
        GD.Print("Validated clean-US PART$19:$01/PART$31:$00 targeting through actual Satchel/Scent item phases: aim delay, active/expired Scent, text pause, flight, cancellation/repeat and split/batched gameplay.");
    }

    private void CompareFireProjectileRingIdentityRom()
    {
        foreach (int id in new[] { 0x19, 0x31 })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug((int)RingId.BlueHoly);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, (int)RingId.BlueHoly) || !_inventory.EquipRingAt(0),
                "Fire collision comparison must equip Blue Holy Ring before contact.");
            _inventory.EquipA(0); _inventory.EquipB(0);
            FillValidationRoom(0x2c, width: 10, height: 8);
            _player.WarpTo(new(80, 64)); _player.Face(Vector2I.Right);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 80, 64)
                { HostilePartsEnabled = true };
            native.InitializeLinkGameplay();
            ZoraFireProjectile? shot = null;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                FailIf(_player.HealthQuarters != native[0xc6aa] ||
                    (_player.PendingContactDamageRaw & 0xff) != native[0xd025] ||
                    _player.NativeContactSignal != (native[0xd02a] != 0) ||
                    shot is not null && shot.Finished != (native[0xd0c0] == 0),
                    $"PART ${id:x2} Blue Holy contact/pending damage/deletion differs, batch={batched}.");
            });
            Step();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Vector2 start = _player.Position + Vector2.Right * 6;
                shot = _entities.Spawn<ZoraFireProjectile>(new ZoraFireSpawn(start, id));
                native[0xd0c0] = 1; native[0xd0c1] = (byte)id;
                native[0xd0cb] = (byte)start.Y; native[0xd0cd] = (byte)start.X;
                int health = _player.HealthQuarters;
                Step(1);
                FailIf((_player.PendingContactDamageRaw != 0) != (id == 0x31),
                    "Blue Holy Ring must suppress PART $19:$01 mode $87, while PART $31:$00 mode $86 still queues damage.");
                Step(2);
                FailIf(!shot.Finished || (_player.HealthQuarters < health) != (id == 0x31),
                    "Fire must finish through its native deferred deletion; only PART $31 damages Blue Holy Link.");
                shot = null;
                Step(40);
            }
        }
    }
}
