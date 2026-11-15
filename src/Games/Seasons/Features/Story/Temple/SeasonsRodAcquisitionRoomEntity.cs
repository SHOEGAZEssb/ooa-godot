namespace Oracles;

// getRodOfSeasons.s: controller, four orbiters, physical Rod, and its linked aura.
internal sealed class SeasonsRodAcquisitionRoomEntity : NativeNpcRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _npcs;
    private readonly RodOfSeasonsDatabase _data;
    private readonly NativeObjectInstruction _source;
    internal SeasonsTempleScriptHost? Host { get; private set; }
    public override bool UpdatesDuringDialogue => NativeInitializationPending || ReadByte(0x42) == 3;
    public override bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;
    internal SeasonsRodAcquisitionRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase npcs,
        RodOfSeasonsDatabase data, NativeObjectInstruction source, OracleRoomData room)
        : base(source.SubId == 0 ? npcs.CreateUninitializedActor(source, room, session.Data.World.SpritePalettes) :
            npcs.Create(source, room, session.Data.World.SpritePalettes), session.Memory, session.Link,
            index => npcs.Animation(0xe6, index), data.Text)
    {
        _session = session; _npcs = npcs; _data = data; _source = source;
        Npc.SetScriptVisible(false); Npc.SetBlocksLink(false);
    }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (ReadByte(0x42))
        {
            case 0:
                if (_session.Save.HasRoomFlag(_session.Rooms.ActiveGroup, _session.RoomId, 0x20)) { Delete(); return false; }
                _session.Memory.SetWramByte(0xcceb, 0);
                Script = Host = new(this, _session, _npcs, _data, "gettingRodOfSeasonsScript"); return false;
            case 1:
                byte[] values = _data.Table("sparkles"); int index = ReadByte(0x43) * 4;
                WriteByte(0x49, values[index]); WriteByte(0x5c, values[index + 1]);
                WriteByte(0x7b, values[index + 2]); WriteByte(0x50, values[index + 3]);
                WriteByte(0x46, 60); WriteByte(0x47, 90); Show(0x80); return false;
            case 2:
                Npc.SetCollisionRadii(4, 4); WriteByte(0x4f, 0xf0); WriteByte(0x46, 0); WriteByte(0x47, 0x30);
                var aura = _session.Entities.TryAllocateInteraction(() => new SeasonsRodAcquisitionRoomEntity(
                    _session, _npcs, _data, _source with { SubId = 3 }, _session.Room));
                if (aura is not null)
                { aura.WriteByte(0x56, 0x40); aura.WriteByte(0x57, 0xd0 + _session.Entities.NativeInteractionSlot(this)); Show(0x81); }
                return false;
            case 3: WriteByte(0x40, ReadByte(0x40) | 0x80); Show(0x82); return false;
            default: throw new NotSupportedException($"{_source.Source}: INTERAC$e6:${ReadByte(0x42):x2}.");
        }
    }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (ReadByte(0x42))
        {
            case 0:
                Script!.AdvanceObjectUpdate(frame.Player); if (Host!.Completed) Delete(); break;
            case 1:
                Npc.AdvanceAnimationUpdates(1);
                if (ReadByte(0x45) == 0)
                { if (!Tick(0x47)) return true; WriteByte(0x45, 1); }
                if (Tick(0x7b))
                { WriteByte(0x7b, 0x10); Sparkle(1); }
                Move(ReadByte(0x50), ReadByte(0x49));
                if (Tick(0x46)) Delete(); break;
            case 2: UpdateRod(frame.Player); break;
            case 3:
                if (_session.Memory.ReadWramByte(0xcceb) != 0) { Delete(); break; }
                int parent = ReadByte(0x57) << 8;
                for (int offset = 0x4a; offset <= 0x4f; offset++) WriteByte(offset, _session.Memory.ReadWramByte(parent | offset));
                Npc.AdvanceAnimationUpdates(1);
                if (Npc.CurrentAnimationParameter != 0)
                { Npc.ConsumeAnimationParameter(); WriteByte(0x5a, ReadByte(0x5a) ^ 0x80); Npc.SetScriptVisible((ReadByte(0x5a) & 0x80) != 0); }
                break;
        }
        if (!Finished) Npc.SetScriptDrawOffset(new(0, unchecked((sbyte)ReadByte(0x4f))));
        return true;
    }
    private void UpdateRod(Player player)
    {
        switch (ReadByte(0x45))
        {
            case 0:
                if ((_session.FrameCounter & 3) != 0) return;
                WriteByte(0x46, ReadByte(0x46) + 1);
                WriteByte(0x4f, _data.Table("bob")[ReadByte(0x46) & 15] + 0xf0);
                if (!Tick(0x47)) return;
                player.ClearNativeItemParents(); player.Face(Vector2I.Up);
                WriteByte(0x49, OracleObjectMovement.Shared.RelativeAngle(Npc.Position, player.Position));
                WriteByte(0x50, 0x14); WriteByte(0x45, 1); return;
            case 1:
                WriteByte(0x49, OracleObjectMovement.Shared.RelativeAngle(Npc.Position, player.Position));
                Move(ReadByte(0x50), ReadByte(0x49));
                if (Overlaps(player, ignoreZ: true)) { WriteByte(0x67, 6); WriteByte(0x45, 2); }
                return;
            case 2:
                int z = unchecked((short)Npc.NativeState!.Word(0x4e)), speed = unchecked((short)Npc.NativeState.Word(0x54));
                bool landed = OracleObjectMath.UpdateSpeedZ(ref z, ref speed, 8);
                Npc.NativeState.SetWord(0x4e, z); Npc.NativeState.SetWord(0x54, speed);
                if (!landed && !Overlaps(player, ignoreZ: false)) return;
                WriteByte(0x47, 30); WriteByte(0x45, 3); return;
            case 3:
                if (!Tick(0x47)) return;
                _session.Memory.SetField("wLinkForceState", 4); _session.Memory.SetField("wcc50", 0);
                WriteByte(0x45, 4); WriteByte(0x4b, (int)player.Position.Y - 14); WriteByte(0x4d, (int)player.Position.X - 4);
                WriteByte(0x4f, 0); WriteByte(0x50, 0);
                _session.ShowText(0x0071, _data.Text(0x0071));
                _session.Save.SetRoomFlag(_session.Rooms.ActiveGroup, _session.RoomId, 0x20);
                _session.Sound.PlaySound(SoundId.MusEssence); _session.Inventory.GiveTreasure(TreasureId.RodOfSeasons, 7);
                Script = Host = new(this, _session, _npcs, _data, "gettingRodOfSeasonsScript_setCounter1To32");
                Host.Darken(true); return;
            case 4:
                if (_session.Dialogue.IsOpen) return;
                WriteByte(0x45, 5); return;
            case 5:
                Script!.AdvanceObjectUpdate(player);
                if (!Host!.Completed) return;
                WriteByte(0x45, 6); WriteByte(0x47, 20); Host.Darken(false); return;
            case 6:
                if (_session.Transitions.NativePaletteFadeActive || !Tick(0x47)) return;
                _session.Memory.SetWramByte(0xcceb, 1); Delete(); return;
            default: throw new NotSupportedException($"getRodOfSeasons.s: $e6:$02 substate${ReadByte(0x45):x2}.");
        }
    }
    private bool Overlaps(Player player, bool ignoreZ) => !player.IsDying && (ignoreZ ||
        Math.Abs(unchecked((sbyte)ReadByte(0x4f)) - player.EnemyContactZ) < 8) &&
        unchecked((byte)((int)player.Position.Y - (int)Npc.Position.Y + ReadByte(0x66) + 6)) < (ReadByte(0x66) + 6) * 2 &&
        unchecked((byte)((int)player.Position.X - (int)Npc.Position.X + ReadByte(0x67) + 6)) < (ReadByte(0x67) + 6) * 2;
    private bool Tick(int offset) { WriteByte(offset, ReadByte(offset) - 1); return ReadByte(offset) == 0; }
    private void Sparkle(int subid) => _session.Entities.TryAllocateInteraction(() => new SeasonsTempleSparkleRoomEntity(
        _session, _npcs, _source with { Id = 0x84, SubId = subid, X = (int)Npc.Position.X, Y = (int)Npc.Position.Y }, _session.Room));
    private void Show(int visible)
    { WriteByte(0x5a, visible); Npc.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(visible)); Npc.SetScriptVisible(true); }
    public override void ReleaseNativeInteractionSlot() { Host?.CancelPalette(); base.ReleaseNativeInteractionSlot(); }
    protected override void AdvanceNpc(RoomEntityFrame frame) { }
}
