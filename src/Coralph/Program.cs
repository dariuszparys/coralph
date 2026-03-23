using Coralph;
using Coralph.Ui;
using Serilog;

await ConsoleOutput.ConfigureForModeAsync(UiMode.Classic, new LoopOptions { UiMode = UiMode.Classic });

var (overrides, err, init, configFile, showHelp, showVersion) = ArgParser.Parse(args);

try
{
    if (showVersion)
    {
        ConsoleOutput.WriteLine($"Coralph {Banner.GetVersion()}");
        return 0;
    }

    if (overrides is null)
    {
        if (err is not null)
        {
            ConsoleOutput.WriteErrorLine(err);
            ConsoleOutput.WriteErrorLine();
        }

        var output = err is null ? ConsoleOutput.OutWriter : ConsoleOutput.ErrorWriter;
        ArgParser.PrintUsage(output);
        return showHelp && err is null ? 0 : 2;
    }

    if (!string.IsNullOrWhiteSpace(overrides.WorkingDir))
    {
        if (!WorkingDirectoryContext.TryApply(overrides.WorkingDir, out var repoRoot, out var workingDirError))
        {
            ConsoleOutput.WriteErrorLine(workingDirError);
            return 2;
        }

        ConsoleOutput.WriteLine($"Using working directory: {repoRoot}");
    }

    if (init)
    {
        return await InitWorkflow.RunAsync(configFile);
    }

    var opt = ConfigurationService.LoadOptions(overrides, configFile);
    if (!LoopOptionsRuntimePreparation.TryPrepare(opt, out var optionError))
    {
        ConsoleOutput.WriteErrorLine(optionError ?? "Invalid runtime options.");
        return 1;
    }

    await ConsoleOutput.ConfigureForModeAsync(UiModeResolver.Resolve(opt), opt);

    EventStreamWriter? eventStream = null;
    if (opt.StreamEvents)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        eventStream = new EventStreamWriter(Console.Out, sessionId);
        eventStream.WriteSessionHeader(Directory.GetCurrentDirectory());
        var errorConsole = ConsoleOutput.CreateConsole(Console.Error, Console.IsErrorRedirected);
        ConsoleOutput.Configure(errorConsole, errorConsole);
    }

    Logging.Configure(opt);
    Log.Information("Coralph starting with Model={Model}, MaxIterations={MaxIterations}", opt.Model, opt.MaxIterations);
    eventStream?.Emit("agent_start", fields: new Dictionary<string, object?>
    {
        ["model"] = opt.Model,
        ["maxIterations"] = opt.MaxIterations,
        ["version"] = Banner.GetVersion(),
        ["showReasoning"] = opt.ShowReasoning,
        ["colorizedOutput"] = opt.ColorizedOutput
    });

    using var cts = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelKeyPressHandler = (_, e) =>
    {
        e.Cancel = true;
        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
        }
    };

    Console.CancelKeyPress += cancelKeyPressHandler;
    var exitCode = 1;
    try
    {
        var orchestrator = new LoopOrchestrator(opt, eventStream);
        exitCode = await orchestrator.RunAsync(cts);
        return exitCode;
    }
    finally
    {
        Console.CancelKeyPress -= cancelKeyPressHandler;
        eventStream?.Emit("agent_end", fields: new Dictionary<string, object?> { ["exitCode"] = exitCode });
        Logging.Close();
    }
}
finally
{
    await ConsoleOutput.DisposeBackendAsync();
}
