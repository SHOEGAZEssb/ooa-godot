using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class LightningRoomEntity : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime
{
    private readonly LightningEffect _effect;
    private readonly RoomEntityManager _owner;
    private int _updates;
    private readonly bool _signalCutscene;
    public Node2D Node {get;}
    public bool Finished => _effect.Finished;
    internal LightningRoomEntity(RoomEventContext context,Vector2 position,bool signalCutscene = true)
    {
        _owner=context.Entities;_signalCutscene=signalCutscene;
        var actor=NpcCharacter.CreateFromRecord(new SharedEffectDatabase().Effect("Lightning").ToNpcRecord(0,0x38,(int)position.Y,(int)position.X));Node=actor;
        _effect=new(actor,()=>_owner.Random.Next().Value,context.Sound.PlaySound,_owner.BeginScreenShake,p=>
        {
            if(_owner.InteractionSlotAvailable)_owner.Spawn<KillEnemyPuffEffect>(new KillEnemyPuffSpawn(p));
        });
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {_effect.UpdateFrame();if(++_updates>=3&&!Finished&&_signalCutscene)_owner.RuntimeState.SetWramByte(0xcfd2,0xff);}
    public void SetTransitionDrawOffset(Vector2 offset) => ((NpcCharacter)Node).SetTransitionDrawOffset(offset);
}
