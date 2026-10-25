using Godot;
using System;

namespace Oracles;

internal sealed partial class MergedTwinrovaCharacter
{
    private void Death(RoomEntityFrame frame)
    {
        switch(Substate)
        {
            case 0:
                if(World.Entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)==0)
                {
                    if(frame.Player.IsDying)return;
                    World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects,1);
                    World.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisableLinkCollisionsAndMenu,1);
                    frame.Player.ClearNativeItemParents();
                }
                Visible=!Visible;
                if(Dec()==0){Counter=25;Substate=1;goto case 1;}
                if(Counter<97&&(Counter&15)==0)
                {
                    var offsets=World.Data.Bytes("merged-explosions");int index=((Counter>>4)-1)*2;
                    World.Entities.TryCreateInteractionExplosion(new(((int)Position.X+unchecked((sbyte)offsets[index+1]))&255,
                        ((int)Position.Y+unchecked((sbyte)offsets[index]))&255),var03:0);
                }
                return;
            case 1:
                if(Dec()!=0){Visible=!Visible;return;}
                Substate=2;Palette=0;RestartAnimation(0);
                World.Entities.OnRoomEntityDialogueRequested(0x2f0c,World.Data.Text(0x2f0c),Position);return;
            case 2:
                Substate=3;World.Cutscene(0x19);World.Entities.OnSoundRequested(SoundId.MusRoomOfRites);return;
            case 3:return;
            default:throw new NotSupportedException($"mergedTwinrova_deathCutscene: substate${Substate:x2}.");
        }
    }
}
