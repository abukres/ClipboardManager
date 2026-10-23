namespace ClipboardManagerWinForm;

static class Program
{
    private const string MutexName = @"Local\ClipboardManager.SingleInstance";
    private const string ActivationEventName = @"Local\ClipboardManager.Activate";

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Only one instance may own the clipboard monitoring chain and the tray icon.
        using var mutex = new Mutex(true, MutexName, out var isFirstInstance);

        if (!isFirstInstance)
        {
            // A second launch asks the running instance to show its configuration window.
            SignalRunningInstance();
            return;
        }

        ApplicationConfiguration.Initialize();

        var context = new ClipboardApplicationContext();

        using var activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        var registration = ThreadPool.RegisterWaitForSingleObject(
            activationEvent,
            (_, _) => context.RequestShowConfiguration(),
            null,
            Timeout.Infinite,
            false);

        // The configuration window can be opened directly, which is handy for shortcuts and demos.
        if (HasCommandLineArgument("--config") || HasCommandLineArgument("/config"))
        {
            context.ShowConfiguration();
        }

        Application.Run(context);

        registration.Unregister(null);
    }

    private static void SignalRunningInstance()
    {
        if (EventWaitHandle.TryOpenExisting(ActivationEventName, out var handle))
        {
            using (handle)
            {
                handle.Set();
            }
        }
    }

    private static bool HasCommandLineArgument(string argument)
    {
        return Environment.GetCommandLineArgs().Any(value =>
            value.Equals(argument, StringComparison.OrdinalIgnoreCase));
    }
}
