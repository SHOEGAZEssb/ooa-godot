using Godot;
using System.Collections.Generic;

namespace Oracles;

// tokay.s:$1a-$1c have only state-zero presentation and an inPresent
// deletion gate. They do not run the NPC animation/push/collision tail.
internal sealed class TokayStatueRoomEntity(TokayCharacter npc,RoomEntityManager entities,TokayNativeDatabase data,TokayInteractionDatabase interactions)
    : NpcCharacterRoomEntityAdapter(npc,npc.SetTransitionDrawOffset), IFixedRoomEntity,
      IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private bool _initialized;
    private readonly WildTokayGameDatabase _accessories=new();
    public bool Finished=>!npc.Active;
    public bool UpdatesDuringDialogue=>!_initialized;
    public bool UpdatesDuringRoomEntityFreeze=>!_initialized;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized=true;
            if (npc.Record.SubId==0x1a)
            {
                npc.SetBasePalette(2);
                npc.ForceNextAnimationFrame();
            }
            else if (npc.Record.SubId==0x1c)
            {
                npc.SetScriptAnimation(interactions.Animation(9));
                if (entities.InteractionSlotAvailable)
                {
                    var visual=_accessories.MeatAccessory(npc.CurrentAnimationParameter);
                    npc.Accessory=new TokayAttachedVisualRoomEntity(npc,
                        data.Visual("museum-meat",npc.Record.Group,npc.Record.Room),new(visual.XOffset,visual.YOffset))
                        { Visible=false,ZIndex=ObjectDrawPriority.FromVisible(visual.Visible) };
                    entities.AddEntity(npc.Accessory);
                }
            }
            return;
        }
        if (entities.RuntimeState.ReadWramByte(0xcfc0)!=0) npc.SetActive(false);
    }
}
