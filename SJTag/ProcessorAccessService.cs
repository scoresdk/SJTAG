namespace SJTag;

public sealed class ProcessorAccessService
{
    private readonly SjtagClient client;
    private readonly ProbeMemory memory;
    private bool sawClear;

    public bool Ready { get; private set; }
    public long Events { get; private set; }
    public uint? LastAddress { get; private set; }
    internal bool SawMailboxClear => sawClear;

    public ProcessorAccessService(SjtagClient client, ProbeMemory memory)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(memory);

        this.client = client;
        this.memory = memory;
    }

    public void ResetReadiness()
    {
        Ready = false;
        sawClear = false;
    }

    internal void AdoptMailboxPolling()
    {
        sawClear = true;
    }

    internal void RestoreReadiness(long events, bool clear, bool ready)
    {
        if (events < 0 || (ready && (!clear || memory.Read(Mailbox.Flag) != 0)))
            throw new ArgumentException("Invalid ready-session snapshot.");

        Ready = ready;
        sawClear = clear;
        Events = events;
    }

    public bool ServiceOne()
    {
        LastAddress = null;

        DebugStatus status = client.GetStatus();

        if (!status.DebugMode)
            throw new InvalidOperationException($"CPU left debug mode: {status}.");

        if (!status.AccessPending)
            return false;

        uint address = client.Transfer(Instruction.Address, 0);
        bool clear = false;
        bool ready = false;

        if (status.CpuWrite)
        {
            uint value = client.Transfer(Instruction.Data, 0);
            memory.Write(address, value, status.AccessSize);
            clear = address == Mailbox.Flag && value == 0;
        }
        else
        {
            uint value = memory.Read(address, status.AccessSize);
            client.Transfer(Instruction.Data, value);
            ready = address == Mailbox.Flag && sawClear;
        }

        client.Transfer(Instruction.Control, ControlRequest.Acknowledge);

        sawClear |= clear;
        Ready |= ready;
        Events++;
        LastAddress = address;

        return true;
    }
}
