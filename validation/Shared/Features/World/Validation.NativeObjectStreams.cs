using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckNativeObjectStreams(FrontendValidationRoot root)
    {
        var rooms=root._gameplaySession!.Rooms;
        var entities=root._seasonsOpening?.Entities ?? root._entities;
        var game=rooms.SaveData.Game;
        var database=NativeObjectStreamDatabase.For(rooms.AssetRoot);
        var memory=OracleRuntimeState.CreateInitialized(game);
        var room=rooms.GetRoom(0,game==OracleGame.Ages?0x98:0xc9);
        byte floor=game==OracleGame.Ages?(byte)0xa0:(byte)0x04;
        for (int y=0; y<room.HeightInTiles; y++)
        for (int x=0; x<room.WidthInTiles; x++) room.SetPositionTileAndCollision(new(x*16+8,y*16+8),floor,0,0);
        string[] entries=game==OracleGame.Ages ? ["targetCartCrystals","group4MapabObjectData","group0Map36ObjectData","group4Mapd2ObjectData","group4Mapd3ObjectData"] :
            ["objectData_sandPuffsFromShipDigging","objectData7e4a","group0Map98ObjectData","group4Map0bObjectData"];
        int cases=0;
        foreach (string label in entries)
        foreach (int occupied in new[] {0,15,16})
        foreach (int gate in new[] {0,1,0x80,0x81,0x82,0x83})
        foreach (int direction in new[] {0,1,2,3})
        foreach (byte load in new byte[] {0,2})
        foreach (byte flags in new byte[] {0,0x80})
        {
            var native=new BraceletRom(room,new(136,24),2,0xff,game:game);
            var save=OracleSaveData.CreateStandardGame(game);
            save.SetRoomFlag(0,room.Id,0x80,flags!=0); native.LoadSave(save);
            native[0xcc2d]=0; native[0xcc05]=load; native[0xcc85]=(byte)gate; native[0xcd02]=(byte)direction;
            native[0xcc32]=1; native[0xcd00]=0; native[0xcc4a]=0xf0;
            native[0xff94]=0x37; native[0xff95]=0x0d; native.GeneratePlacementBuffer();
            for (int index=0; index<0x20; index++) native[0xcec0+index]=0;
            for (int slot=0; slot<occupied; slot++) native[0xd080+slot*256]=native[0xd0c0+slot*256]=1;
            if (game==OracleGame.Ages) memory.SetField("wcc05",load);
            memory.SetField("wcc85",(byte)gate); memory.SetField("wScreenTransitionDirection",(byte)direction);
            memory.SetField("wRoomStateModifier",1);
            var reader=new NativeObjectStreamReader(database,memory,()=>flags);
            var enemies=new List<(int Id,int Sub,int Var,int Index)>();
            var interactions=new List<(int Id,int Sub,int Var)>();
            var parts=new List<(int Id,int Sub,int Var)>();
            int killable=0, count=0, reservations=0;
            foreach (var instruction in reader.Read(database.Entry(label)))
            {
                if (instruction.Opcode is 1 or 2 || instruction.Opcode==9 && instruction.Flags==0)
                {
                    if (interactions.Count==14) { reader.AllocationFailed(); continue; }
                    interactions.Add((instruction.Id,instruction.SubId,instruction.Var03)); continue;
                }
                if (instruction.Opcode==8 || instruction.Opcode==9 && instruction.Flags==2)
                {
                    if (parts.Count+occupied==16) { reader.AllocationFailed(); continue; }
                    parts.Add((instruction.Id,instruction.SubId,instruction.Var03));
                    if (instruction.Opcode==8) reservations++;
                    continue;
                }
                if (instruction.Opcode is not (6 or 7 or 10) && !(instruction.Opcode==9 && instruction.Flags==1))
                    throw new InvalidOperationException($"{instruction.Source}: declared control fixture has an unsupported construction category.");
                int instances=instruction.Opcode==6 ? (instruction.Flags>>5)&7 : 1;
                for (int instance=0; instance<instances; instance++)
                {
                    int index=instruction.Opcode!=9 && (instruction.Flags&1)==0 && killable<7 ? ++killable : 0;
                    if (enemies.Count+occupied==16) { reader.AllocationFailed(); break; }
                    enemies.Add((instruction.Opcode==10 ? 0x59 : instruction.Id,instruction.Opcode==10 ? instruction.Id : instruction.SubId,instruction.Var03,index));
                    if (instruction.Opcode!=9) reservations++;
                    if (instruction.Opcode is not (9 or 10) && (instruction.Flags&2)==0) count++;
                }
            }
            native.ParseObjectAddress(database.Entry(label));
            string source=$"{game}:{label}/occupied{occupied}/gate${gate:x2}/direction{direction}/load${load:x2}/flags${flags:x2}";
            FailIf(memory.ReadField("wcc85")!=native[0xcc85] || killable!=native[0xceca] || count!=native[0xcdd1] || (reservations&15)!=native[0xcec1],
                source+$": native pointer side effect or opcode-run count differs: gate={memory.ReadField("wcc85"):x2}/{native[0xcc85]:x2}, index={killable}/{native[0xceca]}, count={count}/{native[0xcdd1]}.");
            for (int slot=occupied; slot<16; slot++)
            {
                int index=slot-occupied, address=0xd080+slot*256;
                FailIf((index<enemies.Count)!=(native[address]!=0),source+$": native ENEMY slot${slot:x2} admission differs.");
                if (index>=enemies.Count) continue;
                var expected=enemies[index];
                FailIf(native[address]!=(1|expected.Index<<4) || native[address+1]!=expected.Id || native[address+2]!=expected.Sub || native[address+3]!=expected.Var,
                    source+$": native ENEMY slot${slot:x2} identity/index/parameter differs.");
            }
            for (int slot=0; slot<14; slot++)
            {
                int address=0xd240+slot*256;
                FailIf((slot<interactions.Count)!=(native[address]!=0),source+$": native INTERAC slot${slot+2:x2} admission differs.");
                if (slot>=interactions.Count) continue;
                var expected=interactions[slot];
                FailIf(native[address+1]!=expected.Id || native[address+2]!=expected.Sub || native[address+3]!=expected.Var,
                    source+$": native INTERAC slot${slot+2:x2} identity/parameter differs.");
            }
            for (int slot=occupied; slot<16; slot++)
            {
                int index=slot-occupied, address=0xd0c0+slot*256;
                FailIf((index<parts.Count)!=(native[address]!=0),source+$": native PART slot${slot:x2} admission differs.");
                if (index>=parts.Count) continue;
                var expected=parts[index];
                FailIf(native[address+1]!=expected.Id || native[address+2]!=expected.Sub || native[address+3]!=expected.Var,
                    source+$": native PART slot${slot:x2} identity/parameter differs.");
            }
            cases++;
        }
        GD.Print($"Compared {game} shared object control flow in {cases} native pointer/event/condition/capacity streams.");
    }

    private static void CheckSeasonsAfterEventObjects(FrontendValidationRoot root, bool batched)
    {
        var session=root._seasonsOpening!; var rooms=session.Rooms; var entities=session.Entities;
        var memory=session.Memory; var player=root._scene.Player; var app=new ApplicationValidationFixture(root);
        byte flags=session.Save.GetRoomFlags(0,0x98);
        try
        {
            foreach (bool afterEvent in new[] {true,false,true})
            {
                session.Save.SetRoomFlag(0,0x98,0x40); session.Save.SetRoomFlag(0,0x98,0x80,afterEvent);
                root._scene.Dialogue.Close(); memory.SetField("wcc85",0);
                session.DebugTravel(0,0x98);
                // Debug travel deliberately has unrestricted placement. Reparse
                // through the real owner with the declared native entry mode.
                var seed=entities.Random.CaptureState();
                memory.SetField("wWarpDestPos",0xf0); memory.SetField("wScrollMode",1);
                entities.LoadRoom(0,rooms.CurrentRoom,EnemyPlacementContext.FromWarpDestination(0xf0));
                entities.DisableLinkCollisionsAndMenu(); player.BeginCutsceneControl(); player.WarpTo(new(136,24));
                player.Inventory.EquipA(0); player.Inventory.EquipB(0); memory.SetField("wDisabledObjects",6);
                var native=SharedNativeEnemyReference(rooms,player,9);
                native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2; native[0xcc85]=0;
                native.GeneratePlacementBuffer(); native[0xcc8a]=6; native[0xcd00]=0; native[0xcc4a]=0xf0;
                native.ParseObjectAddress(NativeObjectStreamDatabase.For(rooms.AssetRoot).Root(0,0x98));
                void Compare(bool initialized)
                {
                    var actors=entities.Entities<OctorokCharacter>();
                    var rng=entities.Random.CaptureState();
                    FailIf(actors.Count!=(afterEvent?3:0) || entities.RoomEnemyCount!=native[0xcdd1] ||
                        rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls!=seed.Calls+native.RandomCalls,
                        $"Seasons room0:98 after-event={afterEvent}: production root/count/RNG differs from its native program.");
                    for (int slot=0; slot<actors.Count; slot++)
                    {
                        int address=0xd080+slot*256; var actor=actors[slot];
                        FailIf(actor.Record.Id!=9 || actor.Record.SubId!=1 ||
                            actor.Position!=new Vector2(native.Word(address+12)/256f,native.Word(address+10)/256f) ||
                            initialized && (actor.InitializationPending || actor.Health!=native[address+0x29]),
                            $"Seasons room0:98 native Octorok slot${slot:x2} placement/state-zero handoff differs: init={initialized}, " +
                            $"runtime={actor.Position}, native=({native.Word(address+12)/256f},{native.Word(address+10)/256f}), " +
                            $"pending={actor.InitializationPending}, health={actor.Health}/{native[address+0x29]}, modifier={memory.ReadField("wRoomStateModifier"):x2}.");
                    }
                }
                Compare(false);
                app.Step(3,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); Compare(true);
                });
            }
        }
        finally
        {
            session.Save.SetRoomFlag(0,0x98,0x40,(flags&0x40)!=0); session.Save.SetRoomFlag(0,0x98,0x80,(flags&0x80)!=0);
            memory.SetField("wDisabledObjects",0); player.EndCutsceneControl(); entities.EnableLinkCollisionsAndMenu();
        }
    }
}
