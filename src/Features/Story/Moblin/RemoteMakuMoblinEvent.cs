namespace oracleofages;

internal sealed class RemoteMakuMoblinEvent(RoomEventContext context):RemoteMakuEvent(context,new RemoteMakuMoblinDatabase())
{
    internal void StartMessage()=>Begin();
}
