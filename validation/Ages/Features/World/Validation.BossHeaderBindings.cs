
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateBossHeaderBindings()
    {
        foreach (bool scrolling in new[] { false,true })
        foreach (var source in new[] { (Id:0x73,Group:4,Room:0x80),(Id:0x7b,Group:4,Room:0x6b),
            (Id:0x74,Group:4,Room:0xb4),(Id:0x77,Group:5,Room:0x82),(Id:0x07,Group:5,Room:0x78),
            (Id:0x75,Group:5,Room:0x12),(Id:0x76,Group:5,Room:0x4a),(Id:0x7e,Group:5,Room:0x62),
            (Id:0x7d,Group:5,Room:0x36),(Id:0x7c,Group:4,Room:0xbf) })
        {
            ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(source.Group,source.Room,0x80,false);
            LoadValidationRoom(source.Group,source.Room);
            var actor=_entities.EntityAdapters<IRoomEntity>().Single(entity => source.Id switch
            {
                0x73 => entity.Node is ArmosWarriorActor, 0x7b => entity.Node is EyesoarActor,
                0x74 => entity is SmasherRoomEntity, 0x77 => entity.Node is BlueStalfosCharacter,
                0x07 => entity.Node is RamrockCharacter, 0x75 => entity.Node is VireCharacter,
                0x76 => entity.Node is AnglerFishCharacter, 0x7e => entity.Node is PlasmarineCharacter,
                0x7d => entity.Node is OctogonCharacter, _ => entity is SmogRoomEntity
            });
            F1Retain(actor);
            _runtimeState.SetField("wScrollMode",scrolling ? (byte)8 : (byte)1);
            _runtimeState.SetField("wScreenTransitionDirection",0); // Up's returned angle is0, not1.
            _runtimeState.SetField("wLinkForceState",4); _runtimeState.SetField("wLinkStateParameter",0x5a);
            _runtimeState.SetField("wcc93",3); _entities.DisableLinkCollisionsAndMenu();
            _sound.SetNativeActiveMusic(0x31); _sound.ClearPlayRequestAudit(); _player.Face(Vector2I.Down);
            var prepare=typeof(RoomEntityManager).GetMethod("PrepareIncomingEntityForScreenTransition",F1Hidden)!;
            prepare.Invoke(_entities,[actor,_player]);
            string context=$"ENEMY${source.Id:x2}/header-binding/scroll{scrolling}";
            // CommonBossCode source literals, independent of each binding's own
            // generated data. Native execution of this operation is additionally
            // checked in both actual game entries by CheckSharedBossEntry.
            FailIf(_runtimeState.ReadField("wcc93") != 0x83 ||
                _runtimeState.ReadField("wDisableLinkCollisionsAndMenu") != 0 || _sound.NativeActiveMusic != 0xff ||
                _sound.PlayRequestsFor(SoundId.SndCtrlStopMusic) != 1,
                context+": actor initialization must use the complete shared native header exactly once.");
            FailIf(_runtimeState.ReadField("wLinkForceState") != (scrolling ? 0x0b : 4) ||
                _runtimeState.ReadField("wLinkStateParameter") != (scrolling ? 0x16 : 0x5a) ||
                _player.FacingVector != (scrolling ? Vector2I.Up : Vector2I.Down),
                context+": header must sample live scroll/direction and preserve a normal-mode prior request.");
            if (source.Id==0x7c)
                FailIf(((SmogCharacter)actor.Node).Speed != (scrolling ? 0 : 1),
                    context+": Smog must consume native A=0 for scrolling up and A=1 for normal room initialization.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Validated ten production boss-header bindings, native signal/music/collision writes, prior-request preservation and live normal/scrolling return values.");
    }
}
