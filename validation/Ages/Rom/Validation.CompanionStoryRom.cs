using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionDeparturesRom()
    {
        int hostCase1 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool batched in RomHostSchedules(hostCase1++))
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
            CompanionRom? initializedRicky=null;
            Vector2 position = id == 0x0b ? new(40, 56) : id == 0x0c ? new(128, 32) : new(56, 72);
            // These trajectory comparisons start with an already initialized
            // source actor, not a fresh state0 birth (covered separately).
            _runtimeState.SetWramByte(0xd104, id == 0x0d ? (byte)0 : (byte)1);
            if (id == 0x0b)
            {
                _saveData.WriteField("wRickyState",0x80);
                initializedRicky=new CompanionRom(id,position,2,_currentRoom);
                initializedRicky.InitializeRemembered(link,new(40,40),occupied:false);
                initializedRicky.Update(0xff);initializedRicky.Update(0xff);
                FailIf(initializedRicky[0xd104]!=1 || initializedRicky.Position!=position,
                    "Departure fixture must execute native Ricky state0 at the room's actual source floor.");
                for(int at=0xd100;at<0xd140;at++)_runtimeState.SetWramByte(at,initializedRicky[at]);
                _entities.BindNativeSpecialObjectPresentation(id,new RickyCompanionSpawn(position,2,0,room));
                var ricky=_entities.Entities<RickyCompanionRoomEntity>().Single();
                _runtimeState.SetWramByte(0xd103,2);_runtimeState.SetWramByte(0xd104,0x0a);
                _runtimeState.SetField("wDisabledObjects",1);_runtimeState.SetField("wMenuDisabled",1);
                actor = ricky;
                StepGameplayUpdates(1, Vector2.Zero);
            }
            else if (id == 0x0c)
            {
                _saveData.WriteWramByte(AgesWramAddress.wDimitriState, 0x22);
                actor = _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(position, 2, 0, room));
                _entities.AddEntity(new DimitriDepartureRoomEntity(_rooms,_entities,new()));
                for(int i=0;i<10 && !_dialogue.IsOpen;i++)StepGameplayUpdates(1, Vector2.Zero);
            }
            else
            {
                _inventory.GiveTreasure(0x52, 0); _saveData.WriteWramByte(0xc648, 0x20); _saveData.CommitInventoryChange();
                var goodbye = _roomEvents.Get<MooshGoodbyeEvent>();
                goodbye.SpawnPreset();
                actor = goodbye.Body ?? throw new System.InvalidOperationException("The actual $67:$01 producer did not create its native Moosh body.");
                StepGameplayUpdates(3, Vector2.Zero);
            }
            FailIf(!_dialogue.IsOpen, $"Companion ${id:x2} departure did not reach its dialogue boundary.");
            _dialogue.Close();
            if(id==0x0c)StepGameplayUpdates(1,Vector2.Zero);
            // Dialogue/script execution has its own validations. Start the
            // native special object at that same released-text boundary.
            var rom = initializedRicky??new CompanionRom(id, position, 2, _currentRoom);
            rom.WaitForMount(_player.PrecisePosition);
            rom[0xd104] = 0x0a;
            if(id==0x0b)rom[0xcc8a]=rom[0xcc02]=1;
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
            byte[] observed = new byte[4], expected = new byte[4];
            int publicationVisits = 0, probeVisits = 0;
            if (id is 0x0b or 0x0d)
            {
                var probe = new PartMovementScratchProbe(() =>
                { probeVisits++; for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i); });
                _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            }
            void SeedPublication()
            {
                if (id is not (0x0b or 0x0d)) return;
                for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); rom[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); }
            }
            SeedPublication();
            if(id==0x0b)
            {
                StepCompanionRom(actor,rom,1,Vector2.Zero,batched);
                int heldCounter=rom[0xd106],heldFrame=rom[0xd120];
                _runtimeState.SetField("wDisabledObjects",0x21);rom[0xcc8a]=0x21;
                StepCompanionRom(actor,rom,3,Vector2.Zero,batched);
                FailIf(rom[0xd106]!=heldCounter || rom[0xd120]!=heldFrame,
                    "Ages rickyStateA must retain its native counter and animation through a companion-mask hold.");
                _runtimeState.SetField("wDisabledObjects",1);rom[0xcc8a]=1;
                probeVisits=0;SeedPublication();
            }
            // Keep checking after deletion as well: no renewed movement,
            // sound, mount ownership, or reappearance on subsequent updates.
            while (!Finished() && updates < 360)
            {
                StepGameplayUpdates(batched ? 3 : 1, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    bool flight = rom[0xd104] == 0x0a &&
                        (id == 0x0d && rom[0xd103] == 6 || id == 0x0b && rom[0xd103] == 5);
                    if (rom[0xd100] != 0) rom.Update(0xff, afterLink: () =>
                    { for (int i = 0; i < 4; i++) expected[i] = rom[0xcec0 + i]; });
                    else return;
                    updates++;
                    FailIf(Finished() != (rom[0xd100] == 0),
                        $"Companion ${id:x2} departure deletion differs at update {updates}, XY={rom.Position}.");
                    if (!Finished()) CompareCompanionMotion(actor, rom, $"Companion ${id:x2} departure update {updates}");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), $"Companion ${id:x2} departure sounds differ at update {updates}: native=[{string.Join(',', rom.Sounds)}], runtime=[{string.Join(',', sounds.Requests)}].");
                    sounds.Clear();
                    if (flight)
                    {
                        FailIf(probeVisits != 1 || !observed.SequenceEqual(expected),
                            $"Companion${id:x2} departure visit{updates}: native gravity/no-motion and planar scratch differ before PARTs.");
                        publicationVisits++;
                    }
                    probeVisits = 0; SeedPublication();
                });
            }
            FailIf(!Finished(), $"Companion ${id:x2} never finished departing, XY={rom.Position}, state=${rom[0xd103]:x2}.");
            if(id==0x0b)
            {
                for(int at=0xd100;at<0xd140;at++)FailIf(_runtimeState.ReadWramByte(at)!=rom[at],
                    $"Ricky departure must retain native itemDelete's cleared slot byte${at:x4}.");
                var remembered=CompanionRuntimeState.ReadRemembered(_runtimeState);
                FailIf(_runtimeState.ReadField("wDisabledObjects")!=rom[0xcc8a] || _runtimeState.ReadField("wMenuDisabled")!=rom[0xcc02] ||
                    remembered.Id!=rom[0xcc24] || remembered.Group!=rom[0xcc25] || remembered.Room!=rom[0xcc26] ||
                    remembered.Y!=rom[0xcc27] || remembered.X!=rom[0xcc28] ||
                    _player.LocalRespawnPosition!=new Vector2(rom[0xcc22],rom[0xcc21]) ||
                    _runtimeState.ReadField("wLinkLocalRespawnDir")!=rom[0xcc23],
                    "Ages departure must release native masks and copy the cleared slot through saveLinkLocalRespawnAndCompanionPosition; "+
                    $"masks {_runtimeState.ReadField("wDisabledObjects"):x2}/{_runtimeState.ReadField("wMenuDisabled"):x2} vs {rom[0xcc8a]:x2}/{rom[0xcc02]:x2}, "+
                    $"remembered {remembered} vs {rom[0xcc24]:x2}/{rom[0xcc25]:x2}/{rom[0xcc26]:x2}/{rom[0xcc27]:x2}/{rom[0xcc28]:x2}, "+
                    $"local {_player.LocalRespawnPosition}/{_runtimeState.ReadField("wLinkLocalRespawnDir"):x2} vs {rom[0xcc22]:x2}/{rom[0xcc21]:x2}/{rom[0xcc23]:x2}.");
            }
            FailIf(id == 0x0d && publicationVisits != 20 + 0xf0 - 0x48,
                "Moosh's source preset must observe all20 gravity and168 retained-height planar flight visits.");
            FailIf(id == 0x0b && publicationVisits < 20, "Ricky departure must observe its complete native cliff arc, including the landing move.");
            StepGameplayUpdates(6, Vector2.Zero, batched: batched);
            FailIf(_player.CompanionRideActive || sounds.Requests.Count != 0 ||
                _entities.EntityAdapters<IRoomEntity>().Contains(actor), $"Companion ${id:x2} retained ownership after departure.");
            if (id != 0x0c)
                FailIf((_saveData.ReadWramByte(id == 0x0b ? AgesWramAddress.wCompanionStates : AgesWramAddress.wMooshState) & 0x40) !=
                    (rom[id == 0x0b ? 0xc646 : 0xc648] & 0x40), $"Companion ${id:x2} departure save flag differs.");
        }
    }
}
