using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombchuTargetRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            var rom = PrepareSomariaMotionRom(0, primary: true);
            _inventory.GiveTreasure(TreasureId.Bombchus, 0x10); EquipSomariaMotionItem(rom, TreasureId.Bombchus, true);
            rom[0xc6b3] = 0x10; rom.InitializeLinkWalkingAnimation();
            var target = new DeclaredBombchuTarget(_player.Position + new Vector2(0, -32));
            _entities.RegisterEnemySlot(target, 0); _entities.AddEntity(target);
            void PublishTarget()
            {
                rom[0xd080] = (byte)(target.Finished ? 0 : 1);
                if (target.Finished) return;
                rom[0xd081] = (byte)target.Id;
                rom[0xd09a] = (byte)(target.Node.Visible ? 0x80 : 0); rom[0xd0a9] = (byte)target.Health;
                rom[0xd08b] = (byte)target.Node.Position.Y; rom[0xd08d] = (byte)target.Node.Position.X;
                rom[0xd0a6] = rom[0xd0a7] = 4;
            }
            int update = 0;
            void Compare()
            {
                var item = _entities.Entities<BombchuItem>().Single();
                const int slot = 0xd700;
                FailIf(item.ItemState != rom[slot + 4] || item.Counter1 != rom[slot + 6] || item.Counter2 != rom[slot + 7] ||
                    item.Position != new Vector2(rom[slot + 0xd], rom[slot + 0xb]) || item.SpeedRaw != rom[slot + 0x10] ||
                    item.Radius != rom[slot + 0x26] || item.Steering!.Angle != rom[slot + 9],
                    $"Bombchu declared target batch={batched}, update={++update}: native capture/wait/chase/reference differs.");
                if (item.TargetSlot >= 0) FailIf(rom.Word(slot + 0x18) != 0xd080, "Bombchu must retain the original ENEMY slot reference.");
            }
            void Step(int count = 1, bool press = false)
            {
                PublishTarget();
                StepSomariaMotionRom(rom, count, batched, 0xff, press ? 1 : 0, press ? 1 : 0, Compare);
            }
            Step(1, true); Step();
            FailIf(_entities.Entities<BombchuItem>().Single().ItemState != 3, "Bombchu must capture the declared visible target before moving.");
            Step(3);
            target.Finished = true;
            // enemyDelete clears all $40 bytes. Declare that enemy-owned
            // boundary while the captured item's wait prevents a health read.
            for (int offset = 0; offset < 0x40; offset++) rom[0xd080 + offset] = 0;
            Step();
            FailIf(_entities.FindFreeEnemySlot() != 0, "Finished target must release its actual ENEMY slot before reuse.");
            target = new DeclaredBombchuTarget(_player.Position + new Vector2(16, -48)) { Id = 0, Health = 8 };
            target.Node.Visible = false;
            _entities.RegisterEnemySlot(target, 0); _entities.AddEntity(target);
            Step(7);
            FailIf(rom[0xd704] != 3 || rom[0xd706] != 1, "Bombchu must retain the target wait through counter$01.");
            Step();
            FailIf(rom[0xd704] != 4 || rom[0xd706] != 10, "Bombchu target wait falls through into chase on its zero update.");
            target.Node.Visible = false; target.Node.Position += new Vector2(16, -16);
            Step(4); // Visibility is a capture gate, not a retained-target gate.
            target.Id = 0x32; // A retained target's new ID is not rechecked against bombchuTargets.
            Step(4);
            target.Health = 0; Step();
            FailIf(rom[0xd704] != 0xff || rom[0xd720] != 4 || _entities.Entities<BombchuItem>().Single().AnimationCounter != 4,
                "A dead retained target starts the explosion without advancing its first frame.");
            _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems();
        }
        GD.Print("Compared declared live ENEMY fields through Bombchu gameplay: capture, twelve-update wait with actual slot retirement/reuse and zero-update chase, retained invisible/transformed target and dead-target explosion. Enemy AI/combat remains separate.");
    }
}
