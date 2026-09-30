using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionDeparturesRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        {
            ReinitializeGameplayForValidation();
            int room = id == 0x0b ? 0x79 : id == 0x0c ? 0x98 : 0x6b;
            LoadValidationRoom(0, room); _entities.Clear();
            Vector2 link = (from y in Enumerable.Range(1, 6)
                from x in Enumerable.Range(1, 8)
                let point = new Vector2(x * 16 + 8, y * 16 + 8)
                where !_currentRoom.IsSolid(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None
                select point).Last();
            _player.WarpTo(link);
            IRoomEntity actor;
            Vector2 position = id == 0x0b ? new(40, 56) : id == 0x0c ? new(128, 32) : new(72, 56);
            if (id == 0x0b)
            {
                var ricky = _entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(position, 2, 0, room));
                ricky.BeginTingleDeparture(new TingleDatabase().Text(0x2006));
                actor = ricky;
                StepGameplayUpdates(1, Vector2.Zero);
            }
            else if (id == 0x0c)
            {
                _saveData.WriteWramByte(WramAddress.wDimitriState, 0x22);
                actor = _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(position, 2, 0, room));
                StepGameplayUpdates(1, Vector2.Zero);
            }
            else
            {
                actor = _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(position, 2, 0, room,
                    Goodbye: new MooshGoodbyeEventDatabase().Record));
                StepGameplayUpdates(2, Vector2.Zero);
            }
            FailIf(!_dialogue.IsOpen, $"Companion ${id:x2} departure did not reach its dialogue boundary.");
            _dialogue.Close();
            // Dialogue/script execution has its own validations. Start the
            // native special object at that same released-text boundary.
            var rom = new CompanionRom(id, position, 2, _currentRoom);
            rom.WaitForMount(_player.PrecisePosition);
            rom[0xd104] = 0x0a;
            rom[0xd103] = id == 0x0d ? (byte)5 : (byte)3;
            if (id == 0x0b)
            {
                rom[0xd13f] = 3; rom[0xd105] = 1; rom[0xd106] = 8;
                rom.Word(0xd114, -0x180); rom[0xd110] = 0x50;
                rom.SetAnimation(3);
            }
            else rom.SetAnimation(id == 0x0d ? 1 : 0x1e);
            var rng = _random.CaptureState(); rom[0xff94] = rng.Rng1; rom[0xff95] = rng.Rng2;
            int updates = 0;
            var sounds = _sound.AttachPlayRequestAudit();
            bool Finished() => ((IRoomEntityLifetime)actor).Finished;
            // Keep checking after deletion as well: no renewed movement,
            // sound, mount ownership, or reappearance on subsequent updates.
            while (!Finished() && updates < 360)
            {
                StepGameplayUpdates(batched ? 3 : 1, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    if (rom[0xd100] != 0) rom.Update(0xff);
                    else return;
                    updates++;
                    FailIf(Finished() != (rom[0xd100] == 0),
                        $"Companion ${id:x2} departure deletion differs at update {updates}, XY={rom.Position}.");
                    if (!Finished()) CompareCompanionMotion(actor, rom, $"Companion ${id:x2} departure update {updates}");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), $"Companion ${id:x2} departure sounds differ at update {updates}: native=[{string.Join(',', rom.Sounds)}], runtime=[{string.Join(',', sounds.Requests)}].");
                    sounds.Clear();
                });
            }
            FailIf(!Finished(), $"Companion ${id:x2} never finished departing, XY={rom.Position}, state=${rom[0xd103]:x2}.");
            StepGameplayUpdates(6, Vector2.Zero, batched: batched);
            FailIf(_player.CompanionRideActive || sounds.Requests.Count != 0 ||
                _entities.EntityAdapters<IRoomEntity>().Contains(actor), $"Companion ${id:x2} retained ownership after departure.");
            if (id != 0x0c)
                FailIf((_saveData.ReadWramByte(id == 0x0b ? WramAddress.wCompanionStates : WramAddress.wMooshState) & 0x40) !=
                    (rom[id == 0x0b ? 0xc646 : 0xc648] & 0x40), $"Companion ${id:x2} departure save flag differs.");
        }
    }
}
