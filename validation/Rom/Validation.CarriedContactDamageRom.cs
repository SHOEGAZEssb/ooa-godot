using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCarriedContactDamageRom()
    {
        int hostCase = 0;
        foreach (bool bomb in new[] { false, true })
        foreach (string gate in new[] { "normal", "text", "palette", "throw", "thrown" })
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            InitializeTransientSession(OracleSaveData.CreateStandardGame());
            LoadValidationRoom(4, 0xa8); _entities.Clear();
            _inventory.GiveTreasure(bomb ? TreasureId.Bombs : TreasureId.Bracelet, bomb ? 0x10 : 1);
            _inventory.EquipA(bomb ? TreasureId.Bombs : TreasureId.Bracelet); _inventory.EquipB(0);
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            Vector2 pot = new(120, 88);
            if (!bomb)
            {
                _currentRoom.SetPositionTileAndCollision(pot, 0x10, 0x0f, 0);
                _currentRoom.SetUnderlyingMetatile(pot, 0xa0);
            }
            _player.WarpTo(bomb ? new(120, 112) : new(120, 114)); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, bomb ? 112 : 114)
                { HostilePartsEnabled = true };
            rom[0xd009] = 0xff; rom.InitializeLinkGameplay();
            var audit = _sound.AttachPlayRequestAudit();
            bool frozen = false;
            Func<bool> palette = _entities.PaletteFadeActiveSource;
            _entities.PaletteFadeActiveSource = () => gate == "palette" && frozen;
            int update = 0;
            void Step(int count = 1, int angle = 0xff, bool held = false, bool pressed = false)
            {
                int edge = pressed ? 1 : 0;
                StepGameplayUpdates(count, angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    held ? ["attack"] : [], pressed ? ["attack"] : [], batched, () =>
                    {
                        int directionKey = angle switch { 0 => 0x40, 8 => 0x10, 16 => 0x80, 24 => 0x20, _ => 0 };
                        rom.UpdateGameplay(edge, directionKey | (held ? 1 : 0), angle, _entities.FrameCounter); edge = 0;
                        string context = $"Carry contact bomb={bomb} gate={gate} batch={batched} update={++update}";
                        FailIf(_player.HealthQuarters != rom[0xc6aa] || (_player.PendingContactDamageRaw & 0xff) != rom[0xd025] ||
                            _player.NativeContactSignal != (rom[0xd02a] != 0) ||
                            _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                            _player.KnockbackFrames != rom[0xd02d] || _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                            _player.IsCarryingObject != (rom[0xcc5a] == 0x83),
                            context + ": pending damage/signal, health, fixed movement, counters or carry ownership differs.");
                        if (bomb)
                        {
                            BombParentState state = rom[0xd200] == 0 ? BombParentState.Idle : rom[0xd204] switch
                            {
                                2 => BombParentState.Lifting, 3 => BombParentState.Holding, 4 => BombParentState.Throwing,
                                _ => throw new InvalidOperationException(context + $": native Bomb state ${rom[0xd204]:x2}.")
                            };
                            FailIf(_bomb.State != state || _inventory.Bombs != rom[0xc6b0], context + ": bomb parent/ammo differs.");
                            BombEffect[] children = _entities.Entities<BombEffect>().Where(child => !child.Finished).ToArray();
                            int[] slots = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                                .Where(slot => rom[slot] != 0 && rom[slot + 1] == 3).ToArray();
                            FailIf(children.Length != slots.Length, context + ": ITEM$03 occupancy differs.");
                            for (int index = 0; index < slots.Length; index++)
                            {
                                int slot = slots[index]; BombEffect child = children[index];
                                FailIf(child.PrecisePosition != new Vector2(rom.Word(slot + 0xc) / 256f, rom.Word(slot + 0xa) / 256f) ||
                                    child.ZFixed != unchecked((short)rom.Word(slot + 0xe)) ||
                                    child.State == BombState.Thrown && (child.SpeedRaw != rom[slot + 0x10] || child.SpeedZ != unchecked((short)rom.Word(slot + 0x14))),
                                    context + $": ITEM$03 position {child.PrecisePosition}/{new Vector2(rom.Word(slot + 0xc) / 256f, rom.Word(slot + 0xa) / 256f)}, Z ${child.ZFixed & 0xffff:x4}/${rom.Word(slot + 0xe):x4}, speed ${child.SpeedRaw:x2}/${rom[slot + 0x10]:x2}, VZ ${child.SpeedZ & 0xffff:x4}/${rom.Word(slot + 0x14):x4} differs.");
                            }
                        }
                        else
                        {
                            BraceletState state = rom[0xd200] == 0 ? rom[0xdc00] == 0 ? BraceletState.Idle : BraceletState.Projectile : rom[0xd204] switch
                            {
                                0 => BraceletState.SeekingWall, 1 => BraceletState.GrabbingWall, 2 => BraceletState.Lifting,
                                3 => BraceletState.Holding, 4 => BraceletState.Throwing,
                                _ => throw new InvalidOperationException(context + $": native Bracelet state ${rom[0xd204]:x2}.")
                            };
                            FailIf(_bracelet.State != state || (_bracelet.LiftedObject is not null) != (rom[0xdc00] != 0) ||
                                _currentRoom.GetMetatile(pot) != rom[0xcf57], context + $": Bracelet parent {_bracelet.State}/{state}, ITEM$16 {_bracelet.LiftedObject is not null}/${rom[0xdc00]:x2}, pot ${_currentRoom.GetMetatile(pot):x2}/${rom[0xcf57]:x2} differs.");
                            if (_bracelet.LiftedObject is { } child)
                            {
                                Vector2 ground = child.Thrown ? child.GroundPosition : _player.Position + new Vector2(child.Position.X, 0);
                                int z = child.Thrown ? child.ZFixed : (int)child.Position.Y << 8;
                                FailIf(ground != new Vector2(rom.Word(0xdc0c) / 256f, rom.Word(0xdc0a) / 256f) ||
                                    z != unchecked((short)rom.Word(0xdc0e)) || child.Thrown != (rom[0xdc04] == 3) ||
                                    child.Thrown && (child.SpeedRaw != rom[0xdc10] || child.SpeedZ != unchecked((short)rom.Word(0xdc14))),
                                    context + ": ITEM$16 fixed position/gravity/throw speed differs.");
                            }
                        }
                        FailIf(!audit.Requests.Where(id => id is not (SoundId.SndText or SoundId.SndGainHeart or SoundId.SndHeartBeep)).SequenceEqual(rom.Sounds),
                            context + ": pickup/contact/throw/impact cue order differs.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                            context + ": shared RNG differs.");
                    });
            }
            try
            {
                if (!bomb) Step(24, 0);
                Step(1, held: true, pressed: true);
                if (!bomb) Step(11, 16, held: true);
                Step(bomb ? 19 : 13);
                FailIf(!_player.IsCarryingObject, "Actual native pickup must reach state3 before contact.");
                bool alreadyThrown = gate is "throw" or "thrown";
                if (alreadyThrown)
                {
                    Step(1, 0, held: true, pressed: true);
                    if (gate == "thrown") Step(9);
                }
                Vector2 start = _player.Position + Vector2.Right * 6;
                _entities.Spawn<ZoraFireProjectile>(new ZoraFireSpawn(start));
                rom[0xd0c0] = 1; rom[0xd0c1] = 0x19; rom[0xd0cb] = (byte)start.Y; rom[0xd0cd] = (byte)start.X;
                Step();
                FailIf(rom[0xd025] == 0 || _player.IsCarryingObject == alreadyThrown, "Post-object contact must preserve held/released ownership until its parent's next eligible dispatch.");
                frozen = true;
                if (gate == "text") { _dialogue.ShowMessage("Carry pause.", _player.Position.Y); rom[0xcba0] = 1; }
                if (gate == "palette") rom[0xc4ab] = 1;
                Step(3);
                frozen = false; _dialogue.Close(); rom[0xcba0] = rom[0xc4ab] = 0;
                Step();
                if (gate is "text" or "palette")
                {
                    FailIf(!_player.IsCarryingObject, "A frozen parent loses var2a; queued health damage alone must not release its object.");
                    Step(1, 0, held: true, pressed: true);
                }
                Step(40);
                FailIf(_player.IsCarryingObject || (bomb ? _bomb.Active : _bracelet.State is not (BraceletState.Idle or BraceletState.Projectile)), "Throw parent must complete and return Link control.");
            }
            finally { _entities.PaletteFadeActiveSource = palette; _dialogue.Close(); }
        }
        GD.Print("Validated clean-US actual Bomb/Bracelet pickup, PART$19 contact publication, next-parent throw, text/palette signal clearing with retained carry, explicit release and completion, fixed Link/item motion, health/counters, cues/RNG in split/batched gameplay.");
    }
}
