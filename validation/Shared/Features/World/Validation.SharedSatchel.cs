
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedSatchel(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var satchel = ((PlayerWorld)player.World).Satchel;
        var app = new ApplicationValidationFixture(root);
        OracleGame game = rooms.SaveData.Game;
        FailIf(!ReferenceEquals(player.World.Pegasus,satchel.Pegasus),
            $"{game}: input and Link movement must use the same Pegasus owner.");
        var room = rooms.CurrentRoom;
        var tiles = (from y in Enumerable.Range(2,4) from x in Enumerable.Range(2,5)
            let point = new Vector2(x * 16 + 8,y * 16 + 8)
            select (Point:point,Tile:room.GetMetatile(point),Collision:room.GetTerrainInfo(point).Collision)).ToArray();
        // Controlled native floor isolates input/dispatch from unrelated room events.
        foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point,(byte)(game == OracleGame.Ages ? 0x3a : 0x04),0,0);
        try
        {
            foreach (var scenario in new (int Item,int Direction,bool Primary)[] { (0x20,0,true), (0x21,1,false), (0x24,2,true), (0x23,3,false), (0x22,0,true), (0x22,1,false), (0x20,0,true) })
            {
                entities.Clear(); player.WarpTo(new(72,64)); player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(scenario.Direction * 8));
                satchel.Pegasus.Clear();
                inventory.GiveTreasure(TreasureId.SeedSatchel,1); inventory.GiveTreasure(scenario.Item,0x20);
                inventory.SelectSatchelSeeds(scenario.Item - 0x20);
                inventory.EquipA(scenario.Primary ? TreasureId.SeedSatchel : 0);
                inventory.EquipB(scenario.Primary ? 0 : TreasureId.SeedSatchel);
                var random = entities.Random.CaptureState();
                var native = new BraceletRom(room,player.PrecisePosition,scenario.Direction,0xff,game:game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
                native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcc2e] = 0;
                native[0xcd00] = 1;
                native[0xff94] = random.Rng1; native[0xff95] = random.Rng2;
                var sounds = root._sound.AttachPlayRequestAudit();
                int update = 0;
                void Compare()
                {
                    string context = $"{game}: Satchel ITEM 0x{scenario.Item:x2}, A={scenario.Primary}, update={++update}";
                    CompareParentAnimationPublicationRom(player, address => native[address], context);
                    bool parent = Enumerable.Range(0xd2,4).Any(page => native[page << 8] != 0 && native[(page << 8) + 1] == 0x19);
                    FailIf(player.IsUsingSeedSatchel != parent ||
                        player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f,native.Word(0xd00a) / 256f),
                        context + ": native parent lifetime or Link movement differs.");
                    int ammo = (game == OracleGame.Ages ? 0xc6b9 : 0xc6b5) + scenario.Item - 0x20;
                    FailIf(rooms.SaveData.ReadWramByte(ammo) != native[ammo],context + ": BCD seed consumption differs.");
                    FailIf(satchel.Pegasus.RawCounter != native.Word(0xcc6c),context + ": Pegasus timer/pulse differs.");
                    var children = entities.Entities<EmberSeedEffect>().Where(seed => !seed.Finished).ToArray();
                    int[] slots = Enumerable.Range(0xd6,10).Select(page => page << 8)
                        .Where(slot => native[slot] != 0 && native[slot + 1] == scenario.Item).ToArray();
                    FailIf(children.Length != slots.Length,context + ": physical child allocation/deletion differs.");
                    for (int index = 0; index < slots.Length; index++)
                    {
                        var child = children[index]; int slot = slots[index];
                        FailIf(child.SeedItem != scenario.Item || child.PrecisePosition != new Vector2(native.Word(slot + 0x0c) / 256f,native.Word(slot + 0x0a) / 256f) ||
                            child.ZFixed != unchecked((short)native.Word(slot + 0x0e)),context + ": child identity/XYZ differs.");
                    }
                    var dust = entities.Entities<PegasusDustRoomEntity>();
                    FailIf(dust.Count != (native[0xdf00] == 0 ? 0 : 1),context + ": reserved Pegasus dust allocation differs.");
                    if (dust.Count != 0)
                        FailIf(dust[0].Substate != native[0xdf05] || dust[0].Position != new Vector2(native[0xdf0d],native[0xdf0b]) ||
                            dust[0].Visible != ((native[0xdf1a] & 0x80) != 0) || dust[0].TileBase != native[0xdf1d] || dust[0].OamFlags != native[0xdf1c] ||
                            !dust[0].Clouds.SequenceEqual(Enumerable.Range(0,8).Select(index => native[0xdf30 + index]).ToArray()),
                            context + ": native dust initialization/clouds/presentation differs.");
                    var rng = entities.Random.CaptureState();
                    FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != random.Calls + native.RandomCalls,
                        context + ": shared RNG order differs.");
                    FailIf(!sounds.Requests.SequenceEqual(native.Sounds),context + ": sound request order differs.");
                }
                void Step(int count, bool press = false, Vector2 movement = default)
                {
                    string button = scenario.Primary ? "attack" : "item";
                    int keys = (press ? scenario.Primary ? 1 : 2 : 0) |
                        (movement == Vector2.Up ? 0x40 : movement == Vector2.Right ? 0x10 : 0);
                    app.Step(count,movement,held:press ? [button] : [],pressed:press ? [button] : [],batched:batched,
                        afterUpdate:() => { native.UpdateLinkItems(entities.FrameCounter,keys); Compare(); });
                }
                // Declared native mask input: Link reads $81, initialized
                // items read $90, while ITEM_DUST remains in state zero.
                rooms.RuntimeState.SetField("wDisabledObjects",0x90); native[0xcc8a] = 0x90;
                Step(1,true); Step(2);
                rooms.RuntimeState.SetField("wDisabledObjects",0); native[0xcc8a] = 0;
                Step(1,true);
                FailIf(scenario.Item == 0x22 ? !satchel.Pegasus.Active || player.IsUsingSeedSatchel :
                    !player.IsUsingSeedSatchel || entities.Entities<EmberSeedEffect>().Count != 1,
                    $"{game}: fresh Satchel input did not reach native parent/child or Pegasus activation.");
                Step(3);
                rooms.RuntimeState.SetField("wDisabledObjects",0x90); native[0xcc8a] = 0x90;
                Step(3);
                rooms.RuntimeState.SetField("wDisabledObjects",0); native[0xcc8a] = 0;
                if (scenario.Item == 0x23)
                {
                    player.ClearNativeItemParents(); native.ClearItemParents();
                    FailIf(player.IsUsingSeedSatchel || entities.Entities<EmberSeedEffect>().Count != 1,
                        $"{game}: clearing the live Gale Satchel parent retired its independently owned child.");
                }
                root._scene.Dialogue.ShowMessage("Satchel pause.",player.Position.Y); native[0xcba0] = 1;
                Step(5); root._scene.Dialogue.Close(); native[0xcba0] = 0;
                Step(scenario.Item == 0x23 ? 2 : 16);
                if (scenario.Item == 0x22)
                {
                    int retained = inventory.PegasusSeeds;
                    Step(1,true); Step(3, movement:Vector2.Up);
                    FailIf(inventory.PegasusSeeds != retained,$"{game}: active Pegasus input consumed a second seed.");
                    // Independent exact expiry boundary, without a long timer wait.
                    rooms.RuntimeState.SetField("wPegasusSeedCounter",3);
                    rooms.RuntimeState.SetField("wPegasusSeedCounter",0,1);
                    native.Word(0xcc6c,3);
                    Step(1); FailIf(!satchel.Pegasus.Active,"Native counter 3 must retain one update.");
                    Step(1); FailIf(satchel.Pegasus.Active,"Native counter must expire on the second decrement update.");
                    Step(2); Step(1,true); Step(3);
                    FailIf(!satchel.Pegasus.Active || inventory.PegasusSeeds == retained,$"{game}: expired Pegasus must allow a fresh input and seed consumption.");
                }
                else
                {
                    int children = entities.Entities<EmberSeedEffect>().Count;
                    player.ClearNativeItemParents();
                    FailIf(player.IsUsingSeedSatchel || entities.Entities<EmberSeedEffect>().Count != children,
                        $"{game}: clearing the Satchel parent must preserve its physical child.");
                }
                entities.ClearPhysicalPlayerItems(); player.WarpTo(new(72,64));
                satchel.Pegasus.Clear();
            }
        }
        finally
        {
            entities.Clear(); satchel.Pegasus.Clear(); player.WarpTo(new(72,64));
            rooms.RuntimeState.SetField("wDisabledObjects",0);
            inventory.EquipA(0); inventory.EquipB(0);
            foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point,tile.Tile,tile.Collision,0);
        }
    }
}
