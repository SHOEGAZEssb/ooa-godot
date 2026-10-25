using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookSandCrabRom()
    {
        LoadValidationRoom(5, 0x4f);
        foreach (int releaseHeight in new[] { 0, -4 })
        {
            // Bounded native ENEMY walk complements the actual hook gameplay
            // regression. Declare an initialized crab, execute the original
            // collision scan, and supply only ITEM-owned XYZ/substate writes.
            var crab = new SandCrabCharacter();
            var random = new OracleRandom();
            crab.Initialize(EnemyDatabase.Shared.ImportedEnemy(0x1a), _currentRoom, new(104.25f, 88.5f), random);
            crab.UpdateFrame(); crab.UpdateFrame();
            var rom = new EnemyStatusRom(_currentRoom, _saveData, 0);
            const int address = 0xd080;
            rom[address] = 1; rom[address + 1] = 0x1a;
            rom[address + 4] = 9; rom[address + 6] = (byte)crab.Counter;
            rom[address + 9] = (byte)crab.Angle;
            rom.Word(address + 10, (int)(crab.Position.Y * 256));
            rom.Word(address + 12, (int)(crab.Position.X * 256));
            rom[address + 0x24] = 0x9a;
            rom[address + 0x25] = 0x10;
            rom[address + 0x26] = rom[address + 0x27] = 6;
            rom[address + 0x29] = (byte)crab.Health;
            rom[address + 0x3f] = 0x10; // scent-responsive flag, sandCrab.s:state0
            rom.HitWithSword(0x0d, 0);
            crab.BeginSwitchHook(new(103, 88));
            FailIf(rom[address + 4] != 3 || rom[address + 5] != 0 ||
                rom[address + 0x24] != 0x1a || rom[address + 0x29] != crab.Health,
                $"Clean-US $1a hook collision must select $03:$00, clear collision bit7 and preserve health; native=${rom[address+4]:x2}:${rom[address+5]:x2}, collision=${rom[address+0x24]:x2}, health={rom[address+0x29]}/{crab.Health}.");
            int calls = random.Calls, frame = 0;
            void Tick(bool frozen = false)
            {
                rom[0xcba0] = (byte)(frozen ? 1 : 0);
                // Ages wScentSeedActive=$ccd9: state $03 must reject it.
                rom[0xccd9] = 1;
                if (!frozen) crab.UpdateFrame(new Vector2(120, 88));
                rom.Update(frame++, new(103, 88));
                FailIf((int)crab.State != rom[address + 4] || crab.SwitchHookSubstate != rom[address + 5] ||
                    (crab.ZFixed & 0xffff) != rom.Word(address + 14) || crab.Counter != rom[address + 6] ||
                    crab.CollisionEnabled != ((rom[address + 0x24] & 0x80) != 0) ||
                    random.Calls != calls || rom.RandomCalls != 0,
                    $"Clean-US $1a hook state/fall/scent/RNG differs at update {frame}, height={releaseHeight}.");
            }
            Tick(true); Tick(); Tick();
            crab.CopySwitchHookPosition(new(104, 88), releaseHeight);
            rom[address + 15] = unchecked((byte)releaseHeight);
            crab.SwapSwitchHook(); rom[address + 5] = 2; Tick();
            crab.ReleaseSwitchHook(); rom[address + 5] = 3;
            int updates = 0;
            do { Tick(); updates++; } while (crab.State == SandCrabState.SwitchHook && updates < 20);
            FailIf(updates != (releaseHeight == 0 ? 1 : 9) || crab.State != SandCrabState.ChoosingDirection,
                "Clean-US $1a release must land on update 1/9, restore state $08, and defer route RNG.");
            crab.Free();
        }
    }
}
