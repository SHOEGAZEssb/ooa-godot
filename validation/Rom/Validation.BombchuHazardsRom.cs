using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombchuHazardsRom()
    {
        foreach (bool sideview in new[] { false, true })
        foreach (byte tile in sideview ? new byte[] { 0x1a } : new byte[] { 0xfa, 0xf3, 0x61 })
        foreach (bool batched in new[] { false, true })
        {
            var rom = PrepareBombchuGameplayRom(sideview ? 1 : 0, true, sideview);
            _inventory.GiveTreasure(TreasureId.Bombchus, 0x10);
            EquipSomariaMotionItem(rom, TreasureId.Bombchus, true); rom[0xc6b3] = 0x10;
            rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit(); var seed = _random.CaptureState();
            int update = 0;
            void Compare()
            {
                string context = $"Bombchu hazard side={sideview}, tile=${tile:x2}, batch={batched}, update={++update}";
                var item = _entities.Entities<BombchuItem>().SingleOrDefault();
                bool native = rom[0xd700] != 0 && rom[0xd701] == 0x0d;
                FailIf((item is not null) != native || _inventory.Bombchus != rom[0xc6b3], context + ": child deletion/ammo differs.");
                if (item is not null)
                    FailIf(item.Position != new Vector2(rom[0xd70d], rom[0xd70b]) || (ushort)item.SpeedZ != rom.Word(0xd714) ||
                        item.VerticalFlags != rom[0xd73b] || item.Counter2 != rom[0xd707], context + ": water motion/flags/fuse differs.");
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds), context + ": hazard cue/RNG order differs.");
                CompareSomariaMotionRom(rom, context);
            }
            void Step(int count = 1, bool press = false) =>
                StepSomariaMotionRom(rom, count, batched, 0xff, press ? 1 : 0, press ? 1 : 0, Compare);
            void SetTile(Vector2 point, byte value)
            {
                _currentRoom.SetPositionTileAndCollision(point, value, 0, 0);
                int packed = _currentRoom.GetPackedPosition(point);
                rom[0xcf00 + packed] = value; rom[0xce00 + packed] = 0;
            }
            Step(1, true);
            Vector2 point = _entities.Entities<BombchuItem>().Single().Position + new Vector2(0, 5);
            byte original = _currentRoom.GetMetatile(point);
            SetTile(point, tile); Step();
            if (sideview)
            {
                FailIf(rom[0xd700] == 0 || !sounds.Requests.Contains(SoundId.SndSplash),
                    "Side-view water entry must splash without deleting the Bombchu.");
                Step(2); SetTile(point, original); Step(3);
                FailIf(rom[0xd700] == 0 || sounds.Requests.Count(cue => cue == SoundId.SndSplash) != 2,
                    "Leaving side-view water must splash and retain the Bombchu's physical lifetime.");
            }
            else
            {
                FailIf(rom[0xd700] != 0, "Grounded top-down Bombchu must delete on water/hole/lava contact.");
                if (tile == 0xf3)
                {
                    var effect = _entities.Entities<FallingDownHoleEffect>().Single();
                    int native = Enumerable.Range(0xd0, 16).Select(page => (page << 8) + 0x40)
                        .Single(slot => rom[slot] != 0 && rom[slot + 1] == 0x0f);
                    FailIf(effect.Position != new Vector2(rom[native + 0xd], rom[native + 0xb]) ||
                        rom[native + 7] != 0x0d, "Bombchu hole interaction must retain source position and ITEM$0d identity.");
                }
                SetTile(point, original); Step(12);
                Step(1, true); FailIf(rom[0xd700] == 0, "Hazard deletion must permit another Bombchu after parent completion.");
            }
        }
        GD.Print("Compared Bombchu top-down water/hole/lava deletion and repeat use, retained hole ITEM$0d identity, and side-view water entry/exit splash with live gravity/fuse through split/batched gameplay.");
    }
}
