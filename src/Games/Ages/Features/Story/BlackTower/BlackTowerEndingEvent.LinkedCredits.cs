using System;

namespace Oracles;

internal sealed partial class BlackTowerEndingEvent
{
    private FountainDatabase? _fountains;
    private void BeginCreditsCompanionLink(Godot.Vector2 position)
    {
        context.Player.EndCutsceneControl();context.Player.WarpTo(position,recordSafe:false);context.Player.Face(Godot.Vector2I.Up);
        context.Player.Visible=true;context.Player.RequestNativeLinkInitialization();
        context.Inventory.SetScriptedActiveRing((int)RingId.Fist);context.Inventory.SetScriptedEquippedItems(0,0);
        Simulated.Install(Data.Input(6));Write(AgesWramAddress.wDisabledObjects,0);_linkEnabled=true;
    }
    private void LoadLinkedCreditsScene(int index)
    {
        if(index is <4 or >7)throw new NotSupportedException($"endgameCutscenes.s: linked credits vignette ${index:x2} is not represented.");
        // Unlike scenes $00..$03, linked scenes retain room identity and
        // cleared gameplay terrain. Their graphics headers supply the map.
        context.Entities.ClearDynamicInteractions();context.Transitions.ResetCamera();
        Screens.ApplyPalette(0x0f,context.Rooms.World.BackgroundPalettes,context.Entities);
        Screens.ApplyPalette(index==7?0xae:0xca,context.Rooms.World.BackgroundPalettes,context.Entities);
        Screen.Load($"credits-linked{index}");Write(AgesWramAddress.wTilesetAnimation,index==7?255:1);Write(AgesWramAddress.wScrollMode,1);
        if(index!=7)Screen.BeginNativeAnimation(context.AnimationTick());
        foreach(var actor in Data.Placements(index switch{4=>"objectData5542",5=>"objectData5514",6=>"objectData5560",_=>"objectData556a"}))
            switch(actor.Id)
            {
                case 0xa8:
                    if(actor.SubId==2){context.Entities.AddEntity(new CreditsCompanionLoaderRoomEntity(context,actor.Position,position=>
                    {
                        CompanionRuntimeState.BeginCutscene(context.Entities.RuntimeState,0x11,0,position);
                        context.Entities.AddEntity(new CompanionCreditsRoomEntity(context,Data,position){Visible=false,Position=position.Floor()});
                    }));break;}
                    if(actor.SubId!=0x54)throw new NotSupportedException($"Linked credits loader$a8:${actor.SubId:x2}.");
                    CreditsLinkLoaderRoomEntity? loader=null;
                    loader=new(context.Player,this,actor.Position,position=>
                    {
                        BeginCreditsCompanionLink(position);
                        // thing5 retains D (the interaction page), not H=$d0.
                        Write(AgesWramAddress.wLinkObjectIndex,0xd0+context.Entities.NativeInteractionSlot(loader!));
                    });context.Entities.AddEntity(loader);break;
                case 0xad:context.Entities.AddEntity(new ZeldaEndingRoomEntity(context,Data,actor.SubId,actor.Position));break;
                case 0xc1:context.Entities.AddEntity(new CreditsFlyingSpriteRoomEntity(context,Data,actor.Position));break;
                case 0xaa:context.Entities.AddEntity(new DinRoomEntity(context,Data,actor.Position,actor.SubId));break;
                case 0xb9:context.Entities.AddEntity(new HoronDogCreditsRoomEntity(context,Data,actor.SubId,actor.Position));break;
                case 0x6b:context.Entities.AddEntity(new TroupeMemberRoomEntity(Data,actor.SubId,actor.Position));break;
                case 0xdf:context.Entities.AddEntity(new NayruRalphCreditsRoomEntity(context,Data,actor.SubId,actor.Position));break;
                case 0x3c:case 0x3f:context.Entities.AddEntity(new BoyPatternRoomEntity(context,Data,actor.Id,actor.SubId,actor.Position));break;
                case 0x80:
                    var fountainData=_fountains??=new();
                    var placement=new FountainPlacement(context.Rooms.ActiveGroup,context.Rooms.CurrentRoom.Id,actor.SubId,actor.Position,"objects/ages/extraData2.s:objectData5542");
                    context.Entities.AddEntity(new FountainDecorationRoomEntity(placement,fountainData,context.Rooms.SaveData,
                        ()=>Screens.ApplyPalette(fountainData.UsesRuinedPalette(placement.Group,placement.Room,context.Rooms.SaveData)?0x7c:0x7d,context.Rooms.World.BackgroundPalettes,context.Entities)));break;
                default:throw new NotSupportedException($"objects/ages/extraData2.s:linked scene${index:x2} INTERAC${actor.Id:x2}:${actor.SubId:x2}.");
            }
    }
}
