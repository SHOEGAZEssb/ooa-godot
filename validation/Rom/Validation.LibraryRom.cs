using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Text completion is an external input in these bounded interaction tests.
    // Native Link selection, object eligibility, handlers and scripts execute;
    // compressed text/pixels are covered by the separate dialogue comparisons.
    private SomariaRom CreateLibraryInteractionRom()
    {
        (int Id, int Sub, int Y, int X)[] source = _currentRoom.Id switch
        {
            0xec => [(0x52,3,0x58,0xc0),(0x52,4,0x78,0x48),(0x52,5,0x48,0x28),(0x52,6,0x18,0x88)],
            0xd0 => [(0x3b,7,0x48,0x28),(0xab,0x0e,0x78,0x38),(0x3c,0x0b,0x18,0x48),(0x2a,0,0x60,0xc0),(0x9a,9,0x18,0xc8)],
            0xc8 => [(0x80,8,0x12,0x78),(0x52,1,0x28,0x78)],
            0xe4 => [(0xb4,0,0x84,0x78),(0x80,7,0x42,0x78),(0x52,0,0x58,0x78),(0x52,2,0x58,0x78)],
            _ => throw new InvalidOperationException("Library ROM fixture requires $5:$c8/$d0/$e4/$ec.")
        };
        var actors = _entities.Entities<NpcCharacter>();
        FailIf(!actors.Select(n => (n.Record.Id,n.Record.SubId,(int)n.Position.Y,(int)n.Position.X)).SequenceEqual(source),
            $"Library room $5:${_currentRoom.Id:x2} differs from mainData.s's ordered source placements.");
        var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 2,
            (int)_player.Position.X, (int)_player.Position.Y);
        rom.InitializeLinkGameplay();
        for (int i = 0; i < source.Length; i++)
        {
            int address = 0xd040 + _entities.InteractionSlot(actors[i]) * 0x100;
            rom[address] = 1; rom[address+1] = (byte)source[i].Id; rom[address+2] = (byte)source[i].Sub;
            rom[address+11] = (byte)source[i].Y; rom[address+13] = (byte)source[i].X;
        }
        return rom;
    }

    private void PositionLibraryRomAtPlayer(SomariaRom rom)
    {
        rom.Word(0xd00c, (int)(_player.PrecisePosition.X * 256));
        rom.Word(0xd00a, (int)(_player.PrecisePosition.Y * 256));
        rom[0xd008] = (byte)(_player.FacingVector == Vector2I.Up ? 0 :
            _player.FacingVector == Vector2I.Right ? 1 : _player.FacingVector == Vector2I.Down ? 2 : 3);
    }

    private void CompareLibraryInteractionRom(SomariaRom rom, string context)
    {
        FailIf(_dialogue.IsOpen != (rom[0xcba0] != 0) ||
            _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
            _entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02],
            $"{context}: native text/input masks differ: text={_dialogue.IsOpen}/${rom[0xcba0]:x2}, disabled=${_entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/${rom[0xcc8a]:x2}, menu=${_entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled):x2}/${rom[0xcc02]:x2}.");
        if (_dialogue.IsOpen && _dialogue.SourceTextId is int textId)
            FailIf(textId != (rom[0xcba2] | (rom[0xcba3]-4)<<8),
                $"{context}: active text runtime=TX_{textId:x4}, ROM=TX_{(rom[0xcba2] | (rom[0xcba3]-4)<<8):x4}.");
        foreach (var npc in _entities.Entities<NpcCharacter>())
        {
            if (!npc.Active)
            {
                FailIf(Enumerable.Range(0,16).Any(slot => rom[0xd040+slot*0x100]!=0 &&
                    rom[0xd041+slot*0x100]==npc.Record.Id && rom[0xd042+slot*0x100]==npc.Record.SubId),
                    $"{context}: deleted ${npc.Record.Id:x2}:${npc.Record.SubId:x2} remains allocated in ROM.");
                continue;
            }
            int address = 0xd040 + _entities.InteractionSlot(npc) * 0x100;
            bool alive = rom[address] != 0 && rom[address+1] == npc.Record.Id && rom[address+2] == npc.Record.SubId;
            FailIf(npc.Active != alive, $"{context}: ${npc.Record.Id:x2}:${npc.Record.SubId:x2} slot ${address:x4} lifetime differs.");
            if (!alive) continue;
            FailIf(npc.Visible != ((rom[address+0x1a] & 0x80) != 0) ||
                npc.Record.Id != 0x80 && npc.ObjectCollisionBounds.Size != new Vector2(rom[address+0x27]*2, rom[address+0x26]*2),
                $"{context}: ${npc.Record.Id:x2}:${npc.Record.SubId:x2} visibility/hitbox differs.");
            // The carpenter uses showtextlowindex, not showloadedtext; its
            // unused low byte and the decoration's nonblocking bounds are
            // implementation details rather than interaction state.
            if (npc.Record.Id is not (0x80 or 0xb4 or 0x9a) && !(npc.Record.Id == 0x52 && npc.Record.SubId < 3))
                FailIf(npc.TextId != rom.Word(address+0x32), $"{context}: ${npc.Record.Id:x2}:${npc.Record.SubId:x2} loaded text runtime=${npc.TextId:x4}, ROM=${rom.Word(address+0x32):x4}.");
            // Compare the active native OAM pose and countdown, without a
            // rendered-frame capture or per-glyph image read on every update.
            var animation = OracleGraphicsCache.GetAnimationDefinition(npc.CurrentScriptAnimationSource);
            var frame = animation.Frames[npc.CurrentAnimationFrame];
            int counter = frame.Duration - (int)SomariaPrivate<double>(npc, "_animationTicks");
            var image = ValidationRom.LoadCleanUs();
            int oam = 0x14*0x4000 + (rom.Word(address+0x1e) & 0x3fff);
            int cells = image.Span[oam];
            string encoded = string.Join(';', Enumerable.Range(0, cells).Select(cell =>
                string.Join(',', Enumerable.Range(0,4).Select(b => image.Span[oam+1+cell*4+b]))));
            FailIf(counter != rom[address+0x20] || frame.EncodedOam != encoded,
                $"{context}: ${npc.Record.Id:x2}:${npc.Record.SubId:x2} animation counter/OAM differs: counter={counter}/${rom[address+0x20]:x2}.");
        }
    }

    private void ApproachLibraryActorRom(NpcCharacter actor, bool batch, SomariaRom rom)
    {
        ApproachLibraryActor(actor, batch, movement =>
        {
            int angle = movement == Vector2.Up ? 0 : movement == Vector2.Right ? 8 :
                movement == Vector2.Down ? 16 : movement == Vector2.Left ? 24 : 0xff;
            int held = angle == 0 ? 0x40 : angle == 8 ? 0x10 : angle == 16 ? 0x80 : angle == 24 ? 0x20 : 0;
            rom.UpdateGameplay(held, held, angle, _entities.FrameCounter);
            CompareLibraryInteractionRom(rom, $"Library approach ${actor.Record.Id:x2}:${actor.Record.SubId:x2} batch={batch}");
        }, () => PositionLibraryRomAtPlayer(rom), () =>
        {
            int slot = 0xd040 + _entities.InteractionSlot(actor) * 0x100;
            Vector2I offset = rom[0xd008] switch
            {
                0 => new(0,-10), 1 => new(10,0), 2 => new(0,10), 3 => new(-10,0),
                _ => throw new InvalidOperationException("Native library approach has invalid Link direction.")
            };
            var point = new Vector2I(rom[0xd00d],rom[0xd00b]) + offset;
            return Math.Abs(point.Y-rom[slot+11]) < rom[slot+0x26] &&
                Math.Abs(point.X-rom[slot+13]) < rom[slot+0x27];
        });
    }

    private void CloseLibraryRomText(SomariaRom rom)
    {
        _dialogue.Close(); rom[0xcba0] = 0;
    }

    private void ValidateLibraryReadersRom()
    {
        int fixture = 0;
        foreach (int room in new[] { 0xec, 0xd0 })
        foreach (bool clean in new[] { false, true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetGlobalFlag(0x30, clean); _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x25); _saveData.SetGlobalFlag(0x38, false);
            LoadValidationRoom(5, room);
            var rom = CreateLibraryInteractionRom(); int update = 0;
            void Step(int count = 1, bool press = false) => StepGameplayUpdates(count, Vector2.Zero,
                press ? ["attack"] : [], press ? ["attack"] : [], batch, () =>
                {
                    rom.UpdateGameplay(press ? 1 : 0, press ? 1 : 0, 0xff, _entities.FrameCounter);
                    press = false;
                    CompareLibraryInteractionRom(rom, $"Library $5:${room:x2} clean={clean} batch={batch} update={++update}");
                });
            Step(3);
            // All nine entry snapshots are compared above. Exercise only one
            // past reader and the two distinct present script lifetimes.
            int[] ids = room == 0xec ? [0x52] : [0x2a, 0x9a];
            foreach (int id in ids)
            {
                var actor = _entities.Entities<NpcCharacter>().First(n => n.Record.Id == id);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    if (repeat == 1)
                    {
                        _saveData.SetGlobalFlag(0x30, !clean);
                        for (int address=0xc5b0; address<0xcb00; address++) rom[address] = _saveData.ReadWramByte(address);
                    }
                    ApproachLibraryActorRom(actor, batch, rom);
                    Step(1, press:true); Step(2);
                    FailIf(!_dialogue.IsOpen, $"Library $5:${room:x2} ${id:x2} source A probe did not open text.");
                    int text = rom[0xcba2] | (rom[0xcba3]-4)<<8;
                    string expected = id == 0x9a ? "carpenter" : "reader";
                    FailIf(text != (id == 0x9a ? (repeat == 1 ? !clean : clean) ? 0x2314 : 0x2313 : actor.TextId),
                        $"Library $5:${room:x2} {expected} native selected unexpected TX_{text:x4}.");
                    Step(3); CloseLibraryRomText(rom); Step(3);
                }
                // Restore the starting world input before the next actor.
                _saveData.SetGlobalFlag(0x30, clean);
                for (int address=0xc5b0; address<0xcb00; address++) rom[address] = _saveData.ReadWramByte(address);
            }
        }
        GD.Print("Validated clean-US past/present library initialization, hitboxes/OAM clocks, both pollution snapshots, live carpenter selection, reachable repeated conversations and text freeze/resume.");
    }

    private void ValidateLibraryBookRewardRom()
    {
        int fixture = 0;
        foreach (bool fairy in new[] { true, false })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetGlobalFlag(0x20, fairy); _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
            _saveData.SetRoomFlag(5,0xc8,0x20,false); _inventory.LoseTreasure(0x55);
            LoadValidationRoom(5,0xc8); var rom = CreateLibraryInteractionRom();
            int update=0;
            void Step(int count=1, bool press=false) => StepGameplayUpdates(count,Vector2.Zero,
                press ? ["attack"] : [], press ? ["attack"] : [], batch, () =>
                {
                    rom.UpdateGameplay(press ? 1 : 0,press ? 1 : 0,0xff,_entities.FrameCounter); press=false;
                    string context=$"Book $5:$c8 fairy={fairy} batch={batch} update={++update}";
                    CompareLibraryInteractionRom(rom,context);
                    var giver=_entities.EntityAdapters<LibraryActorRoomEntity>().Single(n=>n.Npc.Record.Id==0x52);
                    int slot=0xd040+_entities.InteractionSlot(giver.Npc)*0x100;
                    FailIf(giver.Host!.Counter!=rom[slot+6] || _saveData.HasRoomFlag(5,0xc8,0x20)!=((rom[0xcac8]&0x20)!=0),
                        $"{context}: source counter/committed flag $20 differs: counter={giver.Host.Counter}/${rom[slot+6]:x2}.");
                    // wObtainedTreasureFlags[$55/8], bit ($55 & 7).
                    FailIf(_inventory.HasTreasure(0x55)!=((rom[0xc6a4]&0x20)!=0),
                        context+": native Book of Seals $55 inventory handoff differs.");
                });
            Step(2); var keeper=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x52);
            ApproachLibraryActorRom(keeper,batch,rom); Step(1,press:true);
            Step(3); CloseLibraryRomText(rom); Step();
            if (!fairy)
            {
                Step(); // The copied-buffer scriptjump yielded before enableinput.
                FailIf(_player.CutsceneControlled || _inventory.HasTreasure(0x55), "Native book giver must loop without fairy flag $20.");
                _saveData.SetGlobalFlag(0x20);
                for(int address=0xc5b0;address<0xcb00;address++)rom[address]=_saveData.ReadWramByte(address);
                ApproachLibraryActorRom(keeper,batch,rom);
                Step(1,press:true); CloseLibraryRomText(rom); Step();
            }
            Step(29); FailIf(_dialogue.IsOpen,"Native book wait opened TX_3309 before update30.");
            Step(); FailIf(!_dialogue.IsOpen,"Native book wait missed TX_3309 on update30.");
            CloseLibraryRomText(rom); Step(32); Step(29);
            FailIf(_inventory.HasTreasure(0x55),"Native book reward occurred before the third wait's zero update.");
            Step();
            // Observe native treasure allocation/collection and the first
            // reward text; its presentation is owned by existing treasure tests.
            for(int wait=0;!_dialogue.IsOpen && wait<35;wait++)Step();
            FailIf(!_dialogue.IsOpen || !_inventory.HasTreasure(0x55),"Native $55 reward/text handoff failed.");
            // treasureObjectData.s:$55 selects TX_004e; text.yaml has this
            // complete message, independently of the generated treasure row.
            FailIf((rom[0xcba2] | (rom[0xcba3]-4)<<8)!=0x004e ||
                DialogueBox.PlainText(_dialogue.CurrentMessage).Replace("\n"," ")!="You got the Book of Seals!",
                "Book of Seals $55 reward lost source TX_004e or its complete message.");
            Step(3); CloseLibraryRomText(rom); Step(8); Step(1,press:true); Step(2);
            FailIf(!_dialogue.IsOpen || (rom[0xcba2] | (rom[0xcba3]-4)<<8)!=0x330a,
                "Native book giver failed its already-given TX_330a repeat branch.");
            LoadValidationRoom(5,0xd0);
            FailIf(_player.CutsceneControlled || _dialogue.IsOpen,"Book ROM fixture cancellation retained modal control.");
        }
        GD.Print("Validated clean-US Book of Seals live fairy gate, three wait30 boundaries, flag/decorative-book order, native reward allocation/collection and completed repeat through gameplay.");
    }

    private void ValidateLibraryPodiumPathRom()
    {
        foreach(bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation(); _inventory.EquipA(0); _inventory.EquipB(0);
            _saveData.SetRoomFlag(5,0xe4,0x40); _saveData.SetRoomFlag(5,0xe4,0x20);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame,false); LoadValidationRoom(5,0xe4);
            var rom=CreateLibraryInteractionRom(); int update=0;
            void Step(int count=1,bool press=false) => StepGameplayUpdates(count,Vector2.Zero,
                press ? ["attack"] : [],press ? ["attack"] : [],batch,()=>
                {
                    rom.UpdateGameplay(press ? 1 : 0,press ? 1 : 0,0xff,_entities.FrameCounter);press=false;
                    rom.AdvanceTileGraphics();
                    string context=$"Podium $5:$e4 batch={batch} update={++update}";
                    CompareLibraryInteractionRom(rom,context);
                    FailIf(_entities.RuntimeState.ReadWramByte(0xcfd0)!=rom[0xcfd0],context+": shared book selection $cfd0 differs.");
                    foreach(var actor in _entities.EntityAdapters<LibraryActorRoomEntity>().Where(n=>n.Npc.Record.Id==0xb4))
                    {
                        int slot=0xd040+_entities.InteractionSlot(actor.Npc)*0x100;
                        FailIf(actor.Host!.State!=rom[slot+4] || actor.Host.State>=3 && actor.Host.Counter!=rom[slot+6],
                            $"{context}: book ${actor.Npc.Record.SubId:x2} native state/counter differs.");
                    }
                    for(int row=0;row<_currentRoom.HeightInTiles;row++)
                    for(int col=0;col<_currentRoom.WidthInTiles;col++)
                    {
                        int packed=row*16+col;
                        FailIf(_currentRoom.GetPackedStorageMetatile((byte)packed)!=rom[0xcf00+packed],
                            $"{context}: native path tile ${packed:x2} differs.");
                    }
                });
            Step(3);
            var book=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0xb4&&n.Record.SubId==5);
            ApproachLibraryActorRom(book,batch,rom); Step(1,press:true);
            Step(3); CloseLibraryRomText(rom); Step(22);
            FailIf(_player.CutsceneControlled,"Native final-book path must release control after its terminator interval.");
            ApproachLibraryActorRom(book,batch,rom); Step(1,press:true); Step(3);
            CloseLibraryRomText(rom); Step(3);
            FailIf(_entities.EntityAdapters<LibraryActorRoomEntity>().Single(n=>n.Npc==book).Host!.State != 4,
                "Repeated active book must retain its built path state $04.");
        }
        GD.Print("Validated clean-US past-library child allocation, final-book two-update path writes/terminator, text hold, shared selection, masks and repeated use.");
    }
}
