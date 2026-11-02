using CH347;
using SJTag;

public static class Program
{
    public static void Main(string[] args)
    {
        using var adapter = Ch347Transport.Open(Ch347Options.Fast);
        var jtag = new SjtagClient(adapter);

        jtag.Enable();
        jtag.Halt();

        Console.WriteLine($"0xA0000000: 0x{jtag.ReadCpuWord(0xA000_0000):X8}");
    }
}
