namespace Oracles;

internal sealed class BrotherGoriyaRoomEntity : CombatEnemyRoomEntityAdapter<BrotherGoriyaCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,IScreenTransitionPreloadRoomEntity
{
    internal BrotherGoriyaRoomEntity(BrotherGoriyaCharacter actor,EnemyCombatSourceDescriptor source,Action<int> sound)
        :base(actor,actor.SetTransitionDrawOffset,EnemyCombatDescriptor.WithContactDamage(source,actor,actor.Record.DamageQuarters,
            actor.TakeSwordHit,actor.TakeBurnHit,actor.ApplySwordKnockback,sound,deathPuffAllowed:()=>false,
            completedOutcome:()=>RoomEnemyOutcome.BossTeardown(source.KillableEnemyIndex),acceptedHitSound:SoundId.SndBossDamage)){}
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve)=>Entity.BindSlot(slot,resolve);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame,spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)=>
        throw new InvalidOperationException("Brother Goriya preload requires the actual Link owner.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        if(player is null)throw new InvalidOperationException("Brother Goriya preload requires the actual Link owner.");
        if(Entity.InitializationPending)Entity.UpdateFrame(new(player,0,false),spawns);
        return Entity.Visible?ScreenTransitionPresentation.Visible:ScreenTransitionPresentation.Hidden;
    }
}
