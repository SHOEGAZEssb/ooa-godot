using System.Buffers.Binary;
using System.Text;
using static Oracles.AgesSaveLayout;
using static Oracles.OracleSaveLayout;

namespace Oracles;

public sealed partial class OracleSaveData
{
    public int TimePortalGroup => ReadWramByte(AgesWramAddress.wPortalGroup);
    public int TimePortalRoom => ReadWramByte(AgesWramAddress.wPortalRoom);
    public int TimePortalPosition => ReadWramByte(AgesWramAddress.wPortalPos);
    public int MakuTreeState => ReadWramByte(AgesWramAddress.wMakuTreeState);
    public int MakuMapTextPast => ReadWramByte(AgesWramAddress.wMakuMapTextPast);
    public int MakuTreeSeedSatchelXPosition => ReadWramByte(AgesWramAddress.wMakuTreeSeedSatchelXPosition);

    internal static OracleSaveData CreateAgesGame(ReadOnlySpan<(int Address, byte Value)> transferred)
    {
        var save = new OracleSaveData(new byte[FileSize], OracleGame.Ages);
        foreach (var field in transferred) save.WriteWramByte(field.Address, field.Value);
        save.ApplyInitialFileVariables("common");
        int mode = save.ReadWramByte(AgesWramAddress.wFileIsLinkedGame) +
            2 * save.ReadWramByte(AgesWramAddress.wFileIsHeroGame);
        save.ApplyInitialFileVariables(mode switch
        {
            0 => "standardGame", 2 => "heroGame", 1 or 3 => "linkedGame",
            _ => throw new InvalidOperationException($"fileManagement.initializeFile: invalid mode ${mode:x2}.")
        });
        Array.Fill(save._data, (byte)0xff, UnappraisedRingsOffset, 0x40);
        for (int address = AgesWramAddress.wRingBoxContents; address <= AgesWramAddress.wActiveRing; address++)
            save.WriteWramByte(address, 0xff);
        // Only the unlinked hero variant receives the unappraised Victory Ring $36.
        if (mode == 2)
        {
            int address = 0xc69a + TreasureId.Ring / 8;
            save.WriteWramByte(address, (byte)(save.ReadWramByte(address) | (1 << (TreasureId.Ring & 7))));
            save.WriteWramByte(AgesWramAddress.wUnappraisedRings, 0x76);
        }
        BipinBlossomFamilyStateResolver.InitializeOnGameStart(save);
        new VineSproutDatabase().InitializePositions(save);
        // initializeFile falls through to saveFile, which marks Ages.
        save.WriteWramByte(AgesWramAddress.wWhichGame, 0x01);
        VerificationString.CopyTo(save._data.AsSpan(VerificationOffset));
        return save;
    }

    public void SetTimePortalLocation(int group, int room) =>
        SetTimePortalLocation(group, room, TimePortalPosition);

    public void SetTimePortalLocation(int group, int room, int position)
    {
        if (group is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(group));
        if (room is < 0 or >= RoomsPerFlagTable)
            throw new ArgumentOutOfRangeException(nameof(room));
        if (position is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(position));

        bool changed = WriteWramByte(AgesWramAddress.wPortalGroup, (byte)group);
        changed |= WriteWramByte(AgesWramAddress.wPortalRoom, (byte)room);
        changed |= WriteWramByte(AgesWramAddress.wPortalPos, (byte)position);
        if (changed)
            PublishChange();
    }

    public void ClearTimePortalLocation()
    {
        if (WriteWramByte(AgesWramAddress.wPortalGroup, 0xff))
            PublishChange();
    }

    public void SetMakuTreeState(int state)
    {
        if (state is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(state));
        if (WriteWramByte(AgesWramAddress.wMakuTreeState, (byte)state))
            PublishChange();
    }

    public void SetMakuMapTextPast(int textLow)
    {
        if (textLow is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(textLow));
        if (WriteWramByte(AgesWramAddress.wMakuMapTextPast, (byte)textLow))
            PublishChange();
    }

    public void SetMakuTreeSeedSatchelXPosition(int x)
    {
        if (x is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(x));
        if (WriteWramByte(AgesWramAddress.wMakuTreeSeedSatchelXPosition, (byte)x))
            PublishChange();
    }

    internal void MarkGameCompleted()
    {
        // scripts/common/scriptHelper.s:gameCompleteDialog_markGameAsComplete.
        bool changed=WriteWramByte(AgesWramAddress.wMapleKillCounter,0);
        changed|=WriteWramByte(AgesWramAddress.wFileIsCompleted,1);
        changed|=WriteWramByte(AgesWramAddress.wMakuMapTextPresent,0x1c);
        changed|=WriteWramByte(AgesWramAddress.wMakuMapTextPast,0x8c);
        SetGlobalFlag(AgesGlobalFlag.FinishedGame);
        if(changed)PublishChange();
    }

}
