using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private FeatherRom PrepareFeatherRom(bool primary = true, float fraction = 0)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(4, 0xa8);
        _entities.Clear();
        _inventory.GiveTreasure(TreasureId.Feather, 1);
        _inventory.EquipA(primary ? TreasureId.Feather : 0);
        _inventory.EquipB(primary ? 0 : TreasureId.Feather);
        for (int y = 8; y < _currentRoom.Height; y += 16)
        for (int x = 8; x < _currentRoom.Width; x += 16)
            _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
        _player.WarpTo(new(120, 104));
        StepGameplayUpdates(16, Vector2.Up);
        FailIf(_player.Position != new Vector2(120, 88) || _currentRoom.IsSolid(_player.Position),
            "Feather fixture must reach the takeoff point by walking through clear room geometry.");
        _player.WarpTo(new(120 + fraction, 88 + fraction));
        _player.Face(Vector2I.Up);
        return new FeatherRom(_currentRoom, _player.PrecisePosition, primary);
    }

    private void CompareFeatherRom(FeatherRom rom, string context)
    {
        FailIf(rom[0xd300] != 0 || rom[0xd400] != 0,
            $"{context}: Ages Feather must release its parent on the launch/rejection update.");
        if (_currentRoom.Group >= 6)
        {
            FailIf(_player.PrecisePosition != rom.Position || _player.SideScrollAirborne != rom.Airborne ||
                _player.SideScrollYFixed != rom.Word(0xd00a) || _player.SideScrollSpeedZ != rom.SpeedZ,
                $"{context}: sideview runtime XY={_player.PrecisePosition}, air={_player.SideScrollAirborne}, Vz=${_player.SideScrollSpeedZ & 0xffff:x4}; ROM XY={rom.Position}, air=${rom[0xcc5c]:x2}, Vz=${rom.SpeedZ & 0xffff:x4}, angle=${rom[0xd009]:x2}, speed=${rom[0xd010]:x2}, walls=${rom[0xd033]:x2}.");
            if (rom.Airborne)
            {
                int phase = (rom[0xd031] & 0xfc) switch
                {
                    0xe4 => 0, 0xe8 => 1, 0xec => 2, 0x80 => 3,
                    _ => throw new InvalidOperationException($"{context}: unknown sideview jump graphic ${rom[0xd031]:x2}.")
                };
                FailIf(!_player.AirborneLinkUsesJumpAnimation || rom[0xd030] != 0x18 ||
                    _player.SideScrollAnimationPhase != phase,
                    $"{context}: sideview jump animation differs from native phase {phase}, mode ${rom[0xd030]:x2}.");
            }
            return;
        }
        FailIf(_player.PrecisePosition != rom.Position || _player.TopDownAirborne != rom.Airborne ||
            _player.SwitchHookZFixed != rom.ZFixed || _player.TopDownAirSpeedZ != rom.SpeedZ,
            $"{context}: runtime XY={_player.PrecisePosition}, air={_player.TopDownAirborne}, Z=${_player.SwitchHookZFixed & 0xffff:x4}, Vz=${_player.TopDownAirSpeedZ & 0xffff:x4}; ROM XY={rom.Position}, air=${rom[0xcc5c]:x2}, Z=${rom.ZFixed & 0xffff:x4}, Vz=${rom.SpeedZ & 0xffff:x4}, angle=${rom[0xd009]:x2}, speed=${rom[0xd010]:x2}, walls=${rom[0xd033]:x2}, var37/38=${rom[0xd037]:x2}/${rom[0xd038]:x2}.");
        if (_currentRoom.GetTerrainInfo(_player.Position).Type == TerrainType.Ice)
            CompareFeatherIceVelocityRom(rom, context);
        if (rom.Airborne)
        {
            FailIf(!_player.AirborneLinkUsesJumpAnimation || rom[0xd030] != 0x18,
                $"{context}: Feather must select native jump animation mode $18.");
            int nativePhase = rom[0xd031] switch
            {
                0xe4 => 0, 0xe8 => 1, 0xec => 2, 0x80 => 3,
                _ => throw new InvalidOperationException($"{context}: unknown native jump graphic ${rom[0xd031]:x2}.")
            };
            FailIf(_player.AirborneLinkBodyFrame != nativePhase ||
                nativePhase < 3 && _player.LinkAnimationCounter != rom[0xd020],
                $"{context}: runtime jump phase/counter={_player.AirborneLinkBodyFrame}/{_player.LinkAnimationCounter}, native={nativePhase}/{rom[0xd020]}.");
        }
    }

    private void StepFeatherRom(FeatherRom rom, int count, bool batched, int angle = 0xff,
        bool held = false, bool pressed = false, bool primary = true)
    {
        int update = 0;
        string button = primary ? "attack" : "item";
        Vector2 input = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Delta(0x28, angle).Normalized();
        StepGameplayUpdates(count, input, held ? [button] : [], pressed ? [button] : [], batched,
            afterUpdate: () =>
            {
                rom.Update(angle, held, pressed && update == 0, primary);
                CompareFeatherRom(rom, $"Feather angle=${angle:x2}, update={update++}, batch={batched}");
            });
    }

    private void ValidateFeatherJumpPhysicsRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (float fraction in new[] { 0f, 255 / 256f })
        {
            FeatherRom rom = PrepareFeatherRom(primary, fraction);
            var sounds = _sound.AttachPlayRequestAudit();
            StepFeatherRom(rom, 1, batched, held: true, pressed: true, primary: primary);
            FailIf(!rom.Airborne || rom.ZFixed != -0x1e0 || rom.SpeedZ != -0x1c0,
                "Feather launch must integrate speedZ=-$01e0 before applying gravity $20.");
            StepFeatherRom(rom, 29, batched, held: true, primary: primary);
            FailIf(!rom.Airborne, "Feather must remain airborne through update30.");
            StepFeatherRom(rom, 1, batched, held: true, primary: primary);
            FailIf(rom.Airborne || _player.TopDownAirborne,
                "Feather must land on update31 and clear its airborne state.");
            StepFeatherRom(rom, 4, batched, held: true, primary: primary);
            FailIf(!sounds.Requests.Where(id => id is SoundId.SndJump or SoundId.SndLand).SequenceEqual(rom.Sounds),
                "Feather jump/land sound order must match the native Link handler.");
            StepFeatherRom(rom, 1, batched, primary: primary);
            StepFeatherRom(rom, 31, batched, held: true, pressed: true, primary: primary);
            FailIf(rom.Airborne, "A fresh Feather input edge must permit a second complete jump.");
        }
        GD.Print("Validated executed-ROM Feather fixed-point launch/flight/landing and repeat jumps through A/B and individual/batched gameplay updates.");
    }
}
