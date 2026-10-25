using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ComparePickupPartAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x2b);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(16, 160)); _player.BeginCutsceneControl();
            var parent = _entities.EntityAdapters<HeadThwompBossRoomEntity>().Single();
            var boss = (HeadThwompBoss)parent.Node; F1Retain(parent);
            F1Set(parent, "_initialized", true); F1Set(boss, "_initialized", true);
            F1Set(boss, "_state", HeadThwompState.Red); F1Set(boss, "_phase", 0); boss.Position = new(80, 72); boss.Health = 4;
            var native = F1Native(0x79, 0, 0x10, boss.Position); native[0xd0a9] = 4; F1Fill(free, native);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); F1Enemy(native); F1Birth(native);
            FailIf(boss.Health != 3 || native[0xd0a9] != 3 || _entities.Entities<ItemDropEffect>().Count != free || F1Count(native, 1) != free,
                "Rejected Head Thwomp hearts must preserve the nonterminal red reaction and health decrement.");
            if (free == 0)
            {
                _entities.RetireEntity(_entities.ResolvePartSlot(0)!);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_entities.Entities<ItemDropEffect>().Count != 0, "The red reaction must not retry a rejected optional heart.");
            }
            _entities.Clear();

            ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(5, 0x78, 0xa0); LoadValidationRoom(5, 0x78);
            var bush = _entities.Entities<RespawnableBushRoomEntity>().First(); F1Retain(bush);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(bush.Position + Vector2.Up * 32); _player.BeginCutsceneControl();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            var prediction = new OracleRandom(); prediction.RestoreState(_random.CaptureState());
            while (true) { var seed = prediction.CaptureState(); if ((prediction.Next().Value & 1) != 0) { _random.RestoreState(seed); break; } }
            var bushNative = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            int at = 0xd0c0 + F1Parts[bush] * 256;
            bushNative[at] = 1; bushNative[at + 1] = 0x0f; bushNative[at + 2] = (byte)bush.DropSubId;
            bushNative[at + 4] = 1; bushNative[at + 0x29] = 1; bushNative[at + 0x2a] = 0x80;
            bushNative[at + 11] = (byte)bush.Position.Y; bushNative[at + 13] = (byte)bush.Position.X;
            F1Fill(free, bushNative);
            // A declared published hit isolates the next eligible PART dispatch;
            // ValidatePlacedRespawnableBushRom separately approaches and repeats
            // sword contact over the original room geometry.
            F1Set(bush, "_hitPending", true);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            // Keep occupied slots initialized and mask them, but permit the
            // hit bush via its enabled high bit, as the source masked walk does.
            bushNative[at] = 0x81;
            F1Birth(bushNative);
            FailIf(_entities.Entities<ItemDropEffect>().Count != free || F1Count(bushNative, 1) != free ||
                bush.State != RespawnableBushState.CutDelay || _currentRoom.GetMetatile(bush.Position) != bushNative[0xcf00 + bush.PackedPosition],
                "A cut bush must keep its tile/regrowth effects when its optional checked pickup fails.");
            _entities.Clear();

            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x60); _entities.Clear();
            var debugNative = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 24); F1Fill(free, debugNative);
            bool spawned = _entities.TrySpawnDebugItemDrop(ItemDropDatabase.OneRupee, new(88, 72), out string error);
            FailIf(spawned != (free == 1) || _entities.Entities<ItemDropEffect>().Count != free ||
                (!spawned && !error.Contains("PART")),
                "Debug pickup creation must report a full shared PART pool without retaining an unpooled node.");
            _entities.Clear();
        }
    }
}
