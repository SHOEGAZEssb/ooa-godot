using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTargetCrystalPublicationRom()
    {
        foreach (var (configuration, subId) in new[] { (0, 0), (1, 4), (2, 0), (2, 11) })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl();
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1);
            _runtimeState.SetWramByte(0xcfd4, (byte)configuration); _runtimeState.SetWramByte(0xcfdf, 0);
            FailIf(!_entities.TrySpawnEnemy(0x63, subId, new(72, 64), "Target crystal publication fixture", out string error), error);
            var crystal = _entities.EntityAdapters<TargetCartCrystalRoomEntity>().Single();
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112)
                { CompanionDispatchEnabled = true, HostileEnemiesEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcfd4] = (byte)configuration; native[0xcfdf] = 0;
            const int at = 0xd080;
            native[at] = 1; native[at + 1] = 0x63; native[at + 2] = (byte)subId;
            byte[] observed = new byte[4], expected = new byte[4];
            int probes = 0, movements = 0, update = 0;
            var probe = new PartMovementScratchProbe(() =>
            { probes++; for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i); });
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                bool initialized = native[at + 4] != 0;
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, beforeParts: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; }); update++;
                bool live = native[at] != 0;
                FailIf(crystal.Finished == live || live &&
                    (SomariaPrivate<Vector2>(crystal, "_position") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                        configuration != 0 && (SomariaPrivate<int>(crystal, "_counter") != native[at + 6] ||
                            SomariaPrivate<int>(crystal, "_angle") != native[at + 9])),
                    $"ENEMY$63 config={configuration}/sub=${subId:x2} visit{update}: source movement/turn/stop differs.");
                if (initialized && native[0xcba0] == 0)
                {
                    FailIf(probes != 1 || !observed.SequenceEqual(expected), $"Target crystal config={configuration}/sub=${subId:x2} visit{update}: native movement publication differs before PART dispatch: runtime=[{string.Join(',', observed)}], source=[{string.Join(',', expected)}], probe={probes}.");
                    if (configuration != 0 && live) movements++;
                }
                probes = 0; Seed();
            });
            Seed(); Step(10);
            _dialogue.ShowMessage("Target crystal hold.", _player.Position.Y); native[0xcba0] = 1;
            Step(3); _dialogue.Close(); native[0xcba0] = 0;
            Step(94); // Exercise both $20 and subsequent $40 counter reversals.
            _runtimeState.SetWramByte(0xcfdf, 1); native[0xcfdf] = 1; Step(3);
            FailIf(configuration != 0 && movements < 100 || crystal.Finished != (subId < 5),
                "Target crystal fixture must exercise both turns and the source subid<$05 stop distinction.");
            _entities.Clear();
        }
    }

    private void ValidateTargetDebrisPublicationRom()
    {
        foreach (int direction in new[] { 0, 1, 2, 3 })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl(); _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1);
            _entities.Spawn<NpcCharacter>(new TargetCartDebrisSpawn(new GoronCaveDatabase().Effect(0x92, 3), new(72, 64), direction));
            var debris = _entities.EntityAdapters<TargetCartDebrisRoomEntity>().Single();
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1;
            int at = 0xd040 + _entities.InteractionSlot(debris.Node) * 256;
            native[at] = 1; native[at + 1] = 0x92; native[at + 2] = 3; native[at + 9] = (byte)direction;
            native.Word(at + 10, 0x4000); native.Word(at + 12, 0x4800);
            int movements = 0, update = 0; byte[] expected = new byte[4];
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                int beforeX = native.Word(at + 12), beforeY = native.Word(at + 10);
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; }); update++;
                bool live = native[at] != 0;
                if (live && (beforeX != native.Word(at + 12) || beforeY != native.Word(at + 10))) movements++;
                FailIf(debris.Finished == live || live && debris.Node.Position != new Vector2(native[at + 13], native[at + 11]) ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]),
                    $"Target debris $92:$03 dir={direction} visit{update}: native motion/animation retirement/publication differs: live={!debris.Finished}/{live}, XY={debris.Node.Position}/{native[at + 13]},{native[at + 11]}, scratch=[{string.Join(',', Enumerable.Range(0,4).Select(i => _runtimeState.ReadWramByte(0xcec0+i)))}]/[{string.Join(',', expected)}].");
                for (int i = 0; i < 4; i++) { native[0xcec0 + i] = (byte)(0xa5 + i); _runtimeState.SetWramByte(0xcec0 + i, (byte)(0xa5 + i)); }
            });
            for (int i = 0; i < 4; i++) { native[0xcec0 + i] = (byte)(0xa5 + i); _runtimeState.SetWramByte(0xcec0 + i, (byte)(0xa5 + i)); }
            Step(4); _dialogue.ShowMessage("Target debris continues.", _player.Position.Y); native[0xcba0] = 1;
            Step(3); _dialogue.Close(); native[0xcba0] = 0;
            for (int limit = 0; !debris.Finished && limit < 100; limit += 2) Step(2);
            Step(2); FailIf(!debris.Finished || movements < 8, "Target debris must complete its source animation with real motion observations.");
            _entities.Clear();
        }
    }
}
