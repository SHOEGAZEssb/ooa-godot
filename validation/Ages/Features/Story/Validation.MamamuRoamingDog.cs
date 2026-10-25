using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMamamuRoamingDog()
    {
        _saveData.SetGlobalFlag(AgesGlobalFlag.ReturnedDog,false);
        _saveData.SetRoomFlag(2,0xe7,0x80);
        var rooms=new[]{0x86,0x55,0x88,0x48};
        var positions=new[]{new Vector2(0x88,0x38),new Vector2(0x48,0x38),
            new Vector2(0x68,0x38),new Vector2(0x68,0x48)};
        for(int variant=0;variant<4;variant++)
        {
            _runtimeState.SetWramByte(AgesGameContent.MamamuDogLocationAddress,(byte)variant);
            int random=_entities.RandomCalls;
            LoadValidationRoom(0,rooms[variant]);
            var dog=_entities.EntityAdapters<MamamuRoamingDogRoomEntity>().Single();
            var actor=(NpcCharacter)dog.Node;
            FailIf(actor.Position!=positions[variant]||actor.Visible,
                $"Roaming dog $54:$01/v${variant:x2} was visible before native state zero or lost its source YX position.");
            StepGameplayUpdates(1,Vector2.Zero);
            FailIf(dog.State!=1||!actor.Active||!actor.Visible||dog.Waypoint!=0||
                _runtimeState.ReadWramByte(AgesGameContent.MamamuDogLocationAddress)==variant||
                _entities.RandomCalls<=random||dog.PositionFixed==OracleObjectPosition.FromPixels(positions[variant]),
                $"Roaming dog $54:$01/v${variant:x2} did not move twice, publish graphics, or choose a different $cde2 with global RNG on state zero (state {dog.State}, active {actor.Active}, visible {actor.Visible}, waypoint {dog.Waypoint}, location {_runtimeState.ReadWramByte(AgesGameContent.MamamuDogLocationAddress):x2}, RNG {_entities.RandomCalls-random}, position {actor.Position}, start {positions[variant]}).");
        }
        _runtimeState.SetWramByte(AgesGameContent.MamamuDogLocationAddress,0xff);
        LoadValidationRoom(0,0x86); StepGameplayUpdates(1,Vector2.Zero);
        FailIf(_entities.EntityAdapters<MamamuRoamingDogRoomEntity>().Any(),
            "Roaming dog $54:$01/v$00 survived a mismatched $cde2 state-zero gate.");
        _saveData.SetGlobalFlag(AgesGlobalFlag.ReturnedDog);
        _runtimeState.SetWramByte(AgesGameContent.MamamuDogLocationAddress,0);
        LoadValidationRoom(0,0x86); StepGameplayUpdates(1,Vector2.Zero);
        FailIf(_entities.EntityAdapters<MamamuRoamingDogRoomEntity>().Any(),
            "Returned dog $54:$01/v$00 remained outdoors after GLOBALFLAG_RETURNED_DOG $3b.");
        _saveData.SetGlobalFlag(AgesGlobalFlag.ReturnedDog,false);
        _saveData.SetRoomFlag(2,0xe7,0x80,false);
        LoadValidationRoom(0,0x86); StepGameplayUpdates(1,Vector2.Zero);
        FailIf(_entities.EntityAdapters<MamamuRoamingDogRoomEntity>().Any(),
            "Roaming dog $54:$01/v$00 appeared before Mamamu room $2:e7 flag $80.");

        _saveData.SetRoomFlag(2,0xe7,0x80);
        _runtimeState.SetWramByte(AgesGameContent.MamamuDogLocationAddress,0);
        _inventory.GiveTreasure(TreasureId.Bracelet,1);
        _inventory.EquipA(TreasureId.Bracelet);
        LoadValidationRoom(0,0x86); StepGameplayUpdates(1,Vector2.Zero);
        var roaming=_entities.EntityAdapters<MamamuRoamingDogRoomEntity>().Single();
        var pup=(NpcCharacter)roaming.Node;
        var geometry=new BraceletGrabGeometry();
        bool caught=false;
        _player.WarpTo(new Vector2(64,56));
        FailIf(_rooms.CurrentRoom.IsSolid(_player.Position),
            "Roaming dog $54:$01/v$00 floor approach started inside collision geometry.");
        StepGameplayUpdates(8,Vector2.Right,["move_right"],["move_right"]);
        FailIf(_rooms.CurrentRoom.IsSolid(_player.Position)||_player.Position.X<=64,
            "Roaming dog $54:$01/v$00 floor approach could not walk east in room $0:86.");
        for(int tick=0;tick<700&&pup.Active;tick++)
        {
            Vector2 delta=pup.Position-_player.Position;
            Vector2 direction=Math.Abs(delta.X)>Math.Abs(delta.Y)?
                new(Math.Sign(delta.X),0):new(0,Math.Sign(delta.Y));
            _player.Face(new Vector2I((int)direction.X,(int)direction.Y));
            StepGameplayUpdates(1,Vector2.Zero);
            if(!geometry.Overlaps(new(_player.EnemyContactPosition-new Vector2(6,6),new(12,12)),
                _player.EnemyContactZ,CarriedObjectMotion.DirectionIndex(_player.FacingVector),
                pup.ObjectCollisionBounds,roaming.ZFixed>>8,false)) continue;
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            if(roaming.State==2) { caught=true; break; }
        }
        FailIf(!caught||!_player.IsCarryingObject,
            $"Roaming dog $54:$01/v$00 was not reachable by walking across room $0:86 floor and native Bracelet pickup buffer (Link {_player.Position}, dog {pup.Position}, state {roaming.State}).");
        FailIf(!_saveData.HasGlobalFlag(AgesGlobalFlag.ReturnedDog)||
            _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled)!=0x81||
            _runtimeState.ReadWramByte(AgesWramAddress.wDisableScreenTransitions)!=0x81,
            "Roaming dog grab did not publish RETURNED_DOG $3b and the source $81 menu/scroll masks.");
        for(int tick=0;tick<110&&!_dialogue.IsOpen;tick++) StepGameplayUpdates(1,Vector2.Zero);
        FailIf(!_dialogue.IsOpen||!_dialogue.CurrentMessage.Contains("You caught the")||
            _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=0x81,
            "Roaming dog did not wait for Bracelet lift, count 40 updates, freeze objects and display TX_007f.");
        _dialogue.Close(); StepGameplayUpdates(1,Vector2.Zero);
        for(int tick=0;tick<280&&_rooms.CurrentRoom.Id!=0xe7;tick++) StepGameplayUpdates(1,Vector2.Zero);
        FailIf(_rooms.ActiveGroup!=2||_rooms.CurrentRoom.Id!=0xe7||
            !_entities.Entities<NpcCharacter>().Any(n=>n.Record is {Id:AgesInteractionId.MamamuDog,SubId:0}&&n.Active),
            "Roaming dog $54:$01 did not use source warp ($2:e7, packed $25, transition2 $83) and restore the indoor pup.");
        _runtimeState.SetWramByte(AgesGameContent.MamamuDogLocationAddress,0);
        LoadValidationRoom(0,0x86);
        StepGameplayUpdates(4,Vector2.Zero);
        FailIf(_entities.EntityAdapters<MamamuRoamingDogRoomEntity>().Any(),
            "Returned dog $54:$01/v$00 respawned outdoors after the catch warp.");
    }
}
