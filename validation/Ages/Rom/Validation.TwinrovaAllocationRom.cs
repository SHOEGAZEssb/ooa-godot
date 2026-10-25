using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaAllocationRom()
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0xf5);
        var boss = _entities.EntityAdapters<TwinrovaRoomEntity>().Single();
        FailIf(boss.Slot != 0 || _entities.RoomEnemyCount != 1, "Room $5:$f5 before-event $20,$03,$00 must allocate one counted random-placement enemy.");
        var blockers = new EnemyAiSlotReservation[15];
        for (int slot = 1; slot < 16; slot++)
        {
            blockers[slot - 1] = new(); _entities.RegisterEnemySlot(blockers[slot - 1], slot); _entities.AddEntity(blockers[slot - 1]);
        }
        _player.WarpTo(new(0x78, 0x78));
        var seed = _random.CaptureState();
        var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 0x78, 0x78) { HostileEnemiesEnabled = true };
        rom.InitializeLinkGameplay();
        rom[0xd080] = 1; rom[0xd081] = 3;
        rom[0xd08b] = (byte)boss.Actor.Position.Y; rom[0xd08d] = (byte)boss.Actor.Position.X;
        // Twinrova states $02..$07 are source stubs. Keep live occupied slots
        // without state-zero property loads, RNG or unrelated enemy behavior.
        for (int slot = 1; slot < 16; slot++)
        {
            int at = 0xd080 + slot * 256;
            rom[at] = 1; rom[at + 1] = 3; rom[at + 4] = 2; rom[at + 0x29] = 1;
        }
        rom[0xcc08] = 0x33; rom[0xcc0a] = 0x34; rom[0xcc0c] = 0x35; rom[0xcc0e] = 0x36;
        StepGameplayUpdates(8, Vector2.Zero, batched: true, afterUpdate: () =>
        {
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            FailIf(boss.Actor.State != 1 || rom[0xd084] != 1 || _entities.EntityAdapters<TwinrovaRoomEntity>().Count() != 1 ||
                RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls) || _random.Calls - seed.Calls != 1,
                $"Twinrova full-pool retry differs: state={boss.Actor.State}/{rom[0xd084]}, count={_entities.EntityAdapters<TwinrovaRoomEntity>().Count()}, RNG={_random.Calls-seed.Calls}/{rom.RandomCalls}, sourceSlot1={rom[0xd180]:x2}:{rom[0xd181]:x2}, portInput={_runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled):x2}.");
        });
        _entities.RetireEntity(blockers[0]);
        for (int i = 0; i < 64; i++) rom[0xd180 + i] = 0;
        StepGameplayUpdates(3, Vector2.Zero, afterUpdate: () => rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter));
        FailIf(_entities.EntityAdapters<TwinrovaRoomEntity>().Count() != 2 || boss.Actor.TwinSlot != 1 ||
            boss.Actor.State != rom[0xd084] || boss.Actor.Counter != rom[0xd086] ||
            RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
            "Twinrova must retry the checked child allocation when a live slot becomes free and retain native update timing/RNG.");
        ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(5, 0xf5, 0x80); LoadValidationRoom(5, 0xf5);
        FailIf(_entities.EntityAdapters<TwinrovaRoomEntity>().Any(), "Room $5:$f5 flag$80 must suppress its original before-event Twinrova record.");
        GD.Print("Validated Twinrova before-event placement/count, full enemy-pool retries without extra RNG, resumed child allocation and flag-$80 suppression.");
    }
}
