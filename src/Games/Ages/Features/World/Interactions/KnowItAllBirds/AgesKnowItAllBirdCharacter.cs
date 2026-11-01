using System;

namespace Oracles;

internal sealed partial class AgesKnowItAllBirdCharacter : KnowItAllBirdCharacter
{
    private KnowItAllBirdRecord _dialogue = null!;
    internal KnowItAllBirdScriptHost Script { get; private set; } = null!;

    internal void InitializeBird(NpcRecord record, KnowItAllBirdDatabase database,
        OracleRandom random, Func<bool> textActive)
    {
        _dialogue=database.Get(record.SubId);
        InitializeBirdBody(record,_dialogue.Animations,AgesGameContent.AssetRoot,random,
            player=>Script.Advance(player),()=>Script.Start());
        Script=new(this,database.Commands,textActive);
    }

    internal void SelectText(bool tutorial) => SetDialogue(
        tutorial?_dialogue.TutorialTextId:BaseRecord.TextId,
        tutorial?_dialogue.Tutorial:BaseRecord.Message,canFace:false);
}
