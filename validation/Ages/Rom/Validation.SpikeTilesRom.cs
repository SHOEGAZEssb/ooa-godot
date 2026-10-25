using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSpikeTilesRom()
    {
        foreach (int ring in new[] { 0xff, (int)RingId.RedLuck, (int)RingId.ArmorL3,
            (int)RingId.Blue, (int)RingId.Cursed, (int)RingId.Protection, (int)RingId.Steadfast })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, 0x65);
            _entities.Clear(); // Isolate terrain from the room's Gibdos.
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    $"Room 5:65 spike fixture could not equip ring ${ring:x2}.");
            }
            var room = _rooms.CurrentRoom;
            // room0565.bin and tileTypeMappings.s:@dungeons/@indoors:
            // eleven passable $60 tiles, including the approach target $24.
            int[] spikes = [0x21, 0x24, 0x27, 0x2a, 0x51, 0x54, 0x57, 0x5a, 0x84, 0x87, 0x8a];
            int[] actualSpikes = Enumerable.Range(0, 11 * 16).Where(pos =>
                (pos & 15) < 15 && room.GetMetatile(new((pos & 15) * 16 + 8, (pos >> 4) * 16 + 8)) == 0x60).ToArray();
            FailIf(!actualSpikes.SequenceEqual(spikes), "Room 5:65 lost its eleven source spike tiles $60.");
            foreach (int pos in spikes)
            {
                TerrainInfo terrain = room.GetTerrainInfo(new((pos & 15) * 16 + 8, (pos >> 4) * 16 + 8));
                FailIf(terrain.Type != TerrainType.Spike || terrain.Collision != 0,
                    $"Room 5:65 tile ${pos:x2} must be passable TILETYPE_SPIKE.");
            }

            var rom = new LinkCollisionRom();
            rom[0xcc2e] = 1;
            rom[0xcc33] = (byte)room.ActiveCollisions;
            rom[0xcc34] = room.TilesetFlags;
            rom[0xcd0a] = 15;
            rom[0xcd0b] = 11;
            rom[0xc6cb] = (byte)ring;
            rom[0xc6aa] = (byte)_inventory.HealthQuarters;
            rom[0xc6ab] = (byte)_inventory.MaxHealthQuarters;
            rom[0xd004] = 1;
            rom[0xd024] = 0x80;
            rom[0xd009] = 0xff;
            for (int y = 0; y < 11; y++)
            for (int x = 0; x < 15; x++)
            {
                TerrainInfo terrain = room.GetTerrainInfo(new(x * 16 + 8, y * 16 + 8));
                rom[0xcf00 + y * 16 + x] = terrain.Tile;
                rom[0xce00 + y * 16 + x] = terrain.Collision;
            }
            _player.WarpTo(new(72, 26));
            FailIf(_collision.Collides(_player.Position), "Room 5:65 spike approach must start on reachable floor above tile $24.");
            rom.Word(0xd00a, 26 * 256);
            rom.Word(0xd00c, 72 * 256);
            var sounds = _sound.AttachPlayRequestAudit();
            int startHealth = _inventory.HealthQuarters;
            int update = 0;
            void Compare()
            {
                rom.Call(LinkCollisionRom.ApplyTile);
                rom.Call(LinkCollisionRom.Probe);
                if (rom[0xd02d] != 0) rom.Call(LinkCollisionRom.Knockback);
                if (rom[0xd02d] == 0)
                {
                    rom[0xd009] = 0x10;
                    rom.Call(LinkCollisionRom.StandardSpeed);
                    rom.Call(LinkCollisionRom.Move, rom[0xd010], 0x10);
                }
                rom[0xd02a] = 0; // updateSpecialObjects clears the publication.
                rom[0xcc00] = (byte)_entities.FrameCounter;
                rom.Call(LinkCollisionRom.Invincibility);
                Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                FailIf(_player.PrecisePosition != expected || _player.HealthQuarters != rom[0xc6aa] ||
                    _player.KnockbackFrames != rom[0xd02d] || _player.InvincibilityFrames != rom[0xd02b] ||
                    sounds.Requests.Count(id => id == 0x5f) != rom.SoundRequests.Count(id => id == 0x5f),
                    $"Room 5:65 spike ring=${ring:x2}, update={update}, batch={batched}: " +
                    $"ROM XY={expected}, HP={rom[0xc6aa]}, recoil={rom[0xd02d]}, inv={rom[0xd02b]}; " +
                    $"runtime XY={_player.PrecisePosition}, HP={_player.HealthQuarters}, recoil={_player.KnockbackFrames}, inv={_player.InvincibilityFrames}.");
                if (update == 0)
                    FailIf(_player.HealthQuarters != startHealth || _player.Position.Y != 27,
                        "Room 5:65 must cross the y+$05 spike boundary before damage applies next update.");
                if (update == 1)
                {
                    int damage = ring == (int)RingId.RedLuck ? 1 : ring == (int)RingId.Protection ? 4 : 2;
                    FailIf(_player.HealthQuarters != startHealth - damage || _player.InvincibilityFrames != 39 ||
                        _player.KnockbackFrames != (ring == (int)RingId.Steadfast ? 4 : 9),
                        "Room 5:65 first spike hit must apply raw -$04, source rings, 40 invincibility updates and same-update recoil.");
                }
                update++;
            }
            StepGameplayUpdates(85, Vector2.Down, batched: batched, afterUpdate: Compare);
            FailIf(rom.SoundRequests.Count(id => id == 0x5f) < 2,
                "Room 5:65 spike approach must repeat after invincibility and recoil complete.");
        }
        GD.Print("Validated room 5:65 spike approach, raw damage/rings, sounds, recoil and repeated contact against ROM tile/damage/movement routines in split and batched gameplay updates.");
    }
}
