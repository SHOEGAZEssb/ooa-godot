using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareFallingFireContactRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool shield in new[] { false, true })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wLinkMaxHealth, 0x40);
            save.WriteWramByte(WramAddress.wLinkHealth, 0x40);
            InitializeTransientSession(save);
            if (shield) { _inventory.GiveTreasure(TreasureId.Shield, 3); _inventory.EquipA(TreasureId.Shield); }
            else _inventory.EquipA(0);
            _inventory.EquipB(0);
            // Declare a child-contact boundary on original clear top-down
            // floor. The actual side-view room's emitters, pool ordering and
            // complete child falls have their separate source-room comparison.
            LoadValidationRoom(0, 0x33); _entities.Clear();
            Vector2 start = (from y in Enumerable.Range(3, 4) from x in Enumerable.Range(2, 6)
                let point = new Vector2(x * 16 + 8, y * 16 + 8)
                where _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None &&
                    Enumerable.Range(-6, 13).All(dx => Enumerable.Range(-6, 19).All(dy =>
                        !_currentRoom.IsSolid(point + new Vector2(dx, dy)))) select point).First();
            _player.WarpTo(start); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            Vector2 spawn = start + new Vector2(0, -32);
            _entities.TryCreateFallingFire(spawn);
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)start.X, (int)start.Y) { HostilePartsEnabled = true };
            native[0xd0c0] = 1; native[0xd0c1] = 0x23; native[0xd0c2] = 2;
            native[0xd0cb] = (byte)spawn.Y; native[0xd0cd] = (byte)spawn.X;
            native.InitializeLinkGameplay();
            native.InitializeLinkWalkingAnimation();
            int updates = 0;
            bool contact = false;
            StepGameplayUpdates(100, Vector2.Zero, MenuRomActions(shield ? 1 : 0), MenuRomActions(shield ? 1 : 0), batched, () =>
            {
                native.UpdateGameplay(shield && updates == 0 ? 1 : 0, shield ? 1 : 0, 0xff, _entities.FrameCounter);
                updates++;
                string context = $"PART$23 contact shield={shield}, batch={batched}, update={updates}";
                if (native[0xd02d] != 0) contact = true;
                FailIf((_player.PendingContactDamageRaw & 255) != native[0xd025] ||
                    (((int)_player.InvincibilityFrames) & 255) != native[0xd02b] ||
                    (int)_player.KnockbackFrames != native[0xd02d] ||
                    _player.Position != new Vector2(native[0xd00d], native[0xd00b]),
                    context + $": Link damage/recoil/XY differs (runtime={_player.PendingContactDamageRaw & 255:x2}/{_player.InvincibilityFrames}/{_player.KnockbackFrames}/{_player.Position}; native={native[0xd025]:x2}/{unchecked((sbyte)native[0xd02b])}/{native[0xd02d]}/{native[0xd00d]},{native[0xd00b]}).");
                FailIf(_saveData.ReadWramByte(WramAddress.wLinkHealth) != native[WramAddress.wLinkHealth],
                    context + ": applied health differs.");
                for (int slot = 0; slot < 16; slot++)
                {
                    var fire = _entities.ResolvePartSlot(slot) as FallingFireRoomEntity;
                    int address = 0xd0c0 + slot * 0x100;
                    FailIf((fire is null) != (native[address] == 0), context + ": fire lifetime differs.");
                    if (fire is not null)
                        FailIf(fire.State != native[address + 4] || fire.Counter != native[address + 6] ||
                            fire.YFixed != native.Word(address + 10), context + ": emission/fall after contact differs.");
                }
            });
            FailIf(!contact || shield && _inventory.HealthQuarters != 0x40,
                "Falling fire must exercise source body/shield contact and preserve shield-protected health.");
        }
    }
}
