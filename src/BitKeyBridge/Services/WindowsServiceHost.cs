using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BitKeyBridge;

public static class WindowsServiceHost
{
    public const string ServiceName = "BitKeyBridge";
    public const string DisplayName = "BitKeyBridge BitLocker Recovery Service";

    private const uint ScManagerConnect = 0x0001;
    private const uint ScManagerCreateService = 0x0002;
    private const uint ServiceQueryConfig = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceStart = 0x0010;
    private const uint ServiceStop = 0x0020;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceDelete = 0x00010000;
    private const uint ServiceWin32OwnProcess = 0x00000010;
    private const uint ServiceAutoStart = 0x00000002;
    private const uint ServiceErrorNormal = 0x00000001;
    private const uint ServiceNoChange = 0xFFFFFFFF;

    private const uint ServiceControlStop = 0x00000001;
    private const uint ServiceControlShutdown = 0x00000005;
    private const uint ServiceAcceptStop = 0x00000001;
    private const uint ServiceAcceptShutdown = 0x00000004;

    private const uint ServiceStopped = 0x00000001;
    private const uint ServiceStartPending = 0x00000002;
    private const uint ServiceStopPending = 0x00000003;
    private const uint ServiceRunning = 0x00000004;

    private const int ScStatusProcessInfo = 0;
    private const int ErrorServiceDoesNotExist = 1060;
    private const int ErrorServiceAlreadyRunning = 1056;
    private const int ErrorServiceNotActive = 1062;

    private const uint ServiceConfigDescription = 1;
    private const uint ServiceConfigFailureActions = 2;
    private const int ScActionNone = 0;
    private const int ScActionRestart = 1;

    private static ServiceMainDelegate? _serviceMain;
    private static HandlerExDelegate? _handler;
    private static IntPtr _statusHandle;
    private static CancellationTokenSource? _serviceCts;
    private static AppConfig? _serviceConfig;
    private static AppLogger? _serviceLog;

    public static void InstallOrUpdate()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows Service installation is only available on Windows.");

        var source = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current executable path is unavailable.");

        WindowsEventLogService.EnsureSource();
        Directory.CreateDirectory(AppPaths.ServiceInstallDirectory);

        if (!string.Equals(
                Path.GetFullPath(source),
                Path.GetFullPath(AppPaths.ServiceExecutable),
                StringComparison.OrdinalIgnoreCase))
        {
            var temp = AppPaths.ServiceExecutable + ".new";
            File.Copy(source, temp, true);
            File.Move(temp, AppPaths.ServiceExecutable, true);
        }

        var binaryPath = Quote(AppPaths.ServiceExecutable) + " --service";

        using var scm = OpenScManager(ScManagerConnect | ScManagerCreateService);
        using var existing = OpenServiceSafe(
            scm.DangerousGetHandle(),
            ServiceName,
            ServiceQueryStatus | ServiceChangeConfig | ServiceStart | ServiceStop | ServiceDelete);

        if (!existing.IsInvalid)
        {
            if (!ChangeServiceConfig(
                    existing.DangerousGetHandle(),
                    ServiceNoChange,
                    ServiceAutoStart,
                    ServiceNoChange,
                    binaryPath,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null,
                    DisplayName))
                ThrowLastWin32("Failed to update the BitKeyBridge Windows Service configuration.");
            ConfigureServiceHardening(existing.DangerousGetHandle());
            WindowsEventLogService.TryWrite(
                "BitKeyBridge Windows Service configuration updated.",
                EventLogSeverity.Information,
                4001,
                "Service");
            return;
        }

        var service = CreateService(
            scm.DangerousGetHandle(),
            ServiceName,
            DisplayName,
            0x000F01FF,
            ServiceWin32OwnProcess,
            ServiceAutoStart,
            ServiceErrorNormal,
            binaryPath,
            null,
            IntPtr.Zero,
            null,
            null,
            null);

        if (service == IntPtr.Zero)
            ThrowLastWin32("Failed to create the BitKeyBridge Windows Service.");

