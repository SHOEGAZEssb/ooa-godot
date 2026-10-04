using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLavaRecoveryRom() => ValidateLavaRecoveryRom(false, false);
    private void ValidateIndoorLavaRecoveryRom() => ValidateLavaRecoveryRom(false, true);
    private void ValidateUnderwaterLavaRecoveryRom() => ValidateLavaRecoveryRom(true, false);
    private void ValidateUnderwaterIndoorLavaRecoveryRom() => ValidateLavaRecoveryRom(true, true);

    private void ValidateLavaRecoveryRom(bool underwater, bool indoors)
    {
        int hostCase1 = 0;
        foreach (int tile in indoors ? new[] { 0x61, 0x62, 0x63, 0x64, 0x65 } :
            new[] { 0xe4, 0xe5, 0xe6, 0xe7, 0xe8 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            int group = indoors ? underwater ? 5 : 4 : underwater ? 2 : 0;
            int roomId = indoors ? underwater ? 0x4c : 0 : 0x90;
            if (indoors && underwater) _saveData.WriteWramByte(WramAddress.wJabuWaterLevel, 0x22);
            LoadValidationRoom(group, roomId); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    y == 4 && x is > 0 and < 9 ? (byte)tile : (byte)0xa0,
                    x is 0 or 9 || y is 0 or 7 ? (byte)0x0f : y == 4 ? (byte)0x10 : (byte)0, 0);
            _player.Face(Vector2I.Right); _player.WarpTo(new(80, 56));
            _player.WarpTo(new(80.25f, 56.5f), recordSafe: false);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1, 80, 56);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom[0xcc21] = 56; rom[0xcc22] = 80; rom[0xcc23] = 1;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousDirections = 0, requests = 0, respawns = 0;
            bool previousRequest = false, previousRespawning = false;
            void Step(int count = 1, int angle = 0xff)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = angle == 0x10 ? 0x80 : 0;
                int edge = held & ~previousDirections; previousDirections = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter - 1); edge = 0; update++;
                    bool request = rom[0xcc4f] == 2;
                    bool respawning = rom[0xd004] == 2;
                    if (request && !previousRequest) requests++;
                    if (respawning && !previousRespawning) respawns++;
                    previousRequest = request; previousRespawning = respawning;
                    string context = $"Lava {group:x1}:{roomId:x2} tile=${tile:x2}, update={update}, batch={batched}";
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(_player.PrecisePosition != expected || _player.HealthQuarters != rom[0xc6aa] ||
                        _player.Visible != ((rom[0xd01a] & 0x80) != 0) ||
                        _player.PatchCollisionsEnabled != ((rom[0xd024] & 0x80) != 0) ||
                        _player.NativeNormalStateForInteraction != (rom[0xd004] == 1) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                        _player.TopDownSwimming || _player.IsFallingInHole || _transitions.IsTransitioning,
                        context + $": motion/state/recovery differs: runtime={_player.PrecisePosition}, native={expected}, state=${rom[0xd004]:x2}/${rom[0xd005]:x2}, force=${rom[0xcc4f]:x2}, swim=${rom[0xcc5d]:x2}, health={_player.HealthQuarters}/{rom[0xc6aa]}.");
                    if (rom[0xd004] == 2 && rom[0xd005] == 5)
                        FailIf((_player.DrownAnimationFrame == 0 ? 0xd4 : 0x0b) != rom[0xd031],
                            context + ": native drowning animation frame differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": gameplay sound order differs.");
                    int invincibility = (int)(float)typeof(Player).GetField("_enemyInvincibilityFrames",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
                    FailIf((invincibility & 0xff) != rom[0xd02b],
                        context + $": post-Link invincibility differs: runtime=${invincibility & 0xff:x2}, native=${rom[0xd02b]:x2}.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            Step(6);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int requestedBefore = requests;
                for (int pulse = 0; pulse < 32 && requests == requestedBefore; pulse++)
                {
                    Step();
                    for (int held = 0; held < 5 && requests == requestedBefore; held++) Step(1, 0x10);
                }
                FailIf(requests != requestedBefore + 1 || !_player.IsDrowning,
                    "Collision-reachable lava approach did not enter exactly one native drowning request.");
                Step(6, 0x10); Step(120);
                FailIf(respawns != attempt + 1 || rom[0xd004] != 1 || _player.IsDrowning ||
                    _player.IsFallingInHole || _player.HealthQuarters != 12 - (attempt + 1) * 2,
                    "Lava recovery did not finish with one half-heart of damage and reusable control.");
            }
        }
        GD.Print($"Validated clean-US lava variants underwater={underwater}, indoors={indoors}, reachable entry, force/respawn handoff, fixed XY/facing, collision/visibility, animation, health/sound and repeated recovery in split/batched gameplay.");
    }
}
