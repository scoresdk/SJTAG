using System.Diagnostics;

namespace SJTag;

public sealed partial class DebugSession
{
    public void Resume(CancellationToken cancellationToken = default) =>
        Return(false, cancellationToken);

    public void RunUntilDebugEntry(CancellationToken cancellationToken = default) =>
        Return(true, cancellationToken);

    public DebugReason Step(CancellationToken cancellationToken = default)
    {
        Context.SetSingleStep(true);

        Return(true, cancellationToken);

        Context.SetSingleStep(false);

        if ((Reason & DebugReason.SingleStep) == 0)
            throw new InvalidOperationException($"Expected single-step entry, observed {Reason}.");

        return Reason;
    }

    private void Return(bool expectEntry, CancellationToken cancellationToken, Action? onRunning = null)
    {
        RequireReady();
        cancellationToken.ThrowIfCancellationRequested();

        memory.Write(Mailbox.Opcode, 0);
        memory.Write(Mailbox.Flag, 1);

        service.ResetReadiness();
        State = DebugSessionState.Servicing;

        long start = Stopwatch.GetTimestamp();
        bool newEntry = false;

        try
        {
            while (true)
            {
                CheckDeadline(start, cancellationToken);

                var status = client.GetStatus();

                if (!status.DebugMode)
                {
                    if (!expectEntry)
                    {
                        State = DebugSessionState.Running;
                        onRunning?.Invoke();

                        return;
                    }

                    continue;
                }

                try
                {
                    service.ServiceOne();
                }
                catch (InvalidOperationException) when (!client.GetStatus().DebugMode)
                {
                    continue;
                }

                if (!newEntry && service.LastAddress == 0xFF000000)
                {
                    memory = new ProbeMemory(profile.Image, alternateLanes);
                    service = new ProcessorAccessService(client, memory);
                    newEntry = true;

                    if (!expectEntry)
                        throw new InvalidOperationException("Unexpected debug re-entry.");
                }

                if (newEntry && service.Ready)
                {
                    _ = SavedRegisters.Decode(memory.Words);
                    State = DebugSessionState.Ready;

                    return;
                }
            }
        }
        catch
        {
            State = DebugSessionState.Faulted;

            throw;
        }
    }
}
