using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateOceanHazardsRom()
    {
        foreach (bool pollution in new[] { false, true })
        foreach (bool batch in RomHostSchedules(pollution ? 1 : 0))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            LoadValidationRoom(2, 0xb6);
            // The pictured room's original left pollution and right whirlpool.
            FailIf(_currentRoom.ActiveCollisions != 4 ||
                _currentRoom.GetPackedStorageMetatile(0x20) != 0xeb ||
                _currentRoom.GetPackedStorageMetatile(0x26) != 0xe9 ||
                _entities.Entities<SuctionPitRoomEntity>().Count != 1,
                "Room2:b6 must allocate PART$2e for pollution$eb and whirlpool$e9.");
            var sea = _entities.Entities<SuctionPitRoomEntity>().Single();
            Vector2 start = new(pollution ? 40.25f : 80.25f, 40.5f);
            _player.WarpTo(start); _player.Face(pollution ? Vector2I.Left : Vector2I.Right);
            _player.SetLocalRespawnPosition(start, _player.FacingVector);
            FailIf(_collision.Collides(start) || _currentRoom.GetMetatile(start) is 0xe9 or 0xeb,
                "Ocean hazard approach must begin on the room's original safe, open terrain.");
            int facing = pollution ? 3 : 1;
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, facing,
                (int)start.X, (int)start.Y) { HostilePartsEnabled = true };
            rom.Word(0xd00c, (int)(start.X * 256)); rom.Word(0xd00a, (int)(start.Y * 256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd009] = 0;
            rom[0xcc21] = (byte)start.Y; rom[0xcc22] = (byte)start.X; rom[0xcc23] = (byte)facing;
            rom[0xd0c0] = 1; rom[0xd0c1] = 0x2e;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, lastHeld = 0, health = _player.HealthQuarters;
            bool pulled = false, captured = false, fell = false, hit = false;
            void Step(int count = 1, int angle = 0xff)
            {
                int held = angle == 24 ? 0x20 : angle == 8 ? 0x10 : 0;
                int edge = held & ~lastHeld; lastHeld = held;
                StepGameplayUpdates(count, angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    MenuRomActions(held), MenuRomActions(edge), batched: batch, afterUpdate: () =>
                    {
                        rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter); edge = 0;
                        string context = $"Room2:b6 {(pollution ? "pollution$eb" : "whirlpool$e9")}, batch={batch}, update{++update}";
                        FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                            sea.State != rom[0xd0c4] || sea.Counter != rom[0xd0c6] ||
                            _runtimeState.ReadWramByte(AgesWramAddress.wDisableScreenTransitions) != rom[0xcc91] ||
                            _player.HealthQuarters != rom[0xc6aa] ||
                            _player.PendingContactDamageRaw != unchecked((sbyte)rom[0xd025]) ||
                            _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                            SomariaPrivate<float>(_player, "_enemyKnockbackFrames") != rom[0xd02d] ||
                            (SomariaPrivate<int>(_player, "_forcedRespawnPhase") == 1) != (rom[0xcc4f] == 2) ||
                            _player.NativeNormalStateForInteraction != (rom[0xd004] == 1),
                            $"{context}: XY={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}; " +
                            $"PART={sea.State}:{sea.Counter:x2}/{rom[0xd0c4]}:{rom[0xd0c6]:x2}; " +
                            $"health={_player.HealthQuarters}/{rom[0xc6aa]}, damage={_player.PendingContactDamageRaw}/{unchecked((sbyte)rom[0xd025])}, " +
                            $"inv={_player.InvincibilityFrames}/{unchecked((sbyte)rom[0xd02b])}, recoil={SomariaPrivate<float>(_player, "_enemyKnockbackFrames")}/{rom[0xd02d]}.");
                        FailIf(!sounds.Requests.Where(id => id is SoundId.SndDamageLink or SoundId.SndLinkFall)
                            .SequenceEqual(rom.Sounds.Where(id => id is SoundId.SndDamageLink or SoundId.SndLinkFall)),
                            context + ": hazard sound boundaries differ.");
                        pulled |= rom[0xcc91] != 0;
                        captured |= rom[0xcc4f] == 2;
                        fell |= rom[0xd004] == 2 && rom[0xd005] == 1;
                        hit |= rom[0xd025] == 0xfe;
                    });
            }
            Step(2);
            FailIf(pulled || hit || sea.Counter != 0x20, "PART$2e must initialize harmlessly on safe terrain.");
            int approachAngle = pollution ? 24 : 8;
            for (int wait = 0; !(pollution ? hit : captured) && wait < 180; wait++)
                Step(angle: wait % 6 == 0 ? 0xff : approachAngle);
            if (pollution)
            {
                FailIf(!hit || _player.HealthQuarters != health || _player.InvincibilityFrames != 0x18 ||
                    SomariaPrivate<float>(_player, "_enemyKnockbackFrames") != 0x0c,
                    "Pollution must publish raw damage$fe and invincibility$18/recoil$0c before Link consumes it.");
                Step();
                FailIf(_player.HealthQuarters != health - 1, "Pollution must remove one quarter-heart on the next Link update.");
                Step(30);
                hit = false;
                for (int wait = 0; !hit && wait < 180; wait++)
                    Step(angle: wait % 6 == 0 ? 0xff : approachAngle);
                FailIf(!hit, "Pollution must hurt again after recovery and a repeated real approach.");
                Step();
            }
            else
            {
                FailIf(!pulled || !captured, "Ocean whirlpool must pull Link and request the native falling state.");
                for (int wait = 0; _player.HealthQuarters == health && wait < 80; wait++) Step();
                FailIf(!fell || _player.HealthQuarters != health - 2,
                    "Ocean whirlpool must complete the falling animation and half-heart respawn damage.");
                Step(18);
                FailIf(_player.IsFallingInHole || _player.PrecisePosition != new Vector2((int)start.X, (int)start.Y) ||
                    sea.Counter != 0x20 || _runtimeState.ReadWramByte(AgesWramAddress.wDisableScreenTransitions) != 0,
                    "Ocean whirlpool must return Link to his safe position and release the transition lock.");
                captured = false;
                for (int wait = 0; !captured && wait < 180; wait++)
                    Step(angle: wait % 6 == 0 ? 0xff : approachAngle);
                FailIf(!captured, "Ocean whirlpool must remain active after a completed respawn.");
            }
        }
        GD.Print("Validated room2:b6 ocean whirlpool pull/fall/respawn/reuse and pollution contact/delayed damage/recoil/reuse against clean-US gameplay, including split/batched updates.");
    }
}
