using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidChestRom()
    {
        foreach (bool batch in new[] { false,true })
        foreach (int roomId in new[] { 0x13,0x1c })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers,0);
            _inventory.GiveTreasure(TreasureId.Feather,0);
            LoadValidationRoom(5,roomId);
            FailIf(_rooms.CurrentDungeonIndex != 6,"Source tileset$3b selects present Mermaid dungeon$06.");
            var active=SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free=typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            // The boss-key puzzle's chest creation is independently checked by
            // MermaidBossKeyRom. Begin this consumer at that completed boundary.
            foreach (var actor in active.Where(actor => actor is not DungeonBossKeyMirrorRoomEntity).ToArray())
            { active.Remove(actor); free.Invoke(_entities,[actor]); }
            if (roomId == 0x1c) _currentRoom.SetPositionTileAndCollision(new(120,24),0xf1,null,(long)_animationTicks);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x17) != 0xf1,"Both source chestData records occupy $17.");
            _player.WarpTo(new(120,72)); _player.Face(Vector2I.Up); _inventory.EquipA(ItemId.Feather); _inventory.EquipB(0);
            FailIf(_collision.Collides(_player.Position),"Mermaid chest route must start on the original southern floor.");
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,72);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39]=6;
            foreach (var mirror in _entities.Entities<DungeonBossKeyMirrorRoomEntity>())
            {
                int a=0xd040+_entities.InteractionSlot(mirror)*256;
                rom[a]=1; rom[a+1]=0xdc; rom[a+2]=0x11;
            }
            int update=0;
            void Step(int count=1,int angle=0xff,bool press=false) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                press ? ["attack"] : [],press ? ["attack"] : [],batch,() => {
                rom.UpdateGameplay(press ? 1 : 0,(press ? 1 : 0)|(angle switch { 0=>0x40,8=>0x10,16=>0x80,24=>0x20,_=>0 }),angle,_entities.FrameCounter);
                press=false; rom.AdvanceTileGraphics();
                string context=$"Mermaid chest5:${roomId:x2}, batch={batch}, update{++update}";
                CompareSomariaMotionRom(rom,context);
                for (int address=0xc672;address<0xc6cc;address++)
                    FailIf(_saveData.ReadWramByte(address) != rom[address],context+$": inventory${address:x4} runtime=${_saveData.ReadWramByte(address):x2}, native=${rom[address]:x2}.");
                FailIf(_saveData.GetRoomFlags(5,roomId) != rom[0xca00+roomId],context+": ROOMFLAG_ITEM handoff differs.");
                FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _runtimeState.ReadWramByte(WramAddress.wDisableLinkCollisionsAndMenu) != rom[0xcbca],context+": chest object/collision/menu masks differ.");
                if (_interactions.ChestReward is { } reward)
                    FailIf(reward.Position != new Vector2(rom[0xd04d],rom[0xd04b]) ||
                        reward.Visible != ((rom[0xd05a]&0x80) != 0),context+$": reserved treasure runtime={reward.Position}/{reward.Visible}, native={rom[0xd04d]},{rom[0xd04b]}/{rom[0xd05a]:x2}.");
            });
            Step(1,0,press:true); Step(36,0);
            Step(8,0); // Finish the landing and reach both native chest wall probes.
            FailIf(_player.Position.Y > 43 || _player.Position.Y < 32,$"Original alcove entrance must permit a reachable chest approach: room${roomId:x2}, Link={_player.PrecisePosition}, entrance=${_currentRoom.GetPackedStorageMetatile(0x37):x2}.");
            Step(press:true);
            FailIf(!_interactions.ChestRewardActive || _currentRoom.GetPackedStorageMetatile(0x17) != 0xf0,
                "The actual Link A-button update must open the source chest.");
            if (roomId == 0x1c)
                FailIf(!_inventory.HasDungeonBossKey(0x0c) || _inventory.HasDungeonBossKey(6),
                    "DC:$11 must grant past key$0c in the opening update, before present key$06's rise finishes.");
            Step(33);
            FailIf(!_dialogue.IsOpen || (roomId == 0x13 ? !_inventory.HasTreasure(TreasureId.MermaidSuit) : !_inventory.HasDungeonBossKey(6)),
                "Source INTERAC$60 initialization/setup and 32 rising updates must grant the dungeon reward.");
            Step(4,16);
            _dialogue.Close(); rom[0xcba0]=0;
            Step();
            FailIf(_interactions.ChestRewardActive,"Reserved chest reward must delete after its text closes.");
            Step(press:true);
            FailIf(_interactions.ChestRewardActive,"An opened Mermaid chest must not grant twice.");
            LoadValidationRoom(0,0x60); LoadValidationRoom(5,roomId);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x17) != 0xf0 ||
                (roomId == 0x13 ? !_inventory.HasTreasure(TreasureId.MermaidSuit) :
                    !_inventory.HasDungeonBossKey(6) || !_inventory.HasDungeonBossKey(0x0c)),
                "Mermaid reward, both era keys, and opened chest must persist on re-entry.");
            if (roomId == 0x1c)
            {
                LoadValidationRoom(5,0x38); _entities.Clear();
                _player.WarpTo(new(120,40)); _player.Face(Vector2I.Up);
                FailIf(_rooms.CurrentDungeonIndex != 0x0c || _currentRoom.GetPackedStorageMetatile(0x07) != 0x74 ||
                    _collision.Collides(_player.Position),"Past Mermaid boss door$5:$38/$07 must use key$0c and its actual south floor.");
                seed=_random.CaptureState();
                rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,40);
                rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom.CreateMenuView().LoadDungeon(0x0c);
                var sounds=_sound.AttachPlayRequestAudit();
                void DoorStep(int count=1,int angle=0xff) => StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() => {
                    CompareKeyDoorGameplayRom(rom,seed,sounds.Requests,$"Mermaid acquired past boss key, batch={batch}",dungeon:0x0c);
                    for (int room=0;room<256;room++)
                        FailIf(_saveData.GetRoomFlags(5,room) != rom[0xca00+room],$"Mermaid boss-door flag5:${room:x2} differs.");
                });
                DoorStep();
                for (int walk=0;!_keyDoors.Opening && walk<50;walk++) DoorStep(angle:0);
                FailIf(!_keyDoors.Opening || !_inventory.HasDungeonBossKey(6) || !_inventory.HasDungeonBossKey(0x0c),
                    "The chest-mirrored key must open the actual past boss door without being consumed.");
                DoorStep(40);
                FailIf(_keyDoors.Opening || _currentRoom.GetPackedStorageMetatile(0x07) != 0xa0 ||
                    !_saveData.HasRoomFlag(5,0x38,1) || !_saveData.HasRoomFlag(5,0x36,4),
                    "Boss door must open and persist both source dungeon-layout sides.");
                DoorStep(4,16); DoorStep(4,0);
                FailIf(_keyDoors.Opening || ! _inventory.HasDungeonBossKey(0x0c),"Repeated boss-door approach must preserve the mirrored key.");
            }
        }
        GD.Print("Validated clean-US actual Mermaid Suit/boss-key chest approaches, opening/rise/text/cleanup timing, cross-era mirror, repeat and re-entry in split/batched gameplay.");
    }
}
