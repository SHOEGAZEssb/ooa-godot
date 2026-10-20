using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareSmallFairyCollectionRom()
    {
        int fixture = 0;
        foreach (int mode in Enumerable.Range(0, 6)) // body, sword, boom, hook, cancelled boom/hook.
        foreach (int ring in new[] { 0xff, (int)RingId.BlueJoy })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wLinkMaxHealth, 0x40);
            save.WriteWramByte(WramAddress.wLinkHealth, 4);
            InitializeTransientSession(save);
            int treasure = mode is 2 or 4 ? TreasureId.Boomerang : mode is 3 or 5 ? TreasureId.SwitchHook : TreasureId.Sword;
            _inventory.GiveTreasure(treasure, 1); _inventory.EquipA(treasure); _inventory.EquipB(0);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1); _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0), "Small fairy ring fixture failed.");
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _inventory.SetScriptedHealth(4);
                var seed = _random.CaptureState();
                LoadValidationRoom(3, 0x4f);
                var fairy = (SmallFairyRoomEntity)_entities.ResolvePartSlot(0)!;
                foreach (var entity in _entities.EntityAdapters<IRoomEntity>().Where(entity => !ReferenceEquals(entity, fairy)).ToArray())
                    _entities.RetireEntity(entity);
                // Actual source fairy at $45, approached from the original
                // floor to its left. No collision geometry is replaced.
                Vector2 start = new(64, 72);
                _player.WarpTo(start); _player.Face(Vector2I.Right);
                FailIf(_collision.Collides(start), "PART$28 collection must begin on room$3:$4f's original open floor.");
                var native = new SomariaRom(_saveData, seed, _currentRoom, 1, 64, 72) { HostilePartsEnabled = true };
                var frontend = SomariaPrivate<FrontendRom>(native, "_rom");
                native[0xcc05] = 2; native[0xcd00] = 1; frontend.Call(0x55b7, 0x12);
                native[0xd1c0] = 0; // Isolate one real placement, retaining its slot/position.
                native.InitializeLinkGameplay();
                int updates = 0;
                bool attached = false;
                void Step(int count = 1, bool press = false, Vector2 movement = default)
                {
                    int edge = press ? 1 : 0;
                    StepGameplayUpdates(count, movement, MenuRomActions(press ? 1 : 0), MenuRomActions(press ? 1 : 0), batched, () =>
                    {
                        native.UpdateGameplay(edge, press ? 1 : 0, CompanionMovement.AngleForInput(movement), _entities.FrameCounter);
                        edge = 0; updates++;
                        string context = $"Small fairy mode={mode}, ring=${ring:x2}, repeat={repeat}, update={updates}";
                        FailIf(fairy.Finished != (native[0xd0c0] == 0), context + ": lifetime differs.");
                        if (!fairy.Finished)
                            FailIf(fairy.State != native[0xd0c4] || fairy.Counter != native[0xd0c6] ||
                                fairy.Health != native[0xd0e9] || fairy.CollisionZ != unchecked((sbyte)native[0xd0cf]) ||
                                fairy.PrecisePosition != new Vector2(native.Word(0xd0cc) / 256f, native.Word(0xd0ca) / 256f),
                                $"{context}: state/health/XY/Z differs (runtime={fairy.State}/{fairy.Health}/{fairy.PrecisePosition}/z{fairy.CollisionZ}; native={native[0xd0c4]}/{native[0xd0e9]}/{native.Word(0xd0cc)/256f},{native.Word(0xd0ca)/256f}/z{unchecked((sbyte)native[0xd0cf])}).");
                        foreach (int address in new[] { WramAddress.wLinkHealth, WramAddress.wLinkMaxHealth,
                            WramAddress.wGashaMaturity, WramAddress.wGashaMaturity + 1, 0xc69f })
                            FailIf(_saveData.ReadWramByte(address) != native[address], $"{context}: treasure byte ${address:x4} differs.");
                        var rng = _random.CaptureState();
                        FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95], context + ": RNG differs.");
                        if (!fairy.Finished && fairy.State == 2) attached = true;
                    });
                }
                Step();
                if (mode != 0) Step(press: true);
                bool cancelled = false;
                for (int wait = 0; wait < 120 && !fairy.Finished; wait++)
                {
                    if (mode >= 4 && attached && !cancelled)
                    {
                        _entities.SwitchHook?.Cancel(); _entities.ClearPhysicalPlayerItems(); native.ClearPhysicalItems(); cancelled = true;
                    }
                    Vector2 approach = mode == 0 ? new(Math.Sign(fairy.PrecisePosition.X - _player.Position.X),
                        Math.Sign(fairy.PrecisePosition.Y - _player.Position.Y)) : Vector2.Zero;
                    Step(movement: approach);
                }
                FailIf(!fairy.Finished || mode >= 2 && !attached || mode >= 4 && !cancelled,
                    $"Small fairy mode={mode}, ring=${ring:x2}, repeat={repeat}, batch={batched}: source completion not reached; " +
                    $"finished={fairy.Finished}, attached={attached}, cancelled={cancelled}, Link={_player.Position}, fairy={fairy.PrecisePosition}.");
                Step(4);
                // Finish the carrier before repeating through actual room entry.
                Step(80);
                FailIf(_inventory.HealthQuarters != (mode >= 4 ? 4 : ring == 0xff ? 28 : 52),
                    "Small fairy must grant exactly $18/$30 health, or no treasure after carrier cancellation.");
            }
        }
    }
}
