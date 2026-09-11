# This is the single normalized command contract consumed by the importer,
# runtime decoder, runner, actor preflight, and validation. Source aliases and
# byte shapes retain the assembly origin even when one source operation expands
# into a controller-native runtime command.
$cutsceneVocabularyRows = @(
    "# opcode`tsource-aliases`tbyte-shape`tcommand-type`tactor-shape`targ0-shape`targ1-shape`tpayload-shape`tresults`tactor-members`tcapabilities`tdescription",
    "setcoords`tsetcoords`t3`tCutsceneSetCoordinatesCommand`trequired`thex`thex`tnone`tyield`tActorId`tactor-position`tWrite the integer coordinate bytes, preserving fractions.",
    "disableinput`tdisableinput`t1`tCutsceneDisableInputCommand`tnone`tnone`tnone`tnone`tcontinue`t-`tinput`tDisable Link and the menu.",
    "disablemenu`tdisablemenu`t1`tCutsceneDisableMenuCommand`tnone`tnone`tnone`tnone`tcontinue`t-`tmenu`tDisable the menu.",
    "setdisabledobjects`tdisableallobjects`t1`tCutsceneSetDisabledObjectsCommand`tnone`thex`tnone`tnone`tyield`t-`tdisabled-objects`tWrite the disabled-object mask and yield.",
    "setdisabledobjectscontinue`tnative:wDisabledObjects`truntime`tCutsceneSetDisabledObjectsContinueCommand`tnone`thex`tnone`tnone`tcontinue`t-`tdisabled-objects`tWrite the disabled-object mask and continue.",
    "setcounter`tsetcounter1`t2`tCutsceneSetCounterCommand`tnone`tdecimal`tnone`tnone`tcontinue`t-`tcounter`tInstall counter1 and continue.",
    "waitpreloadedcounter`tnative:counter1`truntime`tCutsceneWaitPreloadedCounterCommand`tnone`tnone`tnone`tnone`tblock|continue`t-`tcounter`tDecrement an already installed counter1.",
    "wait`twait`t1-or-more`tCutsceneWaitCommand`tnone`tdecimal`tnone`tnone`tblock|continue`t-`tcounter`tInstall and wait on script counter1.",
    "waitframes`tcontroller:waitframes`truntime`tCutsceneWaitFramesCommand`tnone`tpositive-decimal`tnone`tnone`tblock|yield`t-`tcounter`tWait a controller-owned fixed-update duration.",
    "showtext`tshowtext`t2-or-3`tCutsceneShowTextCommand`tnone`thex`toptional-decimal`toptional`tyield`t-`tdialogue`tOpen interaction-script text at its optional imported textbox position.",
    "showloadedtext`tshowloadedtext`t1`tCutsceneShowLoadedTextCommand`tnone`tnone`tnone`tnone`tyield`t-`tdialogue`tOpen the interaction's currently loaded text.",
    "checktext`tchecktext`t1`tCutsceneCheckTextCommand`tnone`tnone`tnone`tnone`tblock|continue`t-`tdialogue`tHold until interaction text is inactive.",
    "dialogue`tcontroller:dialogue`truntime`tCutsceneDialogueCommand`tnone`thex`tnone`toptional`tblock|yield`t-`tdialogue`tOpen controller text and retain its close boundary.",
    "showtextdifferentforlinked`tshowtextdifferentforlinked`t4`tCutsceneShowTextVariantsCommand`tnone`thex`thex`ttext-variants`tyield`t-`tdialogue|linked-state`tSelect linked or unlinked text.",
    "setanimation`tsetanimation`t2-or-3`tCutsceneSetAnimationCommand`trequired`thex`tnone`toptional`tyield`tActorId`tactor-animation`tSelect a literal or encoded actor animation.",
    "setanimationcontinue`tasm15:setanimation`truntime`tCutsceneSetAnimationContinueCommand`trequired`thex`tnone`trequired`tcontinue`tActorId`tactor-animation`tSelect an actor animation and continue.",
    "setcollisionradii`tsetcollisionradii`t3`tCutsceneSetCollisionRadiiCommand`trequired`thex`thex`tnone`tyield`tActorId`tactor-collision`tWrite actor collision radii.",
    "makeabuttonsensitive`tmakeabuttonsensitive`t1`tCutsceneMakeAButtonSensitiveCommand`trequired`tnone`tnone`tnone`tcontinue`tActorId`tactor-button`tRegister an actor as a talk target.",
    "initcollisions`tinitcollisions`t1`tCutsceneInitCollisionsCommand`trequired`tnone`tnone`tnone`tcontinue`tActorId`tactor-collision|actor-button`tInstall standard radii and register a talk target.",
    "checkabutton`tcheckabutton`t1`tCutsceneCheckAButtonCommand`trequired`tnone`tnone`tnone`tblock|continue`tActorId`tactor-button`tHold until the actor consumes an A press.",
    "gate`tcontroller:gate`truntime`tCutsceneGateCommand`tnone`tnone`tnone`trequired`tblock|yield`t-`tgate-read`tHold on a named controller gate.",
    "checkmemoryeq`tcheckmemoryeq`t4`tCutsceneMemoryGateCommand`tnone`thex`tnone`trequired`tblock|yield`t-`tmemory-read`tHold until a WRAM binding equals the operand.",
    "jumpifmemoryeq`tjumpifmemoryeq`t6`tCutsceneMemoryBranchCommand`tnone`thex`tdecimal`trequired`tcontinue`t-`tmemory-read`tConditionally branch on a WRAM binding.",
    "jumpifmemoryeqyieldonmiss`tjumpifmemoryset`t6`tCutsceneMemoryBranchYieldOnMissCommand`tnone`thex`tdecimal`trequired`tcontinue|yield`t-`tmemory-read`tBranch and continue on a normalized match; otherwise advance and yield.",
    "jumptablememory`tjumptable_objectbyte`t2+table`tCutsceneMemoryJumpTableCommand`tnone`tnone`tnone`tmemory-jump-table`tcontinue`t-`tmemory-read`tIndex a normalized branch table with a binding.",
    "jumpifroomflagset`tjumpifroomflagset`t4`tCutsceneRoomFlagBranchCommand`tnone`thex`tdecimal`tnone`tcontinue`t-`troom-flag-read`tBranch when a room flag is set.",
    "jumpiftradeitemeq`tjumpiftradeitemeq`t4`tCutsceneTradeItemBranchCommand`tnone`thex`tdecimal`tnone`tcontinue`t-`ttrade-item-read`tBranch when the obtained trade item matches.",
    "jumpiftextoptioneq`tjumpiftextoptioneq`t4`tCutsceneTextOptionBranchCommand`tnone`thex`tdecimal`tnone`tcontinue`t-`ttext-option-read`tBranch on the selected text option.",
    "scriptjump`tscriptjump`t2`tCutsceneBranchCommand`tnone`tdecimal`tnone`tnone`tcontinue`t-`t-`tJump and continue dispatch.",
    "scriptjumpyield`tscriptjump:wBigBuffer`t2`tCutsceneBranchYieldCommand`tnone`tdecimal`tnone`tnone`tyield`t-`t-`tRelocate an in-buffer jump and resume on the next update.",
    "callscript`tcallscript`t3`tCutsceneCallCommand`tnone`tdecimal`tnone`tnone`tyield`t-`tcall-stack`tStore a return address and transfer next update.",
    "return`tretscript`t1`tCutsceneReturnCommand`tnone`tnone`tnone`tnone`tyield`t-`tcall-stack`tRestore a return address next update.",
    "setspeed`tsetspeed`t2`tCutsceneSetSpeedCommand`trequired`thex`tnone`tnone`tyield`tActorId`tactor-registers`tWrite the actor speed register.",
    "setangle`tsetangle`t2`tCutsceneSetAngleCommand`trequired`thex`tnone`tnone`tyield`tActorId`tactor-registers`tWrite the actor angle register.",
    "applyspeed`tapplyspeed`t1-or-2`tCutsceneApplySpeedCommand`trequired`thex`tnone`tnone`tblock|yield`tActorId`tactor-movement`tApply registered speed and angle while counter2 is nonzero.",
    "move`tmoveup|moveright|movedown|moveleft`t2`tCutsceneMoveCommand`trequired`thex`thex`trequired`tblock|yield`tActorId`tactor-animation|actor-movement`tRun a cardinal actor movement command.",
    "jump`tcallscript:jumpAndWaitUntilLanded`truntime`tCutsceneJumpCommand`trequired`tdecimal`thex`thex`tblock|yield`tActorId`tactor-z|sound`tRun the typed jump-and-land subscript.",
    "writeobjectbyte`twriteobjectbyte`t3`tCutsceneWriteObjectByteCommand`trequired`thex`thex`tnone`tyield`tActorId`tactor-object-write`tWrite an Interaction byte.",
    "writememory`twritememory`t4`tCutsceneWriteMemoryCommand`tnone`thex`tnone`trequired`tcontinue`t-`tmemory-write`tWrite one WRAM byte and continue.",
    "giveitem`tgiveitem`t3`tCutsceneGiveItemCommand`tnone`thex`thex`tnone`tyield`t-`titem-give`tCreate a treasure for immediate collection.",
    "playsound`tscriptCmd_playsound`t2`tCutscenePlaySoundCommand`tnone`thex`tnone`tnone`tyield`t-`tsound`tQueue a sound effect.",
    "setmusic`tsetmusic`t2`tCutsceneSetMusicCommand`tnone`thex`tnone`tnone`tyield`t-`tmusic`tSelect the active music track.",
    "flicker`tasm15:objectFlickerVisibility`truntime`tCutsceneFlickerCommand`trequired`thex`thex`tnone`tblock|continue`tActorId`tactor-visible|frame-counter`tRun the recognized visibility flicker loop.",
    "translate`tcontroller:translate`truntime`tCutsceneTranslateCommand`trequired`tpositive-decimal`tdecimal`ttranslation`tblock|yield`tActorId`tactor-position|actor-animation`tTranslate one actor over fixed updates.",
    "paralleltranslate`tcontroller:paralleltranslate`truntime`tCutsceneParallelTranslateCommand`trequired`tpositive-decimal`tpositive-decimal`tparallel-translation`tblock|yield`tActorId|Actor2Id`tactor-position`tTranslate two actors in stable order.",
    "deleteactor`tcontroller:deleteactor`truntime`tCutsceneDeleteActorCommand`trequired`tnone`tnone`tnone`tend`tActorId`tactor-delete`tDelete an actor and end the stream.",
    "setglobalflag`tcontroller:setglobalflag`truntime`tCutsceneSetGlobalFlagCommand`tnone`thex`tnone`tnone`tcontinue`t-`tglobal-flag-write`tSet a global flag and continue.",
    "orroomflag`torroomflag`t2`tCutsceneOrRoomFlagCommand`tnone`thex`tnone`tnone`tyield`t-`troom-flag-write`tOR the room flags and yield.",
    "orroomflagcontinue`tnative:orRoomFlags`truntime`tCutsceneOrRoomFlagContinueCommand`tnone`thex`tnone`tnone`tcontinue`t-`troom-flag-write`tOR the room flags and continue.",
    "native`tasm15`t3-or-4`tCutsceneNativeCommand`tnone`tnone`tnone`trequired`tcontinue`t-`tnative`tRun a native handler and continue.",
    "nativeyield`tasm15:yield`truntime`tCutsceneNativeYieldCommand`tnone`tnone`tnone`trequired`tyield`t-`tnative`tRun a native handler and yield.",
    "nativeblock`tasm15:block`truntime`tCutsceneNativeBlockingCommand`toptional`tpositive-decimal`tnone`tnative-block`tblock|yield`tActor?`tnative-block`tUpdate an event-specific native handler until complete.",
    "enableinput`tenableinput`t1`tCutsceneEnableInputCommand`tnone`tnone`tnone`tnone`tcontinue`t-`tinput`tEnable Link and the menu.",
    "scriptend`tscriptend`t1`tCutsceneEndCommand`tnone`tnone`tnone`tnone`tend`t-`tscript-end`tEnd the interaction script."
)
$cutsceneCommandSchemas = @{}
foreach ($row in $cutsceneVocabularyRows | Select-Object -Skip 1) {
    $columns = $row.Split([char]"`t")
    if ($columns.Count -ne 12) {
        throw "Cutscene command schema row '$($columns[0])' has " +
            "$($columns.Count) columns instead of 12."
    }
    $opcode = $columns[0]
    if ($cutsceneCommandSchemas.ContainsKey($opcode)) {
        throw "Duplicate cutscene command schema opcode '$opcode'."
    }
    $cutsceneCommandSchemas.Add($opcode, [pscustomobject]@{
        ActorShape = $columns[4]
        Arg0Shape = $columns[5]
        Arg1Shape = $columns[6]
        PayloadShape = $columns[7]
    })
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\script_command_vocabulary.tsv'),
    $cutsceneVocabularyRows)
