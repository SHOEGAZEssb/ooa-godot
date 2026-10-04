using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkContactDamageHandoffRom()
    {
        FieldInfo fraction = typeof(Player).GetField("_damageAccumulator", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int hostCase = 0;
        foreach (var (ring, gate, outcome, partId) in new[]
        {
            ((int)RingId.Blue, "normal", 0, 0x19), ((int)RingId.ArmorL3, "text", 0, 0x19),
            ((int)RingId.Protection, "disabled", 0, 0x19), ((int)RingId.Steadfast, "normal", 0, 0x19),
            ((int)RingId.Steadfast, "palette", 0, 0x19), ((int)RingId.PowerL1, "sword", 0, 0x19),
            ((int)RingId.BlueHoly, "normal", 0, 0x19), (0xff, "normal", 1, 0x19), (0xff, "normal", 2, 0x19),
            (0xff, "disabled", 1, 0x19), (0xff, "text", 1, 0x19),
            (0xff, "normal", 0, 0x18), (0xff, "normal", 0, 0x1a)
        })
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, (byte)(outcome == 0 ? 0x20 : 1)); save.WriteWramByte(0xc6ab, 0x30);
            InitializeTransientSession(save); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            if (ring != 0xff) _inventory.GrantAppraisedRingForDebug(ring);
            if (outcome == 2) _inventory.GiveTreasure(TreasureId.Potion, 0);
            _inventory.GiveTreasure(TreasureId.Sword, 1);
            _inventory.EquipA(gate == "sword" ? TreasureId.Sword : 0); _inventory.EquipB(0);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c,
                    x is 0 or 9 || y is 0 or 7 ? (byte)0x0f : (byte)0, 0);
            _player.WarpTo(new(80, 64)); _player.Face(Vector2I.Right);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, 80, 64) { HostilePartsEnabled = true };
            rom[0xd009] = 0xff; rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            bool frozen = false, attack = gate == "sword";
            var restriction = new EnemyGrabValidationRestriction { Frozen = false };
            _entities.AddEntity(restriction);
            Func<bool> palette = _entities.PaletteFadeActiveSource, disabled = _entities.NonInteractionObjectsDisabledSource;
            _entities.PaletteFadeActiveSource = () => gate == "palette" && frozen;
            _entities.NonInteractionObjectsDisabledSource = () => gate == "disabled" && frozen;
            int update = 0, previousHeld = 0;
            IHostileProjectile? ordinaryShot = null;
            ZoraFireProjectile? zoraShot = null;
            void Step(int count = 1)
            {
                int held = attack ? 1 : 0, edge = held & ~previousHeld; previousHeld = held;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.AdvanceDeathPrelude();
                    rom.UpdateGameplay(edge, held, 0xff, _entities.FrameCounter); edge = 0;
                    string context = $"Contact PART${partId:x2} queue ring=${ring:x2} gate={gate} outcome={outcome} batch={batched} update={++update}";
                    FailIf(_player.HealthQuarters != rom[0xc6aa] || (_player.PendingContactDamageRaw & 0xff) != rom[0xd025] ||
                        _player.NativeContactSignal != (rom[0xd02a] != 0) || (int)fraction.GetValue(_player)! != rom[0xd029] ||
                        _inventory.HasTreasure(TreasureId.Potion) != ((rom[0xc69f] & 0x80) != 0) || _player.IsDying != (rom[0xcdd5] != 0),
                        context + $": health/pending byte/contact lifetime/fraction/potion/death differs: runtime={_player.HealthQuarters}/${_player.PendingContactDamageRaw & 0xff:x2}/{_player.NativeContactSignal}/${(int)fraction.GetValue(_player)!:x2}, native={rom[0xc6aa]}/${rom[0xd025]:x2}/${rom[0xd02a]:x2}/${rom[0xd029]:x2}.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        _player.KnockbackFrames != rom[0xd02d] || _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                        _player.IsAttacking != Enumerable.Range(0xd2, 3).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 5),
                        context + ": fixed movement/recoil/invincibility/retained Sword differs.");
                    bool finished = ordinaryShot?.Finished ?? zoraShot?.Finished ?? true;
                    if (ordinaryShot is not null || zoraShot is not null)
                        FailIf(finished == (rom[0xd0c0] != 0), context + ": deferred PART deletion differs.");
                    // The native fixture supplies object passes, while the
                    // status bar/menu warning has its own ROM comparisons.
                    FailIf(!sounds.Requests.Where(id => id is not (SoundId.SndText or SoundId.SndGainHeart or SoundId.SndHeartBeep)).SequenceEqual(rom.Sounds),
                        context + $": cue order differs: runtime={string.Join(',', sounds.Requests)}, native={string.Join(',', rom.Sounds)}.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                });
            }
            try
            {
                Step();
                for (int repeat = 0; repeat < (outcome == 1 ? 1 : 2); repeat++)
                {
                    if (repeat != 0)
                    {
                        _inventory.RefillHealth(); rom[0xc6aa] = rom[0xc6ab];
                        if (_inventory.ActiveRing != 0xff)
                            FailIf(!_inventory.EquipRingAt(0), "Could not release the previous queued-damage ring.");
                        rom[0xc6cb] = 0xff;
                    }
                    Vector2 start = _player.Position + Vector2.Right * 6;
                    if (partId == 0x1a) start -= EnemyBehaviorTables.Shared.EnemyArrowSpawnOffsets[3].Vector;
                    ordinaryShot = partId switch
                    {
                        0x18 => _entities.Spawn<OctorokRockProjectile>(new OctorokRockSpawn(start, 24)),
                        0x1a => _entities.Spawn<EnemyArrowProjectile>(new EnemyArrowSpawn(start, 24)),
                        _ => null
                    };
                    zoraShot = partId == 0x19 ? _entities.Spawn<ZoraFireProjectile>(new ZoraFireSpawn(start)) : null;
                    rom[0xd0c0] = 1; rom[0xd0c1] = (byte)partId; rom[0xd0c9] = 24;
                    rom[0xd0cb] = (byte)start.Y; rom[0xd0cd] = (byte)start.X;
                    int health = _player.HealthQuarters;
                    Step();
                    FailIf(rom[0xd025] == 0 || _player.HealthQuarters != health || !(_player.NativeContactSignal && rom[0xd02a] != 0),
                        $"Actual PART${partId:x2} post-object contact must publish raw damage/counters without changing health or consuming its ring.");
                    // Declared inventory-equipment boundary between publication
                    // and consumption. Source protections were already sampled.
                    if (ring != 0xff)
                    {
                        if (_inventory.RingAt(0) != ring)
                            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring), $"Could not store queued-damage ring ${ring:x2}.");
                        FailIf(!_inventory.EquipRingAt(0), $"Could not equip queued-damage ring ${ring:x2}.");
                        rom[0xc6cb] = (byte)ring;
                    }
                    frozen = true;
                    if (gate == "text") { _dialogue.ShowMessage("Queued damage pause.", _player.Position.Y); rom[0xcba0] = 1; }
                    if (gate == "palette") rom[0xc4ab] = 1;
                    if (gate == "disabled") { restriction.Frozen = true; rom[0xcc8a] = 0x81; }
                    Step(3);
                    FailIf((rom[0xd025] != 0) != (gate == "palette") || _player.NativeContactSignal,
                        "Palette retains pending damage, while text/$81 still consume it; var2a always clears after Link.");
                    frozen = false; restriction.Frozen = false; rom[0xc4ab] = rom[0xcc8a] = 0;
                    _dialogue.Close(); rom[0xcba0] = 0; attack = false;
                    Step();
                    if (outcome == 1) { Step(16); break; } // Spin handoff; game-over UI has its own fixture.
                    for (int wait = 0; (rom[0xd02d] != 0 || rom[0xd02b] != 0 || rom[0xd0c0] != 0 || _player.IsAttacking) && wait < 40; wait++) Step();
                    FailIf(rom[0xd02d] != 0 || rom[0xd02b] != 0 || rom[0xd0c0] != 0 || _player.IsAttacking,
                        "Contact/recoil/part/Sword must finish before bounded repeat and slot reuse.");
                    Step();
                }
            }
            finally { _entities.PaletteFadeActiveSource = palette; _entities.NonInteractionObjectsDisabledSource = disabled; _dialogue.Close(); }
        }
        CompareElectricContactDamageHandoffRom();
        GD.Print("Validated clean-US actual PART$18/$19/$1a post-object damage publication/deferred deletion, electric collisionEffect36, exact next-Link health/fraction/potion/death, ring changes at consumption, source protection timing, Steadfast signal lifetime, text/$81/palette gates, retained Sword, recovery/repeat and cues/RNG through split/batched gameplay.");
    }

    private void CompareElectricContactDamageHandoffRom()
    {
        int hostCase = 0;
        foreach (int ring in new[] { 0xff, (int)RingId.GreenHoly, (int)RingId.Protection, (int)RingId.Steadfast, (int)RingId.Blue })
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            InitializeTransientSession(OracleSaveData.CreateStandardGame());
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1); _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0), "Electric fixture could not equip its ring.");
            }
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0x2c, 0, 0);
            _player.WarpTo(new(80, 64));
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 2, 80, 64);
            rom[0xd009] = 0xff; rom.InitializeLinkGameplay();
            var menu = rom.CreateMenuView();
            var collision = new ObjectCollisionRom();
            collision[0xc6aa] = (byte)_player.HealthQuarters; collision[0xc6cb] = (byte)ring;
            collision[0xd024] = 0x80; collision[0xd004] = 1;
            collision[0xd00b] = collision[0xd08b] = 64; collision[0xd00d] = collision[0xd08d] = 80;
            collision[0xd026] = collision[0xd027] = collision[0xd0a6] = collision[0xd0a7] = 6;
            collision[0xd0a4] = 0xfc; collision[0xd0a5] = 0x4d;
            collision[0xd0a8] = 0xf8; collision[0xd0a9] = 10; collision[0xd0be] = 0x80;
            collision.Call(ObjectCollisionRom.Scan);
            FailIf(collision.Dispatches.Count != 1 || collision.Dispatches[0].Effect != 0x36,
                "The declared native ENEMY$7c mode$4d contact did not execute collisionEffect36.");
            // Transfer the executed collision boundary into the Link/shock
            // fixture; enemy AI is outside this bounded handoff comparison.
            foreach (int address in new[] { 0xd024, 0xd025, 0xd02a, 0xd02b, 0xd02c, 0xd02d, 0xccdb }) rom[address] = collision[address];
            _player.ApplyElectricShock(new(80, 64));
            FailIf(_player.HealthQuarters != collision[0xc6aa] || (_player.PendingContactDamageRaw & 0xff) != collision[0xd025] ||
                _player.NativeContactSignal != (collision[0xd02a] != 0) || _player.KnockbackFrames != collision[0xd02d],
                "Electric contact must publish the original raw damage/signal/counters without committing health.");
            var audit = _sound.AttachPlayRequestAudit();
            int update = 0;
            StepGameplayUpdates(56, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                menu.AdvancePalette(); menu.AdvanceElectricShock();
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); menu.UpdateScreenShake();
                string context = $"Electric handoff ring=${ring:x2} batch={batched} update={++update}";
                FailIf(_player.HealthQuarters != rom[0xc6aa] || (_player.PendingContactDamageRaw & 0xff) != rom[0xd025] ||
                    _player.NativeContactSignal != (rom[0xd02a] != 0) || _player.ElectricShockCounter != rom[0xccdc] ||
                    _player.ElectricShockActive != (rom[0xccdb] != 0) || _player.KnockbackFrames != rom[0xd02d] ||
                    _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                    context + $": health {_player.HealthQuarters}/{rom[0xc6aa]}, damage ${_player.PendingContactDamageRaw & 0xff:x2}/${rom[0xd025]:x2}, signal {_player.NativeContactSignal}/${rom[0xd02a]:x2}, inv {_player.InvincibilityFrames}/{unchecked((sbyte)rom[0xd02b])}, recoil {_player.KnockbackFrames}/{rom[0xd02d]}, shock {_player.ElectricShockCounter}/{rom[0xccdc]}, XY {_player.PrecisePosition}/{new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f)} differs.");
                FailIf(!audit.Requests.Where(id => id is not (SoundId.SndGainHeart or SoundId.SndHeartBeep)).SequenceEqual(rom.Sounds), context + ": cue order differs.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                    context + ": shared RNG differs.");
            });
            FailIf(_player.ElectricShockActive || _player.KnockbackFrames != 0 || _player.PendingContactDamageRaw != 0,
                "Shock must clear its gate and consume recoil before completion.");
        }
    }
}
