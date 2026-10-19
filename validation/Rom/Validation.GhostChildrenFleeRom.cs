using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGhostChildrenFleeRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(0, 0x7b, 0x40, false); LoadValidationRoom(0, 0x7b);
            var owner = _roomEvents.Get<GraveyardGhostKidsEvent>();
            var lanes = new[] { SomariaPrivate<ChildLane>(owner, "_red"), SomariaPrivate<ChildLane>(owner, "_green"), SomariaPrivate<ChildLane>(owner, "_blue") };
            foreach (var entity in _entities.EntityAdapters<IRoomEntity>().Where(entity => !lanes.Any(lane => ReferenceEquals(lane.Actor, entity.Node))).ToArray())
                _entities.RetireEntity(entity);
            _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1); _runtimeState.SetWramByte(WramAddress.wMenuDisabled, 1);
            // Isolate actor publication from cutscene01's later warp-query
            // writer using the same real source gate in both engines.
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = native[0xcc02] = 1;
            native[0xcc6e] = 1; // wWarpsDisabled, distinct from wDisableWarps/$ccb2.
            int[] addresses = lanes.Select(lane => 0xd040 + _entities.InteractionSlot(lane.Actor) * 256).ToArray();
            for (int i = 0; i < lanes.Length; i++)
            {
                int at = addresses[i]; native[at] = 1; native[at + 1] = (byte)lanes[i].Actor.Record.Id;
                native[at + 2] = (byte)lanes[i].Actor.Record.SubId;
                native[at + 11] = (byte)lanes[i].Actor.Position.Y; native[at + 13] = (byte)lanes[i].Actor.Position.X;
            }
            native.AdvanceInteractions(); // Real graphics/script initialization, before declared flee setup.
            for (int i = 0; i < lanes.Length; i++)
            {
                var lane = lanes[i]; int at = addresses[i];
                lane.Stage = ChildStage.Fleeing; lane.Counter = 0x38;
                lane.PrecisePosition = lane.Actor.Position + new Vector2(0.25f, 0.5f);
                lane.Actor.SetScriptAnimation(lane.Actor.Record.RightAnimation);
                native[at + 4] = 1; native[at + 5] = i == 0 ? (byte)0 : (byte)2;
                native[at + 6] = 0; native[at + 7] = 0x38; native[at + 9] = 8; native[at + 0x10] = 0x50; native[at + 0x39] = 0;
                native.Word(at + 10, (int)(lane.PrecisePosition.Y * 256)); native.Word(at + 12, (int)(lane.PrecisePosition.X * 256));
                native[0xffae] = 0x40;
                byte[] wrapper = [0x16, (byte)(at >> 8), 0x3e, 1, 0xcd, 0x0a, 0x2b, 0xc9];
                for (int j = 0; j < wrapper.Length; j++) native[0xc100 + j] = wrapper[j];
                SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 0);
            }
            native[0xffae] = 0;
            int update = 0, moving = 0; bool completed = false;
            int[] exits = new int[3]; byte[] expected = new byte[4];
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count, bool text = false) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                bool[] live = addresses.Select(at => native[at] != 0).ToArray();
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                native.CheckTileWarps(); // Source wWarpsDisabled gate preserves actor scratch.
                for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i];
                for (int i = 0; i < lanes.Length; i++)
                {
                    int at = addresses[i]; bool alive = native[at] != 0;
                    if (live[i] && !text) moving++;
                    if (live[i] && !alive) exits[i] = update;
                    FailIf(lanes[i].Actor.Active != alive || alive &&
                        (lanes[i].Counter != native[at + 7] || lanes[i].PrecisePosition != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f)),
                        $"Ghost child{i} visit{update}: source position/counter/screen-boundary deletion differs.");
                }
                completed = (native[0xc77b] & 0x40) != 0;
                FailIf(owner.Completed != completed || _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != native[0xcc8a] ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != native[0xcc02] ||
                    _player.CutsceneControlled == completed || Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]),
                    $"Ghost escape visit{update}: flag={owner.Completed}/{completed}, controls={_player.CutsceneControlled}, mask={_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/{native[0xcc8a]:x2}, menu={_runtimeState.ReadWramByte(WramAddress.wMenuDisabled):x2}/{native[0xcc02]:x2}, scratch=[{string.Join(',', Enumerable.Range(0,4).Select(i => _runtimeState.ReadWramByte(0xcec0+i)))}]/[{string.Join(',', expected)}].");
                Seed();
            });
            Seed(); Step(4); _dialogue.ShowMessage("Ghost escape hold.", _player.Position.Y); native[0xcba0] = 1;
            Step(3, true); _dialogue.Close(); native[0xcba0] = 0;
            Step(48); Step(2);
            FailIf(owner.HasState || !completed || moving < 100 || exits.Any(exit => exit == 0) || !(exits[1] < exits[2] && exits[2] < exits[0]),
                "Ghost children must leave independently in green/blue/red order, with first-exit control release.");
            LoadValidationRoom(0, 0x7b); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(owner.HasState || lanes.Any(lane => lane.Actor.Active), "Completed ghost scene must remain suppressed on re-entry.");
        }
    }
}
