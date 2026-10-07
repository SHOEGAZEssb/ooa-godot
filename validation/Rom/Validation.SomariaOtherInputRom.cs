using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaOtherInput()
    {
        // itemUsageTables.s: Shield selector5, Boomerang selector2,
        // Bracelet selector3/priority$10 and Cane selector3/priority$00.
        // Shield retains its slot while checkNoOtherParentItemsInUse waits.
        foreach (int other in new[] { TreasureId.Shield, TreasureId.Boomerang, TreasureId.Bracelet })
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            SomariaRom rom = PrepareSomariaMotionRom(2, primary: primary);
            _inventory.GiveTreasure(other, 1);
            if (primary) _inventory.EquipB(other); else _inventory.EquipA(other);
            for (int address = 0xc600; address < 0xc800; address++) rom[address] = _saveData.ReadWramByte(address);
            rom.InitializeLinkWalkingAnimation();
            var seed = _random.CaptureState(); var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, competing = button ^ 3, updates = 0;
            void Compare()
            {
                string context = $"Cane/ITEM${other:x2}, A-Cane={primary}, batch={batched}, update={++updates}";
                var cane = _entities.Somaria!;
                bool parent = rom[0xd200] != 0 && rom[0xd201] == 4;
                bool weapon = rom[0xd600] != 0 && rom[0xd601] == 4;
                FailIf(cane.Active != parent || (cane.Weapon != null) != weapon,
                    context + ": Cane parent/reserved weapon ownership differs.");
                if (parent)
                    FailIf(cane.Parent!.Parameter != rom[0xd221] || SomariaPrivate<int>(cane.Parent, "_counter") != rom[0xd220],
                        context + ": Cane animation clock/parameter differs.");
                if (weapon) FailIf(cane.Weapon!.State != rom[0xd604], context + ": Cane creation state differs.");
                FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0) ||
                    SomariaPrivate<int>(_player, "_shieldParentButton") != (rom[0xd500] == 0 ? 0 : rom[0xd503]) ||
                    SomariaPrivate<bool>(_player, "_shieldParentInitialized") != (rom[0xd504] != 0),
                    context + ": retained Shield slot/initialization/raise differs.");
                int boomParent = Enumerable.Range(0xd3, 2).Select(page => page << 8)
                    .FirstOrDefault(slot => rom[slot] != 0 && rom[slot + 1] == 6);
                FailIf(_player.IsUsingBoomerang != (boomParent != 0) || boomParent != 0 &&
                    (_entities.BoomerangParent.Counter != rom[boomParent + 0x20] ||
                     _entities.BoomerangParent.Parameter != rom[boomParent + 0x21]),
                    context + ": Boomerang parent clock/lifetime differs.");
                int[] slots = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                    .Where(slot => rom[slot] != 0 && rom[slot + 1] == 6).ToArray();
                var children = _entities.Entities<BoomerangItem>();
                FailIf(children.Count != slots.Length, context + ": Boomerang allocation/deletion differs.");
                for (int index = 0; index < children.Count; index++)
                {
                    var child = children[index]; int slot = slots[index];
                    FailIf(child.State != rom[slot + 4] || child.Counter != rom[slot + 6] || child.Angle != rom[slot + 9] ||
                        child.ZHigh != unchecked((sbyte)rom[slot + 0xf]) || child.PrecisePosition !=
                            new Vector2(rom.Word(slot + 0xc) / 256f, rom.Word(slot + 0xa) / 256f) ||
                        child.Visible != ((rom[slot + 0x1a] & 0x80) != 0) ||
                        child.CollisionEnabled != ((rom[slot + 0x24] & 0x80) != 0),
                        context + ": Boomerang physical state/flight/collision differs.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.SequenceEqual(rom.Sounds), context + ": ordered cues/shared RNG differ.");
            }
            void Step(int count = 1, int held = 0, int pressed = 0) =>
                StepSomariaMotionRom(rom, count, batched, held: held, pressed: pressed, afterUpdate: Compare);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.ClearPhysicalPlayerItems(); _player.WarpTo(_player.PrecisePosition);
                rom.ClearPhysicalItems(); Step(); Step(1, 3, 3);
                if (other == TreasureId.Bracelet)
                {
                    FailIf(caneActive() || _bracelet.State != BraceletState.SeekingWall,
                        "Bracelet priority$10 must win over Cane$00 on actual open floor.");
                    Step(3, competing);
                    Step(1, 3, button);
                    FailIf(caneActive() || _bracelet.State != BraceletState.SeekingWall,
                        "Held empty-handed Bracelet must reject a later Cane edge.");
                    Step(1, competing); Step(1, button, button);
                    FailIf(caneActive() || _bracelet.State != BraceletState.Idle,
                        "Bracelet release occurs after allocation and cannot admit Cane on the same update.");
                    Step(); Step(1, button, button); Step(23);
                }
                else
                {
                    FailIf(!caneActive() || _player.IsUsingShield ||
                        _player.IsUsingBoomerang != (other == TreasureId.Boomerang),
                        "Cane must coexist with Boomerang and suppress an already held Shield.");
                    Step(9, competing);
                    FailIf(!caneActive() || _player.IsUsingBoomerang || _player.IsUsingShield,
                        "Boomerang parent completion must retain Cane's remaining swing lock.");
                    if (repeat == 0)
                    {
                        _dialogue.ShowMessage("Cane input pause.", _player.Position.Y); rom[0xcba0] = 1;
                        Step(3, competing); _dialogue.Close(); rom[0xcba0] = 0;
                    }
                    Step(9, competing);
                    FailIf(caneActive() || _player.IsUsingShield != (other == TreasureId.Shield),
                        "Cane completion must permit a retained Shield without a fresh edge.");
                    Step(5, competing);
                }
                FailIf(_entities.Entities<SomariaBlock>().Count != 1 || rom.Blocks.Length != 1,
                    "Completed Cane must leave one physical block after the competing input resolves.");
            }
            bool caneActive() => _entities.Somaria!.Active;
        }
        GD.Print("Compared native Cane/Bracelet priority and release, Boomerang coexistence/flight, retained Shield raise, exact creation/completion, dialogue, full Link/room and sounds/RNG across A/B, repeat and split/batched gameplay.");
    }
}
