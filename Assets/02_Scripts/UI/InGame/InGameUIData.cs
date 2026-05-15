// CSV 한 줄에 들어 있는 인게임 UI 데이터를 보관한다.

using System;

[Serializable]
public sealed class InGameUIData
{
    public string Level { get; }
    public string MainMission { get; }
    public string SubMission1 { get; }
    public string SubMission2 { get; }
    public int Brawler { get; }
    public int Slasher { get; }
    public int Gunman { get; }
    public bool Order { get; }
    public bool HasSubMission1 => !string.IsNullOrWhiteSpace(SubMission1);
    public bool HasSubMission2 => !string.IsNullOrWhiteSpace(SubMission2);
    public bool CanUseOrder => Order;


    public InGameUIData(
        string level,
        string mainMission,
        string subMission1,
        string subMission2,
        int brawler,
        int slasher,
        int gunman,
        bool order)
    {
        Level = level;
        MainMission = mainMission;
        SubMission1 = subMission1;
        SubMission2 = subMission2;
        Brawler = brawler;
        Slasher = slasher;
        Gunman = gunman;
        Order = order;
    }
}
