
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedObjectConditions(RoomEntityManager entities, BraceletRom native)
    {
        var memory=entities.RuntimeState;
        byte originalModifier=memory.ReadField("wRoomStateModifier");
        const string source="code/objectLoading.s:objectDataOp0 native bitTable comparison";
        RoomObjectRecord row=new(0,0,0,RoomObjectKind.ParameterEnemy,0x32,0,0,1,88,104,0x56,0)
            {SourceOverride=source,Var03=9};
        for (int modifier=0; modifier<8; modifier++)
        for (int mask=0; mask<256; mask++)
        {
            memory.SetField("wRoomStateModifier",(byte)modifier);
            native[0xcc32]=(byte)modifier; native[0xd080]=0;
            native.ParseObjectStream([0xf0,(byte)mask,0xf9,1,0x32,0,9,88,104,0xff]);
            FailIf(entities.RoomObjectConditionMet(row with {ConditionMask=mask})!=(native[0xd080]!=0),
                $"{entities.AssetRoot}: condition mask${mask:x2}, live modifier${modifier:x2} differs from objectDataOp0.");
        }
        memory.SetField("wRoomStateModifier",0x83);
        bool rejected=false;
        try { _=entities.RoomObjectConditionMet(row with {ConditionMask=1}); }
        catch (NotSupportedException error) { rejected=error.Message.Contains(source) && error.Message.Contains("$83"); }
        FailIf(!rejected,"An unsupported conditional room-state table index must fail with source identity and its raw hex byte.");
        memory.SetField("wRoomStateModifier",originalModifier);
    }

    private static void CheckAgesRoomObjectConditions(FrontendValidationRoot root, bool batched)
    {
        var rooms=root._gameplaySession!.Rooms; var entities=root._entities;
        var memory=rooms.RuntimeState; var save=rooms.SaveData; var player=root._scene.Player;
        var app=new ApplicationValidationFixture(root);
        bool swapped=save.HasRoomFlag(0,0x36,OracleSaveData.RoomFlagLayoutSwap);
        int maple=save.MapleKillCounter;
        save.SetRoomFlag(0,0x36,OracleSaveData.RoomFlagLayoutSwap,false); save.SetMapleKillCounter(0);
        try
        {
            foreach (byte modifier in new byte[] {1,0,2,0})
            {
                root.LoadDebugRoom(0,0x36);
                FailIf(memory.ReadField("wRoomStateModifier")!=0,
                    "Ages room0:36 initialization must publish its normal modifier $00 before object conditions.");
                // Parse again without another initializeRoom. The native parser
                // reads this byte even when it differs from the layout flags.
                memory.SetField("wRoomStateModifier",modifier); entities.LoadRoom(0,rooms.CurrentRoom);
                entities.DisableLinkCollisionsAndMenu(); player.BeginCutsceneControl(); player.WarpTo(new(136,24));
                memory.SetField("wDisabledObjects",4);
                void Compare()
                {
                    // enemyData.s: condition$01 scopes Octorok$09 x1 and
                    // Buzz Blob$18 x2, not just the first opcode run.
                    bool admitted=modifier==0;
                    FailIf(entities.Entities<OctorokCharacter>().Count!=(admitted?1:0) ||
                        entities.Entities<BuzzBlobCharacter>().Count!=(admitted?2:0) ||
                        entities.RoomEnemyCount!=(admitted?3:0) || memory.ReadField("wRoomStateModifier")!=modifier,
                        $"Ages room0:36 must use live modifier${modifier:x2} across both native enemy opcode runs: " +
                        $"Octorok={entities.Entities<OctorokCharacter>().Count}, BuzzBlob={entities.Entities<BuzzBlobCharacter>().Count}, count={entities.RoomEnemyCount}, modifier={memory.ReadField("wRoomStateModifier"):x2}.");
                }
                Compare(); app.Step(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
                rooms.Load(0,0x36); entities.LoadRoom(0,rooms.CurrentRoom);
                FailIf(memory.ReadField("wRoomStateModifier")!=0 || entities.RoomEnemyCount!=3,
                    "A normal Ages room re-entry must recalculate the modifier once and restore its conditional stream.");
            }
        }
        finally
        {
            save.SetRoomFlag(0,0x36,OracleSaveData.RoomFlagLayoutSwap,swapped);
            save.SetMapleKillCounter(maple); memory.SetField("wDisabledObjects",0);
            player.EndCutsceneControl(); entities.EnableLinkCollisionsAndMenu();
        }
    }
}
