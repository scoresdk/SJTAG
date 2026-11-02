using CH347;
using SJTag;
using Spectre.Console;

namespace Encore;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            bool extract = false;
            bool write = false;
            bool verify = false;
            bool erase = false;
            bool adoptHalted = false;

            string? input = null;
            string? output = null;
            string? adapterName = null;

            IScoreDevice? device = null;

            foreach (string arg in args)
            {
                if (arg.StartsWith("-M"))
                    device = arg[2..] switch
                    {
                        "hyperscan" => new Hyperscan(),
                        _ => throw new ArgumentException("Invalid device.")
                    };
                else if (arg.StartsWith("-I"))
                    input = arg[2..];
                else if (arg.StartsWith("-O"))
                    output = arg[2..];
                else if (arg.StartsWith("-A"))
                    adapterName = arg[2..];
                else if (arg == "-X")
                    extract = true;
                else if (arg == "-W")
                    write = true;
                else if (arg == "-V")
                    verify = true;
                else if (arg == "-E")
                    erase = true;
                else if (arg == "-R")
                    adoptHalted = true;
                else
                    throw new ArgumentException($"Unknown argument: {arg}");
            }

            if (extract && (write || verify || erase))
                throw new ArgumentException("Extract mode can only be used alone.");

            if (!extract && !write && !verify && !erase)
                throw new ArgumentException("Select -X, -W, -V, or -E.");

            if (adoptHalted && !(write || verify || erase))
                throw new ArgumentException("-R can only be used with -W, -V, or -E.");

            if (device is null)
                throw new ArgumentException("Select a device with -M.");

            if (extract && string.IsNullOrWhiteSpace(output))
                throw new ArgumentException("Extraction requires -O followed by an output path.");

            if ((write || verify) && string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Writing/verifying requires -I followed by an input path.");

            int size = checked((int)device.GetFlashSize());
            byte[]? desired = null;

            if (write || verify)
            {
                long length = new FileInfo(input!).Length;
                FlashImageOperations.ValidateSize(length, size);

                desired = File.ReadAllBytes(input!);
                FlashImageOperations.ValidateSize(desired.Length, size);
            }

            if (extract)
            {
                output = Path.GetFullPath(output!);

                if (!Directory.Exists(Path.GetDirectoryName(output)))
                    throw new DirectoryNotFoundException("Output directory does not exist.");
            }

            if (device is not Hyperscan)
                throw new NotSupportedException("No NOR profile for this device.");

            var profile = new SST800();

            if (size != profile.ByteCount)
                throw new InvalidOperationException("Device and flash profile sizes disagree.");

            using var adapter = Ch347Transport.Open(Ch347Options.Fast);

            using var client = adoptHalted
                ? ((Hyperscan)device).MountHaltedSession(adapter)
                : device.Mount(adapter);
            var flash = new NorFlash(new DmaFlashBus(client), profile);
            EncoreRamNorProgrammer? ramProgrammer = write
                ? new EncoreRamNorProgrammer(client, profile, flash)
                : null;

            AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn())
                .Start(ctx =>
                {
                    if (extract)
                    {
                        var task = ctx.AddTask("Extracting flash", maxValue: size);
                        byte[] bytes = flash.ReadImage(done => task.Value = done);

                        using var file = new FileStream(
                            output!,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None);

                        file.Write(bytes);
                        file.Flush(flushToDisk: true);
                    }

                    if (erase)
                    {
                        var erasing = ctx.AddTask("Waiting for chip erase", maxValue: size);
                        erasing.IsIndeterminate = true;

                        flash.EraseChip(
                            verificationProgress: done => erasing.Value = done,
                            eraseCompleted: () =>
                            {
                                erasing.Description = "Checking flash";
                                erasing.IsIndeterminate = false;
                            });

                        erasing.Value = size;
                    }

                    if (write)
                    {
                        var reading = ctx.AddTask(
                            "Checking range",
                            maxValue: desired!.Length);

                        var programming = ctx.AddTask(
                            "Programming",
                            maxValue: desired.Length,
                            autoStart: false);

                        var verifying = ctx.AddTask(
                            "Reading back",
                            maxValue: desired.Length,
                            autoStart: false);

                        int changes = ramProgrammer!.ProgramPrefix(
                            desired,
                            done => reading.Value = done,
                            done =>
                            {
                                if (!programming.IsStarted)
                                    programming.StartTask();

                                programming.Value = done;

                                if (done == desired.Length)
                                    programming.Description = "Reading back";
                            },
                            done =>
                            {
                                if (!verifying.IsStarted)
                                    verifying.StartTask();

                                verifying.Value = done;
                            });

                        programming.Value = desired.Length;
                        programming.Description = "Programming";
                    }

                    if (verify)
                    {
                        var task = ctx.AddTask("Verifying flash ", maxValue: desired!.Length);

                        FlashImageOperations.Verify(
                            desired,
                            flash.Read(0, desired.Length, done => task.Value = done));
                    }
                });

            Console.WriteLine("Done -- Halted.");

            if (write || verify)
                Console.WriteLine("Please power-cycle the device.");

            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            Console.Error.WriteLine("Operation failed!");

            return 1;
        }
    }
}
