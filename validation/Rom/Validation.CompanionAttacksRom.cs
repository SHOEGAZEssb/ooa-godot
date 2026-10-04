using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDimitriSwallowRom()
    {
        var data = new DimitriDatabase();
        int type = Enumerable.Range(0, 128).First(data.AcceptsMouthCollision);
        int mode = Enumerable.Range(0, 125).First(data.CanSwallow);
        int hostCase2 = 0;
        foreach (bool vulnerable in new[] { false, true })
        foreach (int direction in new[] { 0, 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(0x0c, direction);
            Vector2 p = new Vector2(72, 64) + OracleObjectMath.StrictCardinalVector(direction * 8) * 16;
            var target = new CompanionMouthProbe(type, mode, p, vulnerable);
            _entities.AddEntity(target);
            rom[0xd080] = 1; rom[0xd0a4] = (byte)(0x80 | type); rom[0xd0a5] = (byte)mode;
            rom[0xd08b] = (byte)p.Y; rom[0xd08d] = (byte)p.X;
            rom[0xd0a6] = rom[0xd0a7] = 6; rom[0xd0a9] = 8;
            rom[0xd0ab] = vulnerable ? (byte)0 : (byte)0xff;
            void CompareSwallow() => FailIf(target.Swallowed != ((rom[0xd0bf] & 0x80) != 0),
                "Dimitri mouth contact did not match the native swallow effect/update boundary.");
            StepCompanionRom(actor, rom, 1, Vector2.Zero, batched, attack: true, edge: true, compare: CompareSwallow);
            StepCompanionRom(actor, rom, 65, Vector2.Zero, batched, compare: CompareSwallow);
            FailIf(target.Swallowed != vulnerable, "Dimitri swallow fixture did not exercise its intended eligibility gate.");
        }
    }

    private void ValidateDimitriMouthMasksRom()
    {
        var data = new DimitriDatabase();
        var rom = new ObjectCollisionRom();
        for (int type = 0; type < 128; type++)
        foreach (int gate in new[] { 0, 1, 2, 3, 4 })
        {
            rom.ClearObjects();
            rom[0xd0a4] = (byte)(type | (gate == 1 ? 0 : 0x80));
            rom[0xd0aa] = (byte)(gate == 2 ? 0x80 : 0);
            rom[0xd0ab] = (byte)(gate == 3 ? 1 : gate == 4 ? 0xff : 0);
            rom[0xd08b] = rom[0xd70b] = 64;
            rom[0xd08d] = rom[0xd70d] = 80;
            rom[0xd0a6] = rom[0xd0a7] = 6;
            rom[0xd726] = rom[0xd727] = 8;
            rom[0xd724] = 0x8f;
            rom.Call(ObjectCollisionRom.Scan);
            FailIf((rom.Dispatches.Count != 0) != (gate == 0 && data.AcceptsMouthCollision(type)),
                $"Dimitri native mouth mask type=${type:x2}, gate={gate} differs.");
        }
        for (int mode = 0; mode < 125; mode++)
            FailIf(data.CanSwallow(mode) != (rom.Table(0x6d0a + mode * 32 + 0x0f) == 0x25),
                $"Dimitri native swallow effect mode=${mode:x2} differs.");
        GD.Print("Validated all 128 Dimitri mouth masks with collision/invincibility gates, and all 125 swallow-effect rows.");
    }

    private void ValidateCompanionAttackTilesRom()
    {
        int hostCase1 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (int direction in new[] { 0, 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(id, direction);
            for (int y = 2; y <= 5; y++)
            for (int x = 3; x <= 5; x++)
                SetCompanionRomTile(rom, x, y, 0xc5);
            void CompareTiles()
            {
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 10; x++)
                    FailIf(_currentRoom.GetMetatile(new(x * 16 + 8, y * 16 + 8)) != rom[0xcf00 + y * 16 + x],
                        $"Companion ${id:x2} direction={direction} tile ({x},{y}) differs after native attack probes.");
            }
            StepCompanionRom(actor, rom, id == 0x0d ? 60 : 1, Vector2.Zero, batched, attack: true, edge: true, compare: CompareTiles);
            StepCompanionRom(actor, rom, 75, Vector2.Zero, batched, compare: CompareTiles);
            // The second action must observe the replacements rather than
            // replaying the first action's tile or persistent side effects.
            StepCompanionRom(actor, rom, id == 0x0d ? 60 : 1, Vector2.Zero, batched, attack: true, edge: true, compare: CompareTiles);
            StepCompanionRom(actor, rom, 75, Vector2.Zero, batched, compare: CompareTiles);
        }
    }
}
