using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHeartRingSideViewRom()
    {
        FieldInfo counter = typeof(Player).GetField("_heartRingDistanceFixed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int hostCase = 0;
        foreach (int ring in new[] { 0x13, 0x14 })
        foreach (int terrain in Enumerable.Range(0, 5))
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, 0x14); save.WriteWramByte(0xc6ab, 0x30);
            InitializeTransientSession(save);
            int group = terrain >= 3 ? 7 : 6;
            int room = terrain == 2 ? 0x10 : terrain >= 3 ? 0x05 : 0x97;
            LoadValidationRoom(group, room); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.RingBox, 1); _inventory.GrantAppraisedRingForDebug(ring);
            FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                $"Could not equip Heart Ring ${ring:x2} in side-view fixture.");
            _inventory.GiveTreasure(TreasureId.Feather, 1);
            bool primary = (terrain & 1) == 0;
            _inventory.EquipA(primary ? TreasureId.Feather : 0); _inventory.EquipB(primary ? 0 : TreasureId.Feather);
            if (terrain >= 3) _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (terrain == 4) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            if (terrain < 2)
                for (int y = 8; y < _currentRoom.Height; y += 16)
                for (int x = 8; x < _currentRoom.Width; x += 16)
                    _currentRoom.SetPositionTileAndCollision(new(x, y),
                        y >= 136 ? (byte)(terrain == 1 ? 0x20 : 0x26) : (byte)0,
                        y >= 136 ? (byte)0x0f : (byte)0, 0);
            Vector2 start = terrain == 2 ? new(200.25f, 136.5f) : terrain >= 3 ? new(40.25f, 56.5f) : new(104.25f, 121.5f);
            _player.WarpTo(start);
            FailIf(_collision.Collides(start), "Heart Ring side-view fixture must start in reachable open geometry.");
            var rom = new SideViewRom(_saveData, _random.CaptureState(), _currentRoom, start, _entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit();
            int previousHeld = 0, update = 0;
            void SeedCounter(int value)
            {
                counter.SetValue(_player, value);
                rom[0xcc53] = (byte)value; rom[0xcc54] = (byte)(value >> 8); rom[0xcc55] = (byte)(value >> 16);
            }
            void Step(int count = 1, int angle = 0xff, bool feather = false)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y < 0 ? 0x40 : movement.Y > 0 ? 0x80 : 0) | (feather ? primary ? 1 : 2 : 0);
                int edge = held & ~previousHeld; previousHeld = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.Update(angle, edge, held); edge = 0;
                    string context = $"Heart Ring side-view ${group:x1}:${room:x2} ring=${ring:x2} terrain={terrain} batch={batched} update={++update}";
                    CompareSideViewRom(rom, context);
                    int nativeCounter = rom.Word(0xcc53) | rom[0xcc55] << 16;
                    FailIf((int)counter.GetValue(_player)! != nativeCounter,
                        context + $": movement eligibility/distance differs: runtime={(int)counter.GetValue(_player)!:x6}, native={nativeCounter:x6}.");
                    FailIf(_saveData.ReadWramByte(0xc65f) != rom[0xc65f] || _saveData.ReadWramByte(0xc660) != rom[0xc660] ||
                        _inventory.HasTreasure(TreasureId.HeartRefill) != ((rom[0xc69f] & 2) != 0),
                        context + ": refill treasure flag/Gasha maturity differs.");
                    FailIf(_player.KnockbackFrames != rom[0xd02d] ||
                        _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]), context + ": recoil/invincibility boundary differs.");
                    FailIf(!sounds.Requests.Where(id => id is not (SoundId.SndText or SoundId.SndGainHeart or SoundId.SndDamageLink))
                        .SequenceEqual(rom.Sounds.Where(id => id != SoundId.SndDamageLink)),
                        context + ": item/movement cues differ (HUD and declared contact caller excluded).");
                });
            }
            int threshold = (ring == 0x13 ? 2 : 3) << 16;
            int travel = terrain == 2 ? 0 : 8, reverse = terrain == 2 ? 16 : 24;
            Step(); SeedCounter(threshold - 0x100); Step(3, travel);
            _dialogue.ShowMessage("Heart Ring side-view pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3, travel); _dialogue.Close(); rom[0xcba0] = 0;
            Step(8, reverse); Step(3);
            if (terrain < 2)
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    Step(3, travel); SeedCounter(threshold - 1);
                    Step(1, travel, feather: true);
                    Step(5, reverse);
                    for (int wait = 0; (rom[0xcc5c] & 15) != 0 && wait < 60; wait++) Step(angle: reverse);
                    FailIf((rom[0xcc5c] & 15) != 0, "Heart Ring side-view Feather must land within its bounded native arc.");
                    Step(2, reverse); Step();
                }
            SeedCounter(threshold - 1); Step(3, travel); Step(3);
            _inventory.RefillHealth(); rom[0xc6aa] = rom[0xc6ab];
            SeedCounter(threshold - 0x100); Step(3, reverse); Step();
            if (terrain == 0 || ring == 0x14 && terrain == 1)
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    int recoilAngle = repeat == 0 ? 24 : 8;
                    SeedCounter(threshold - 1);
                    FailIf(!_player.ApplyEnemyContactDamage(_player.Position - OracleObjectMovement.Shared.Direction(recoilAngle) * 16, 1),
                        "Heart Ring side-view repeated contact must pass the normal damage gate.");
                    rom[0xd02a] = 0x80; rom[0xd02b] = 0x22; rom[0xd02c] = (byte)recoilAngle; rom[0xd02d] = 0x0f;
                    rom.ApplyLinkDamage(0xfe); rom[0xd02a] = 0;
                    Step(4, recoilAngle);
                    _dialogue.ShowMessage("Heart Ring side-view recoil pause.", _player.Position.Y); rom[0xcba0] = 1;
                    Step(3, recoilAngle); _dialogue.Close(); rom[0xcba0] = 0;
                    Step(11, recoilAngle); Step(20); Step(2, recoilAngle);
                }
        }
        GD.Print("Validated clean-US Heart Ring L1/L2 side-view movement, normal/ice floor, repeated A/B Feather arcs and landing, ladder axes, Flippers/Mermaid swimming rejection, recoil/recovery, idle/text/full-health and treasure/maturity effects through split/batched gameplay.");
    }
}
