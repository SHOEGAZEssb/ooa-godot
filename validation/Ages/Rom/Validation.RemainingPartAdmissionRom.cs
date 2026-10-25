using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private const BindingFlags F1Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void F1Set(object owner, string field, object value) =>
        owner.GetType().GetField(field, F1Hidden)!.SetValue(owner, value);
    private Dictionary<IRoomEntity, int> F1Parts => SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_partSlots");
    private void F1Retain(IRoomEntity parent)
    {
        foreach (var actor in _entities.EntityAdapters<IRoomEntity>().Where(actor => !ReferenceEquals(actor, parent)).ToArray())
            _entities.RetireEntity(actor);
    }
    private void F1Fill(int free, SomariaRom native, Action? observePart = null)
    {
        // Declared no-op occupied slots are high, avoiding the separate source
        // checkBPartSlotsAvailable out-of-pool scan when its last slot is free.
        foreach (int slot in Enumerable.Range(0, 16).Where(slot => !F1Parts.Values.Contains(slot)).Skip(free).ToArray())
        {
            var actor = new ItemPhaseValidationEntity(observePart ?? (() => { }));
            F1Parts.Add(actor, slot); _entities.AddEntity(actor);
            int at = 0xd0c0 + slot * 256;
            native[at] = 1; native[at + 1] = 0x7f; native[at + 4] = 1;
        }
    }
    private SomariaRom F1Native(int id, int sub, int state, Vector2 point)
    {
        var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
        native[0xd080] = 1; native[0xd081] = (byte)id; native[0xd082] = (byte)sub; native[0xd084] = (byte)state;
        native[0xd089] = 8; native[0xd08b] = (byte)point.Y; native[0xd08d] = (byte)point.X;
        native[0xd0a9] = 8; native[0xd0a0] = 0x7f;
        native[0xffae] = 0x80; native[0xffaf] = 0xd0;
        byte[] caller = [0x16, 0xd0, 0xcd, 0x07, 0x2f, 0xc9];
        for (int i = 0; i < caller.Length; i++) native[0xc100 + i] = caller[i];
        return native;
    }
    private void F1Enemy(SomariaRom native)
    {
        // Other source helpers use the same scratch caller (for example setTile).
        byte[] caller = [0x16, 0xd0, 0xcd, 0x07, 0x2f, 0xc9];
        for (int i = 0; i < caller.Length; i++) native[0xc100 + i] = caller[i];
        native[0xcc00] = (byte)_entities.FrameCounter;
        native[0xffae] = 0x80; native[0xffaf] = 0xd0;
        SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 0x0d);
    }
    private void F1Birth(SomariaRom native)
    {
        // State0 is admitted by the original masked walk while initialized
        // filler slots are held. This is a declared no-op filler boundary.
        native[0xcc8a] = 8; native.AdvanceParts(_entities.FrameCounter); native[0xcc8a] = 0;
    }
    private static int F1Count(SomariaRom native, int id) => Enumerable.Range(0, 16)
        .Count(slot => native[0xd0c0 + slot * 256] != 0 && native[0xd0c1 + slot * 256] == id);

    private void ValidateRemainingPartAdmissionRom()
    {
        CompareHeadThwompAdmissionRom();
        ComparePumpkinAdmissionRom();
        CompareLegacyBossDeathAdmissionRom();
        CompareShadowBatchAdmissionRom();
        CompareBossShadowAdmissionRom();
        CompareOptionalPartAdmissionRom();
        CompareFountainHeartAdmissionRom();
        CompareMaplePartAdmissionRom();
        ComparePickupPartAdmissionRom();
        ReinitializeGameplayForValidation();
        GD.Print("Validated remaining F1 checked PART callers, counted/partial/atomic batches, retry boundaries and native side effects.");
    }

    private void CompareHeadThwompAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (string mode in new[] { "bomb", "fireball", "circular", "boulder" })
        foreach (int free in mode == "bomb" ? new[] { 0, 1, 2 } : new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x2b);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(16, 160)); _player.BeginCutsceneControl();
            var parent = _entities.EntityAdapters<HeadThwompBossRoomEntity>().Single();
            var boss = (HeadThwompBoss)parent.Node; F1Retain(parent);
            F1Set(boss, "_initialized", true); F1Set(parent, "_initialized", true); boss.Position = new(80, 72);
            F1Set(_entities, "_enemyFrameCounter", 1);
            int state = mode switch { "bomb" => 9, "fireball" => 0x0d, "circular" => 0x0e, _ => 0x0f };
            F1Set(boss, "_state", mode switch { "bomb" => HeadThwompState.Spinning, "fireball" => HeadThwompState.Green,
                "circular" => HeadThwompState.Blue, _ => HeadThwompState.Purple });
            int counter = mode switch { "bomb" => 100, "fireball" => 65, "circular" => 1, _ => 113 };
            F1Set(boss, "_phase", mode == "boulder" ? 2 : 1); F1Set(boss, "_counter", counter);
            F1Set(boss, "_spinDelay", 2); F1Set(boss, "_projectileCounter", 81);
            if (mode == "bomb")
            {
                var prediction = new OracleRandom(); prediction.RestoreState(_random.CaptureState());
                while (true) { var seed = prediction.CaptureState(); if ((prediction.Next().Value & 7) == 0) { _random.RestoreState(seed); break; } }
            }
            var native = F1Native(0x79, 0, state, boss.Position); F1Fill(free, native);
            native[0xd085] = (byte)(mode == "boulder" ? 2 : 1); native[0xd086] = (byte)counter;
            native[0xd0b3] = 2; native[0xd0b4] = 81; native[0xcc00] = 2;
            var before = _random.CaptureState(); _sound.ClearPlayRequestAudit();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); F1Enemy(native); F1Birth(native);
            int id = mode switch { "bomb" => 0x40, "fireball" => 0x39, "circular" => 0x3c, _ => 0x3b };
            int admitted = free >= (mode == "bomb" ? 2 : 1) ? 1 : 0;
            int runtime = mode == "bomb" ? _entities.EntityAdapters<HeadThwompBombDropRoomEntity>().Count() :
                mode == "boulder" ? _entities.Entities<HeadThwompBoulder>().Count : _entities.Entities<HeadThwompProjectile>().Count;
            FailIf(runtime != admitted || F1Count(native, id) != admitted || F1Parts.Count != 16 - free + admitted * (mode == "bomb" ? 2 : 1),
                $"Head Thwomp {mode} must preserve native checked admission and physical footprint, free={free}.");
            FailIf(_random.Calls - before.Calls != native.RandomCalls, $"Rejected {mode} must not consume child initialization RNG.");
            int cue = mode == "circular" ? SoundId.SndBeam : SoundId.SndFallInHole;
            FailIf(_sound.PlayRequestsFor(cue) != native.Sounds.Count(sound => sound == cue), "Head Thwomp attack cue must require successful admission.");
            if (mode == "bomb")
                FailIf(F1Count(native, 1) != admitted || _entities.Entities<ItemDropEffect>().Count != admitted,
                    "Head Thwomp's Bomb pickup and physics helper must occupy separate live PART slots.");
            F1Set(boss, "_state", HeadThwompState.WaitingForLink);
            void ComparePair()
            {
                if (mode != "bomb" || admitted == 0) return;
                var fillers = F1Parts.Where(pair => pair.Key is ItemPhaseValidationEntity).Select(pair => pair.Value).ToArray();
                foreach (int slot in fillers) native[0xd0c0 + slot * 256] = 0;
                native.AdvanceParts(_entities.FrameCounter);
                foreach (int slot in fillers) native[0xd0c0 + slot * 256] = 1;
                FailIf(_entities.EntityAdapters<HeadThwompBombDropRoomEntity>().Count() != F1Count(native, 0x40) ||
                    _entities.Entities<ItemDropEffect>().Count != F1Count(native, 1),
                    "Bomb helper landing/deletion must release its own slot while leaving its pickup independently live.");
                var pickup = _entities.Entities<ItemDropEffect>().Single();
                FailIf(pickup.Position != new Vector2(native[0xd0cd], native[0xd0cb]),
                    $"Bomb helper must copy its source high coordinates into the independent pickup: runtime={pickup.Position}, native={native[0xd0cd]},{native[0xd0cb]}.");
            }
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: ComparePair);
            FailIf(mode == "bomb" && admitted == 1 && _entities.Entities<ItemDropEffect>().Single().ElapsedFrames != 4,
                "The Bomb pickup must receive one shared PART dispatch per update independently of its helper.");
            if (mode == "bomb" && admitted == 1)
            {
                for (int update = 0; update < 120 && _entities.EntityAdapters<HeadThwompBombDropRoomEntity>().Any(); update++)
                    StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: ComparePair);
                FailIf(_entities.EntityAdapters<HeadThwompBombDropRoomEntity>().Any() || _entities.Entities<ItemDropEffect>().Count != 1,
                    "The Bomb physics helper must retire on landing without retiring or retaining an extra slot for its ordinary pickup.");
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: ComparePair);
            }
            _entities.Clear(); FailIf(F1Parts.Count != 0, "Cancellation must free both Bomb pair allocations.");
        }
    }

    private void ComparePumpkinAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1, 2, 3 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x13);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(16, 112)); _player.BeginCutsceneControl();
            var parent = _entities.EntityAdapters<PumpkinHeadBossRoomEntity>().Single(); var boss = (PumpkinHeadBoss)parent.Node;
            F1Retain(parent); F1Set(boss, "_initialized", true); F1Set(parent, "_initialized", true);
            F1Set(boss, "_state", BossState.Firing); F1Set(boss, "_counter", 37); F1Set(boss, "_angle", 8);
            F1Set(boss, "_headPosition", new Vector2(80, 72)); F1Set(boss, "_headZ", 0); F1Set(boss, "_bodyZFixed", 0);
            var native = F1Native(0x78, 3, 0x0c, new(80, 72)); native[0xd086] = 1;
            int observed = 0;
            F1Fill(free, native, () =>
            {
                observed++;
                FailIf(_sound.PlayRequestsFor(SoundId.SndVeranFairyAttack) != (free == 0 ? 0 : 1),
                    "Pumpkin Head's ENEMY firing cue must precede every physical PART observer.");
            });
            _sound.ClearPlayRequestAudit(); StepGameplayUpdates(1, Vector2.Zero, batched: batched); F1Enemy(native);
            FailIf(F1Count(native, 0x42) != (free == 0 ? 0 : 1) || observed != 16 - free,
                "Pumpkin Head must admit only the checked primary in its parent dispatch.");
            // The native ENEMY handler precedes PART; the primary and its
            // later siblings therefore initialize in this same gameplay update.
            F1Birth(native);
            F1Set(boss, "_projectileFired", true); F1Set(boss, "_counter", 10000);
            var shots = _entities.Entities<PumpkinHeadProjectile>();
            FailIf(shots.Count != free || F1Count(native, 0x42) != free || shots.Any(shot => shot.Delay != 8),
                "Pumpkin PART$42 must stop its ordered $02/$01 sibling attempts at the first allocation failure.");
            for (int slot = 0; slot < free; slot++)
            {
                var shot = (PumpkinHeadProjectile)_entities.ResolvePartSlot(slot)!.Node;
                FailIf(shot.Angle != native[0xd0c9 + slot * 256] || native[0xd0c4 + slot * 256] != 1 || native[0xd0c6 + slot * 256] != 8,
                    $"Pumpkin siblings must initialize on birth in source slot/angle order: free={free}, slot={slot}, runtime=${shot.Angle:x2}, native=${native[0xd0c9 + slot * 256]:x2}, nativeState={native[0xd0c4 + slot * 256]}.");
            }
            FailIf(_sound.PlayRequestsFor(SoundId.SndVeranFairyAttack) != native.Sounds.Count(sound => sound == SoundId.SndVeranFairyAttack),
                "Failed Pumpkin primary allocation must skip the attack sound.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                // Original initialized projectile updates, with the declared
                // inert filler reservations excluded from native execution.
                var held = Enumerable.Range(0, 16).Select(slot => 0xd0c0 + slot * 256)
                    .Where(at => native[at + 1] == 0x7f).Select(at => (At: at, Enabled: native[at])).ToArray();
                foreach (var row in held) native[row.At] = 0;
                native.AdvanceParts(_entities.FrameCounter);
                foreach (var row in held) native[row.At] = row.Enabled;
                foreach (var shot in shots)
                {
                    int slot = F1Parts.Single(pair => pair.Key.Node == shot).Value;
                    FailIf(shot.Delay != native[0xd0c6 + slot * 256], "Pumpkin's post-birth counter must advance on the same native updates.");
                }
            });
            FailIf(shots.Any(shot => shot.Delay != 6), "Every admitted Pumpkin projectile must continue advancing without unpooled siblings.");
            foreach (var shot in shots)
                _entities.RetireEntity(F1Parts.Single(pair => pair.Key.Node == shot).Key);
            // partDelete clears all $40 bytes; merely clearing enabled would
            // leave a stale state1 and suppress the next primary's setup.
            for (int slot = 0; slot < free; slot++)
                for (int offset = 0; offset < 0x40; offset++) native[0xd0c0 + slot * 256 + offset] = 0;
            F1Set(boss, "_projectileFired", false); F1Set(boss, "_counter", 37);
            native[0xd084] = 0x0c; native[0xd086] = 1; observed = 0;
            _sound.ClearPlayRequestAudit();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); F1Enemy(native); F1Birth(native);
            FailIf(observed != 16 - free || _entities.Entities<PumpkinHeadProjectile>().Count != free || F1Count(native, 0x42) != free ||
                _entities.Entities<PumpkinHeadProjectile>().Any(shot => shot.Delay != 8),
                $"Repeated firing free={free}: PART observers={observed}, runtime children={_entities.Entities<PumpkinHeadProjectile>().Count}, source={F1Count(native, 0x42)}, delays={string.Join(',', _entities.Entities<PumpkinHeadProjectile>().Select(shot => shot.Delay))}; ENEMY must precede PART.");
            _entities.Clear();
            FailIf(F1Parts.Count != 0, "Pumpkin cancellation must release all real PART slots.");
        }
    }
}