        ConfigureServiceHardening(service);
        CloseServiceHandle(service);
        WindowsEventLogService.TryWrite(
            "BitKeyBridge Windows Service installed.",
            EventLogSeverity.Information,
            4000,
            "Service");
    }

    public static void Uninstall()
    {
        using var scm = OpenScManager(ScManagerConnect);
        using var service = OpenServiceSafe(
            scm.DangerousGetHandle(),
            ServiceName,
            ServiceQueryStatus | ServiceStop | ServiceDelete);
        if (service.IsInvalid)
        {
            if (Marshal.GetLastWin32Error() == ErrorServiceDoesNotExist) return;
            ThrowLastWin32("Failed to open the BitKeyBridge Windows Service.");
        }

        try { Stop(); } catch { }
        if (!DeleteService(service.DangerousGetHandle()))
            ThrowLastWin32("Failed to delete the BitKeyBridge Windows Service.");
        WindowsEventLogService.TryWrite(
            "BitKeyBridge Windows Service uninstalled.",
            EventLogSeverity.Information,
            4004,
            "Service");
    }

    public static void Start()
    {
        using var scm = OpenScManager(ScManagerConnect);
        using var service = OpenServiceRequired(scm.DangerousGetHandle(), ServiceName, ServiceStart | ServiceQueryStatus);
        if (!StartService(service.DangerousGetHandle(), 0, null))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorServiceAlreadyRunning)
                throw new Win32Exception(error, "Failed to start the BitKeyBridge Windows Service.");
        }
        WaitForState(ServiceRunning, TimeSpan.FromSeconds(30));
        WindowsEventLogService.TryWrite(
            "BitKeyBridge Windows Service started.",
            EventLogSeverity.Information,
            4002,
            "Service");
    }

    public static void Stop()
    {
        using var scm = OpenScManager(ScManagerConnect);
        using var service = OpenServiceRequired(scm.DangerousGetHandle(), ServiceName, ServiceStop | ServiceQueryStatus);
        var status = new SERVICE_STATUS();
        if (!ControlService(service.DangerousGetHandle(), ServiceControlStop, ref status))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorServiceNotActive)
                throw new Win32Exception(error, "Failed to stop the BitKeyBridge Windows Service.");
        }
        WaitForState(ServiceStopped, TimeSpan.FromSeconds(30));
        WindowsEventLogService.TryWrite(
            "BitKeyBridge Windows Service stopped.",
            EventLogSeverity.Information,
            4003,
            "Service");
    }

    public static ServiceInfo GetInfo()
    {
        using var scm = OpenScManager(ScManagerConnect);
        using var service = OpenServiceSafe(
            scm.DangerousGetHandle(),
            ServiceName,
            ServiceQueryStatus | ServiceQueryConfig);
        if (service.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorServiceDoesNotExist)
                return new ServiceInfo();
            throw new Win32Exception(error, "Failed to query the BitKeyBridge Windows Service.");
        }

        var status = QueryStatus(service.DangerousGetHandle());
        return new ServiceInfo
        {
            Installed = true,
            State = StateName(status.dwCurrentState),
            BinaryPath = AppPaths.ServiceExecutable,
            Identity = QueryIdentity(service.DangerousGetHandle())
        };
    }

    public static void ConfigureIdentity(
        string mode,
        string? account,
        string? password,
        bool restartIfRunning = true)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Windows Service identity configuration is only available on Windows.");
        if (!SecurityContext.IsAdministrator())
            throw new InvalidOperationException(
                "Administrator rights are required to change the Windows Service identity.");

        var normalized = string.IsNullOrWhiteSpace(mode)
            ? "LocalSystem"
            : mode.Trim();

        string serviceAccount;
        string? servicePassword;

        if (normalized.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase))
        {
            serviceAccount = "LocalSystem";
            servicePassword = string.Empty;
        }
        else if (normalized.Equals("gMSA", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Equals("ManagedAccount", StringComparison.OrdinalIgnoreCase))
        {
            serviceAccount = (account ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(serviceAccount))
                throw new ArgumentException(
                    "A gMSA / managed service account name is required.");
            if (!serviceAccount.EndsWith("$", StringComparison.Ordinal))
                throw new ArgumentException(
                    "A gMSA account name must end with '$' (for example DOMAIN\\BitKeyBridgeSvc$).");

            servicePassword = null;
        }
        else if (normalized.Equals("DomainAccount", StringComparison.OrdinalIgnoreCase))
        {
            serviceAccount = (account ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(serviceAccount))
                throw new ArgumentException(
                    "A domain service account name is required.");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException(
                    "A password is required when configuring a regular domain service account.");

            servicePassword = password;
        }
        else
        {
            throw new ArgumentException(
                "Service identity mode must be LocalSystem, gMSA, or DomainAccount.");
        }

        var before = GetInfo();
        if (!before.Installed)
            throw new InvalidOperationException(
                "Install the BitKeyBridge Windows Service before changing its identity.");

        var wasRunning = string.Equals(
            before.State,
            "Running",
            StringComparison.OrdinalIgnoreCase);

        if (wasRunning)
            Stop();

        try
        {
            using var scm = OpenScManager(ScManagerConnect);
            using var service = OpenServiceRequired(
                scm.DangerousGetHandle(),
                ServiceName,
                ServiceChangeConfig |
                ServiceQueryConfig |
                ServiceQueryStatus |
                ServiceStart |
                ServiceStop);

            if (!ChangeServiceConfig(
                    service.DangerousGetHandle(),
                    ServiceNoChange,
                    ServiceNoChange,
                    ServiceNoChange,
                    null,
                    null,
                    IntPtr.Zero,
                    null,
                    serviceAccount,
                    servicePassword,
                    null))
            {
                ThrowLastWin32(
                    "Failed to change the BitKeyBridge Windows Service identity.");
            }

            WindowsEventLogService.TryWrite(
                $"BitKeyBridge Windows Service identity changed to {serviceAccount}.",
                EventLogSeverity.Warning,
                4005,
                "Service");
        }
        finally
        {
            if (restartIfRunning && wasRunning)
                Start();
        }
    }

    public static int RunService(AppConfig config)
    {
        _serviceConfig = config;
        _serviceMain = ServiceMain;
        _handler = HandlerEx;

        var table = new[]
        {
            new SERVICE_TABLE_ENTRY
            {
                lpServiceName = ServiceName,
                lpServiceProc = _serviceMain
            },
            new SERVICE_TABLE_ENTRY()
        };

        if (!StartServiceCtrlDispatcher(table))
            return Marshal.GetLastWin32Error();

        return 0;
    }

    private static void ServiceMain(int argc, IntPtr argv)
    {
        _statusHandle = RegisterServiceCtrlHandlerEx(ServiceName, _handler!, IntPtr.Zero);
        if (_statusHandle == IntPtr.Zero) return;

        _serviceCts = new CancellationTokenSource();
        _serviceLog = new AppLogger(AppPaths.ServiceLogFile, 10);
        _serviceLog.Initialize();

        SetRuntimeStatus(ServiceStartPending, 0, 3000);
        SetRuntimeStatus(ServiceRunning, ServiceAcceptStop | ServiceAcceptShutdown, 0);
        WindowsEventLogService.TryWrite(
            "BitKeyBridge service runtime entered Running state.",
            EventLogSeverity.Information,
            4010,
            "Service");

        try
        {
            RunWorkerAsync(_serviceConfig ?? ConfigService.LoadAppConfig(), _serviceCts.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _serviceLog.Error(ex.ToString());
            WindowsEventLogService.TryWrite(
                "BitKeyBridge service runtime failed: " + ex.Message,
                EventLogSeverity.Error,
                4099,
                "Service");
        }
        finally
        {
            SetRuntimeStatus(ServiceStopped, 0, 0);
            _serviceCts.Dispose();
            _serviceCts = null;
        }
    }

    private static uint HandlerEx(uint control, uint eventType, IntPtr eventData, IntPtr context)
    {
        if (control is ServiceControlStop or ServiceControlShutdown)
        {
            SetRuntimeStatus(ServiceStopPending, 0, 15000);
            try { _serviceCts?.Cancel(); } catch { }
        }
        return 0;
    }

    private static async Task RunWorkerAsync(AppConfig config, CancellationToken ct)
    {
        HealthHttpServer? health = null;
        RemoteApiServer? remoteApi = null;
        try
        {
            if (config.HealthEndpointEnabled)
            {
                health = new HealthHttpServer(config);
                health.Start(ct);
                _serviceLog?.Info(
                    $"Health endpoint listening on 127.0.0.1:{Math.Clamp(config.HealthEndpointPort, 1024, 65535)}.");
            }

            if (config.RemoteApiEnabled)
            {
                remoteApi = new RemoteApiServer(config);
                remoteApi.Start(ct);
                _serviceLog?.Info(
                    $"Remote API listening with TLS on TCP {Math.Clamp(config.RemoteApiPort, 1024, 65535)}. Management={config.RemoteApiAllowManagement}.");
                WindowsEventLogService.TryWrite(
                    $"Remote API started on TCP {Math.Clamp(config.RemoteApiPort, 1024, 65535)}. Management={config.RemoteApiAllowManagement}.",
                    EventLogSeverity.Information,
                    4302,
                    "RemoteAPI");
            }

            var first = true;
            while (!ct.IsCancellationRequested)
            {
                if (!first || config.ServiceRunExportOnStart)
                {
                    try
                    {
                        _serviceLog?.Info("Scheduled BitLocker export started.");
                        var export = new ExportService(config);
                        var result = await export.RunAsync(false, false, null, null, ct);
                        if (result.Success)
                        {
                            _serviceLog?.Info($"Scheduled export completed. Rows={result.ValidRows}; DC={result.AdServer}.");
                            WindowsEventLogService.TryWrite(
                                $"Scheduled BitLocker export completed. Rows={result.ValidRows}; DC={result.AdServer}; Published={result.Published}.",
                                EventLogSeverity.Information,
                                4200,
                                "Export");
                        }
                        else
                        {
                            _serviceLog?.Error("Scheduled export failed: " + result.ErrorMessage);
                            WindowsEventLogService.TryWrite(
                                "Scheduled BitLocker export failed: " + result.ErrorMessage,
                                EventLogSeverity.Error,
                                4299,
                                "Export");
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _serviceLog?.Error("Scheduled export exception: " + ex);
                        WindowsEventLogService.TryWrite(
                            "Scheduled BitLocker export exception: " + ex.Message,
                            EventLogSeverity.Error,
                            4298,
                            "Export");
                    }
                }

                first = false;
                var minutes = Math.Clamp(config.ServiceIntervalMinutes, 1, 10080);
                await Task.Delay(TimeSpan.FromMinutes(minutes), ct);
            }
        }
        finally
        {
            remoteApi?.Dispose();
            health?.Dispose();
        }
    }

    private static void ConfigureServiceHardening(IntPtr service)
    {
        var description = new SERVICE_DESCRIPTION
        {
            lpDescription =
                "BitKeyBridge exports BitLocker recovery metadata from Active Directory, " +
                "hosts optional monitoring endpoints, and performs scheduled health checks."
        };
        if (!ChangeServiceConfig2Description(
                service,
                ServiceConfigDescription,
                ref description))
            ThrowLastWin32("Failed to configure the BitKeyBridge service description.");

        var actionSize = Marshal.SizeOf<SC_ACTION>();
        var actionsPointer = Marshal.AllocHGlobal(actionSize * 3);
        try
        {
            Marshal.StructureToPtr(
                new SC_ACTION { Type = ScActionRestart, Delay = 5000 },
                actionsPointer,
                false);
            Marshal.StructureToPtr(
                new SC_ACTION { Type = ScActionRestart, Delay = 30000 },
                IntPtr.Add(actionsPointer, actionSize),
                false);
            Marshal.StructureToPtr(
                new SC_ACTION { Type = ScActionNone, Delay = 0 },
                IntPtr.Add(actionsPointer, actionSize * 2),
                false);

            var failureActions = new SERVICE_FAILURE_ACTIONS
            {
                dwResetPeriod = 86400,
                lpRebootMsg = null,
                lpCommand = null,
                cActions = 3,
                lpsaActions = actionsPointer
            };

            if (!ChangeServiceConfig2FailureActions(
                    service,
                    ServiceConfigFailureActions,
                    ref failureActions))
                ThrowLastWin32("Failed to configure BitKeyBridge service failure recovery.");
        }
        finally
        {
            Marshal.FreeHGlobal(actionsPointer);
        }
    }

    private static void WaitForState(uint expected, TimeSpan timeout)
    {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            var info = GetInfo();
            var current = info.State;
            if (string.Equals(current, StateName(expected), StringComparison.OrdinalIgnoreCase))
                return;
            Thread.Sleep(250);
        }
        throw new TimeoutException($"Windows Service did not reach state {StateName(expected)} within {timeout.TotalSeconds:0} seconds.");
    }

    private static string QueryIdentity(IntPtr service)
    {
        _ = QueryServiceConfig(
            service,
            IntPtr.Zero,
            0,
            out var bytesNeeded);
        var error = Marshal.GetLastWin32Error();
        if (bytesNeeded <= 0)
        {
            if (error != 122)
                throw new Win32Exception(
                    error,
                    "Failed to determine the Windows Service configuration size.");
            return string.Empty;
        }

        var buffer = Marshal.AllocHGlobal(bytesNeeded);
        try
        {
            if (!QueryServiceConfig(service, buffer, bytesNeeded, out _))
                ThrowLastWin32("Failed to query the BitKeyBridge Windows Service configuration.");

            var config = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(buffer);
            return config.lpServiceStartName == IntPtr.Zero
                ? string.Empty
                : Marshal.PtrToStringUni(config.lpServiceStartName) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static SERVICE_STATUS_PROCESS QueryStatus(IntPtr service)
    {
        var size = Marshal.SizeOf<SERVICE_STATUS_PROCESS>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!QueryServiceStatusEx(service, ScStatusProcessInfo, buffer, size, out _))
                ThrowLastWin32("Failed to query the BitKeyBridge Windows Service state.");
            return Marshal.PtrToStructure<SERVICE_STATUS_PROCESS>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void SetRuntimeStatus(uint state, uint acceptedControls, uint waitHint)
    {
        if (_statusHandle == IntPtr.Zero) return;
        var status = new SERVICE_STATUS
        {
            dwServiceType = ServiceWin32OwnProcess,
            dwCurrentState = state,
            dwControlsAccepted = acceptedControls,
            dwWin32ExitCode = 0,
            dwServiceSpecificExitCode = 0,
            dwCheckPoint = state is ServiceStartPending or ServiceStopPending ? 1u : 0u,
            dwWaitHint = waitHint
        };
        SetServiceStatus(_statusHandle, ref status);
    }

    private static string StateName(uint state) => state switch
    {
        ServiceStopped => "Stopped",
        ServiceStartPending => "StartPending",
        ServiceStopPending => "StopPending",
        ServiceRunning => "Running",
        5 => "ContinuePending",
        6 => "PausePending",
        7 => "Paused",
        _ => "Unknown"
    };

    private static SafeServiceHandle OpenScManager(uint access)
    {
        var handle = OpenSCManager(null, null, access);
        if (handle == IntPtr.Zero)
            ThrowLastWin32("Failed to open Windows Service Control Manager.");
        return new SafeServiceHandle(handle);
    }

    private static SafeServiceHandle OpenServiceRequired(IntPtr scm, string name, uint access)
    {
        var service = OpenServiceSafe(scm, name, access);
        if (service.IsInvalid)
            ThrowLastWin32($"Windows Service '{name}' is not installed or cannot be opened.");
        return service;
    }

    private static SafeServiceHandle OpenServiceSafe(IntPtr scm, string name, uint access) =>
        new(OpenService(scm, name, access));

    private static void ThrowLastWin32(string message) =>
        throw new Win32Exception(Marshal.GetLastWin32Error(), message);

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private sealed class SafeServiceHandle : SafeHandle
    {
        public SafeServiceHandle() : base(IntPtr.Zero, true) { }
        public SafeServiceHandle(IntPtr handle) : base(IntPtr.Zero, true) => SetHandle(handle);
        public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SERVICE_DESCRIPTION
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SC_ACTION
    {
        public int Type;
        public uint Delay;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SERVICE_FAILURE_ACTIONS
    {
        public uint dwResetPeriod;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpRebootMsg;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpCommand;
        public uint cActions;
        public IntPtr lpsaActions;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QUERY_SERVICE_CONFIG
    {
        public uint dwServiceType;
        public uint dwStartType;
        public uint dwErrorControl;
        public IntPtr lpBinaryPathName;
        public IntPtr lpLoadOrderGroup;
        public uint dwTagId;
        public IntPtr lpDependencies;
        public IntPtr lpServiceStartName;
        public IntPtr lpDisplayName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS_PROCESS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void ServiceMainDelegate(int argc, IntPtr argv);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint HandlerExDelegate(uint control, uint eventType, IntPtr eventData, IntPtr context);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SERVICE_TABLE_ENTRY
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpServiceName;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public ServiceMainDelegate? lpServiceProc;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateService(
        IntPtr scm,
        string serviceName,
        string displayName,
        uint desiredAccess,
        uint serviceType,
        uint startType,
        uint errorControl,
        string binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr scm, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "QueryServiceConfigW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceConfig(
        IntPtr service,
        IntPtr queryServiceConfig,
        int bufferSize,
        out int bytesNeeded);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig(
        IntPtr service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string? binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password,
        string? displayName);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig2Description(
        IntPtr service,
        uint infoLevel,
        ref SERVICE_DESCRIPTION info);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig2FailureActions(
        IntPtr service,
        uint infoLevel,
        ref SERVICE_FAILURE_ACTIONS info);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartService(IntPtr service, int numArgs, string[]? args);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ControlService(IntPtr service, uint control, ref SERVICE_STATUS status);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteService(IntPtr service);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(
        IntPtr service,
        int infoLevel,
        IntPtr buffer,
        int bufferSize,
        out int bytesNeeded);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartServiceCtrlDispatcher([In] SERVICE_TABLE_ENTRY[] serviceTable);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr RegisterServiceCtrlHandlerEx(
        string serviceName,
        HandlerExDelegate callback,
        IntPtr context);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetServiceStatus(IntPtr statusHandle, ref SERVICE_STATUS status);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
 (for example DOMAIN\\BitKeyBridgeSvc$).");
            servicePassword = null;
        }
        else if (normalized.Equals("DomainAccount", StringComparison.OrdinalIgnoreCase))
        {
            serviceAccount = (account ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(serviceAccount))
                throw new ArgumentException("A domain service account name is required.");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException(
                    "A password is required when configuring a regular domain service account.");
            servicePassword = password;
        }
        else
        {
            throw new ArgumentException(
                "Service identity mode must be LocalSystem, gMSA, or DomainAccount.");
        }

        var before = GetInfo();
        if (!before.Installed)
            throw new InvalidOperationException(
                "Install the BitKeyBridge Windows Service before changing its identity.");

        var wasRunning = string.Equals(
            before.State,
            "Running",
            StringComparison.OrdinalIgnoreCase);

        if (wasRunning)
            Stop();

        try
        {
            using var scm = OpenScManager(ScManagerConnect);
            using var service = OpenServiceRequired(
                scm.DangerousGetHandle(),
                ServiceName,
                ServiceChangeConfig | ServiceQueryConfig | ServiceQueryStatus | ServiceStart | ServiceStop);

            if (!ChangeServiceConfig(
                    service.DangerousGetHandle(),
                    ServiceNoChange,
                    ServiceNoChange,
                    ServiceNoChange,
                    null,
                    null,
                    IntPtr.Zero,
                    null,
                    serviceAccount,
                    servicePassword,
                    null))
                ThrowLastWin32("Failed to change the BitKeyBridge Windows Service identity.");

            WindowsEventLogService.TryWrite(
                $"BitKeyBridge Windows Service identity changed to {serviceAccount}.",
                EventLogSeverity.Warning,
                4005,
                "Service");
        }
        finally
        {
            if (restartIfRunning && wasRunning)
                Start();
        }
    }

    public static int RunService(AppConfig config)
    {
        _serviceConfig = config;
        _serviceMain = ServiceMain;
        _handler = HandlerEx;

        var table = new[]
        {
            new SERVICE_TABLE_ENTRY { lpServiceName = ServiceName, lpServiceProc = _serviceMain },
            new SERVICE_TABLE_ENTRY()
        };

        if (!StartServiceCtrlDispatcher(table))
            return Marshal.GetLastWin32Error();

        return 0;
    }

    private static void ServiceMain(int argc, IntPtr argv)
    {
        _statusHandle = RegisterServiceCtrlHandlerEx(ServiceName, _handler!, IntPtr.Zero);
        if (_statusHandle == IntPtr.Zero) return;

        _serviceCts = new CancellationTokenSource();
        _serviceLog = new AppLogger(AppPaths.ServiceLogFile, 10);
        _serviceLog.Initialize();

        SetRuntimeStatus(ServiceStartPending, 0, 3000);
        SetRuntimeStatus(ServiceRunning, ServiceAcceptStop | ServiceAcceptShutdown, 0);
        WindowsEventLogService.TryWrite(
            "BitKeyBridge service runtime entered Running state.",
            EventLogSeverity.Information,
            4010,
            "Service");

        try
        {
            RunWorkerAsync(_serviceConfig ?? ConfigService.LoadAppConfig(), _serviceCts.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _serviceLog.Error(ex.ToString());
            WindowsEventLogService.TryWrite(
                "BitKeyBridge service runtime failed: " + ex.Message,
                EventLogSeverity.Error,
                4099,
                "Service");
        }
        finally
        {
            SetRuntimeStatus(ServiceStopped, 0, 0);
            _serviceCts.Dispose();
            _serviceCts = null;
        }
    }

    private static uint HandlerEx(uint control, uint eventType, IntPtr eventData, IntPtr context)
    {
        if (control is ServiceControlStop or ServiceControlShutdown)
        {
            SetRuntimeStatus(ServiceStopPending, 0, 15000);
            try { _serviceCts?.Cancel(); } catch { }
        }
        return 0;
    }

    private static async Task RunWorkerAsync(AppConfig config, CancellationToken ct)
    {
        HealthHttpServer? health = null;
        RemoteApiServer? remoteApi = null;
        try
        {
            if (config.HealthEndpointEnabled)
            {
                health = new HealthHttpServer(config);
                health.Start(ct);
                _serviceLog?.Info(
                    $"Health endpoint listening on 127.0.0.1:{Math.Clamp(config.HealthEndpointPort, 1024, 65535)}.");
            }

            if (config.RemoteApiEnabled)
            {
                remoteApi = new RemoteApiServer(config);
                remoteApi.Start(ct);
                _serviceLog?.Info(
                    $"Remote API listening with TLS on TCP {Math.Clamp(config.RemoteApiPort, 1024, 65535)}. Management={config.RemoteApiAllowManagement}.");
                WindowsEventLogService.TryWrite(
                    $"Remote API started on TCP {Math.Clamp(config.RemoteApiPort, 1024, 65535)}. Management={config.RemoteApiAllowManagement}.",
                    EventLogSeverity.Information,
                    4302,
                    "RemoteAPI");
            }

            var first = true;
            while (!ct.IsCancellationRequested)
            {
                if (!first || config.ServiceRunExportOnStart)
                {
                    try
                    {
                        _serviceLog?.Info("Scheduled BitLocker export started.");
                        var export = new ExportService(config);
                        var result = await export.RunAsync(false, false, null, null, ct);
                        if (result.Success)
                        {
                            _serviceLog?.Info($"Scheduled export completed. Rows={result.ValidRows}; DC={result.AdServer}.");
                            WindowsEventLogService.TryWrite(
                                $"Scheduled BitLocker export completed. Rows={result.ValidRows}; DC={result.AdServer}; Published={result.Published}.",
                                EventLogSeverity.Information,
                                4200,
                                "Export");
                        }
                        else
                        {
                            _serviceLog?.Error("Scheduled export failed: " + result.ErrorMessage);
                            WindowsEventLogService.TryWrite(
                                "Scheduled BitLocker export failed: " + result.ErrorMessage,
                                EventLogSeverity.Error,
                                4299,
                                "Export");
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _serviceLog?.Error("Scheduled export exception: " + ex);
                        WindowsEventLogService.TryWrite(
                            "Scheduled BitLocker export exception: " + ex.Message,
                            EventLogSeverity.Error,
                            4298,
                            "Export");
                    }
                }

                first = false;
                var minutes = Math.Clamp(config.ServiceIntervalMinutes, 1, 10080);
                await Task.Delay(TimeSpan.FromMinutes(minutes), ct);
            }
        }
        finally
        {
            remoteApi?.Dispose();
            health?.Dispose();
        }
    }

    private static void ConfigureServiceHardening(IntPtr service)
    {
        var description = new SERVICE_DESCRIPTION
        {
            lpDescription =
                "BitKeyBridge exports BitLocker recovery metadata from Active Directory, " +
                "hosts optional monitoring endpoints, and performs scheduled health checks."
        };
        if (!ChangeServiceConfig2Description(
                service,
                ServiceConfigDescription,
                ref description))
            ThrowLastWin32("Failed to configure the BitKeyBridge service description.");

        var actionSize = Marshal.SizeOf<SC_ACTION>();
        var actionsPointer = Marshal.AllocHGlobal(actionSize * 3);
        try
        {
            Marshal.StructureToPtr(
                new SC_ACTION { Type = ScActionRestart, Delay = 5000 },
                actionsPointer,
                false);
            Marshal.StructureToPtr(
                new SC_ACTION { Type = ScActionRestart, Delay = 30000 },
                IntPtr.Add(actionsPointer, actionSize),
                false);
            Marshal.StructureToPtr(
                new SC_ACTION { Type = ScActionNone, Delay = 0 },
                IntPtr.Add(actionsPointer, actionSize * 2),
                false);

            var failureActions = new SERVICE_FAILURE_ACTIONS
            {
                dwResetPeriod = 86400,
                lpRebootMsg = null,
                lpCommand = null,
                cActions = 3,
                lpsaActions = actionsPointer
            };

            if (!ChangeServiceConfig2FailureActions(
                    service,
                    ServiceConfigFailureActions,
                    ref failureActions))
                ThrowLastWin32("Failed to configure BitKeyBridge service failure recovery.");
        }
        finally
        {
            Marshal.FreeHGlobal(actionsPointer);
        }
    }

    private static void WaitForState(uint expected, TimeSpan timeout)
    {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            var info = GetInfo();
            var current = info.State;
            if (string.Equals(current, StateName(expected), StringComparison.OrdinalIgnoreCase))
                return;
            Thread.Sleep(250);
        }
        throw new TimeoutException($"Windows Service did not reach state {StateName(expected)} within {timeout.TotalSeconds:0} seconds.");
    }

    private static SERVICE_STATUS_PROCESS QueryStatus(IntPtr service)
    {
        var size = Marshal.SizeOf<SERVICE_STATUS_PROCESS>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!QueryServiceStatusEx(service, ScStatusProcessInfo, buffer, size, out _))
                ThrowLastWin32("Failed to query the BitKeyBridge Windows Service state.");
            return Marshal.PtrToStructure<SERVICE_STATUS_PROCESS>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void SetRuntimeStatus(uint state, uint acceptedControls, uint waitHint)
    {
        if (_statusHandle == IntPtr.Zero) return;
        var status = new SERVICE_STATUS
        {
            dwServiceType = ServiceWin32OwnProcess,
            dwCurrentState = state,
            dwControlsAccepted = acceptedControls,
            dwWin32ExitCode = 0,
            dwServiceSpecificExitCode = 0,
            dwCheckPoint = state is ServiceStartPending or ServiceStopPending ? 1u : 0u,
            dwWaitHint = waitHint
        };
        SetServiceStatus(_statusHandle, ref status);
    }

    private static string StateName(uint state) => state switch
    {
        ServiceStopped => "Stopped",
        ServiceStartPending => "StartPending",
        ServiceStopPending => "StopPending",
        ServiceRunning => "Running",
        5 => "ContinuePending",
        6 => "PausePending",
        7 => "Paused",
        _ => "Unknown"
    };

    private static SafeServiceHandle OpenScManager(uint access)
    {
        var handle = OpenSCManager(null, null, access);
        if (handle == IntPtr.Zero)
            ThrowLastWin32("Failed to open Windows Service Control Manager.");
        return new SafeServiceHandle(handle);
    }

    private static SafeServiceHandle OpenServiceRequired(IntPtr scm, string name, uint access)
    {
        var service = OpenServiceSafe(scm, name, access);
        if (service.IsInvalid)
            ThrowLastWin32($"Windows Service '{name}' is not installed or cannot be opened.");
        return service;
    }

    private static SafeServiceHandle OpenServiceSafe(IntPtr scm, string name, uint access) =>
        new(OpenService(scm, name, access));

    private static void ThrowLastWin32(string message) =>
        throw new Win32Exception(Marshal.GetLastWin32Error(), message);

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private sealed class SafeServiceHandle : SafeHandle
    {
        public SafeServiceHandle() : base(IntPtr.Zero, true) { }
        public SafeServiceHandle(IntPtr handle) : base(IntPtr.Zero, true) => SetHandle(handle);
        public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SERVICE_DESCRIPTION
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SC_ACTION
    {
        public int Type;
        public uint Delay;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SERVICE_FAILURE_ACTIONS
    {
        public uint dwResetPeriod;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpRebootMsg;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpCommand;
        public uint cActions;
        public IntPtr lpsaActions;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS_PROCESS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void ServiceMainDelegate(int argc, IntPtr argv);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint HandlerExDelegate(uint control, uint eventType, IntPtr eventData, IntPtr context);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SERVICE_TABLE_ENTRY
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpServiceName;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public ServiceMainDelegate? lpServiceProc;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateService(
        IntPtr scm,
        string serviceName,
        string displayName,
        uint desiredAccess,
        uint serviceType,
        uint startType,
        uint errorControl,
        string binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr scm, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig(
        IntPtr service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string? binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password,
        string? displayName);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig2Description(
        IntPtr service,
        uint infoLevel,
        ref SERVICE_DESCRIPTION info);

    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig2FailureActions(
        IntPtr service,
        uint infoLevel,
        ref SERVICE_FAILURE_ACTIONS info);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartService(IntPtr service, int numArgs, string[]? args);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ControlService(IntPtr service, uint control, ref SERVICE_STATUS status);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteService(IntPtr service);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(
        IntPtr service,
        int infoLevel,
        IntPtr buffer,
        int bufferSize,
        out int bytesNeeded);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartServiceCtrlDispatcher([In] SERVICE_TABLE_ENTRY[] serviceTable);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr RegisterServiceCtrlHandlerEx(
        string serviceName,
        HandlerExDelegate callback,
        IntPtr context);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetServiceStatus(IntPtr statusHandle, ref SERVICE_STATUS status);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
