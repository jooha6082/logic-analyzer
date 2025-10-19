// Models.cs
// Plain data containers. English only.

namespace LogicAnalyzer;

public readonly record struct Sample(
    int TimeNs,
    string IO,
    int nCE0,
    int ALE,
    int CLE,
    int nWE,
    int nRE,
    int RnB,
    int nWP,
    int DQS
)
{
    public int TimeNs { get; } = TimeNs;
    public string IO  { get; } = IO;
    public int nCE0 { get; } = nCE0;
    public int ALE  { get; } = ALE;
    public int CLE  { get; } = CLE;
    public int nWE  { get; } = nWE;
    public int nRE  { get; } = nRE;
    public int RnB  { get; } = RnB;
    public int nWP  { get; } = nWP;
    public int DQS  { get; } = DQS;
}

public sealed class ValidCmdRow
{
    public int Time { get; }
    public string CMD { get; }  // Erase/Program/Read/Reset/Unknown
    public string ADD1 { get; private set; } = "";
    public string ADD2 { get; private set; } = "";
    public string ADD3 { get; private set; } = "";
    public string ADD4 { get; private set; } = "";
    public string ADD5 { get; private set; } = "";
    public string ADD6 { get; private set; } = "";

    public ValidCmdRow(int time, string cmdName, IReadOnlyList<string> addrs)
    {
        Time = time;
        CMD = cmdName;
        if (addrs.Count > 0) ADD1 = addrs[0];
        if (addrs.Count > 1) ADD2 = addrs[1];
        if (addrs.Count > 2) ADD3 = addrs[2];
        if (addrs.Count > 3) ADD4 = addrs[3];
        if (addrs.Count > 4) ADD5 = addrs[4];
        if (addrs.Count > 5) ADD6 = addrs[5];
    }
}

// Rejects: explicit Command/Address rejections only
public sealed class CmdAddrReject
{
    public int Time { get; init; }              // WE rising time
    public string Kind { get; init; } = "";     // "Command" | "Address"
    public string Reason { get; init; } = "";   // e.g. tCMDHShort, AddressGatingFailed, AmbiguousCLE_ALE, NextCommandBefore6Addr, NonAddressWindow
    public string Code { get; init; } = "";     // IO hex at rising
    public double TAC_ns { get; init; }         // measured tCMDH/tADDH if available (F2 at write time)
    public int ParentCmdTime { get; init; }     // for address rejects; 0 for command rejects
    public string ParentCmdName { get; init; } = "";
}

// AC detect log entry for all pin-detected command/address windows
public sealed class AcDetectLog
{
    public int Time { get; init; }                    // rising WE time
    public string Stage { get; init; } = "";          // "Command" | "Address"
    public string Name { get; init; } = "";           // Command: Erase/... ; Address: "Address"
    public string Code { get; init; } = "";           // IO hex
    public double TAC_ns { get; init; }               // tCMDH/tADDH (F2 at write time)
    public int ParentCmdTime { get; init; }           // only for addresses
    public string ParentCmdName { get; init; } = "";  // only for addresses
}
