using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_COMPANION_SPAWNER $67:$03 owns a placed slot until its first
// dispatch, including when a restored companion already occupies $d1.
internal sealed class DimitriPresetSpawnerRoomEntity(Action spawn)
    : RoomEntityAdapter<Node2D>(new Node2D { Name="DimitriPresetSpawner_67_03",Visible=false },static _=>{}),
      IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,
      IScreenTransitionPreloadRoomEntity
{
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    private void Initialize(){if(Finished)return;spawn();Finished=true;}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Initialize();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {Initialize();return ScreenTransitionPresentation.Hidden;}
}
