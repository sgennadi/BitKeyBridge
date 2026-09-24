using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(x => x.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                          x.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                          x.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            ConsoleHelper.EnsureConsole();
            PrintHelp();
            return 0;
        }

        if (args.Any(x => x.Equals("--version", StringComparison.OrdinalIgnoreCase)))
        {
            ConsoleHelper.EnsureConsole();
            Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");
            return 0;
        }

        if (args.Any(x => x.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            ConsoleHelper.EnsureConsole();
            return RunSelfTest();
        }

        var isCli = args.Any(x =>
            x.Equals("--cli", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--dry-run", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--dc-test", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--install-service", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--uninstall-service", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--start-service", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--stop-service", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--health", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--check-update", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--update", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--ad-test", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--cloud-machine-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--cloud-machine-save", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--cloud-machine-delete", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--cert-key-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--cert-key-grant", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--cert-key-revoke", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--rbac-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--rbac-enable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--rbac-disable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--rbac-admin-bypass", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--rbac-reader-add", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--rbac-reader-remove", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--rbac-rotator-add", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--rbac-rotator-remove", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-ui-admin-add", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-ui-admin-remove", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-verify", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-signing-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-signing-setup", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-signing-sign", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-signing-verify", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-signing-disable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-signing-rollover", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--remote-token-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--remote-token-generate", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--remote-token-revoke", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--config-backup", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--config-restore", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--diagnostics-bundle", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--incident-verify", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-run", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-dry-run", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-enable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-disable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-run-on-start", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-no-run-on-start", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-interval-hours", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--incident-retention-days", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--backup-retention-days", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--backup-minimum-files", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--temp-retention-days", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--storage-acl-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--storage-acl-repair", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--privileged-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--jit-grant", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--jit-revoke", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--jit-enable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--jit-disable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--jit-minutes", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--jit-grantor-add", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--jit-grantor-remove", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approval-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approval-approve", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approval-deny", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approval-enable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approval-disable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approval-minutes", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approver-add", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approver-remove", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-flush", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-enable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-disable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-mode", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-file", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-webhook", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-client-cert", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-timeout-seconds", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-flush-minutes", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-max-outbox", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-fail-closed", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-fail-open", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--entra-cert-rollover", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--vault-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--vault-save-user", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--vault-save-machine", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--vault-delete-user", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--vault-delete-machine", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-identity-local-system", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-identity-gmsa", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-identity-user", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-coverage-enable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-coverage-disable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-coverage-interval", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-coverage-run-on-start", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-coverage-no-run-on-start", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-enable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-disable", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-max-no-key", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-max-unencrypted", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-max-stale", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-max-old-key", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-severity-no-key", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-severity-unencrypted", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-severity-stale", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-severity-old-key", StringComparison.OrdinalIgnoreCase));
        var needsConsole = isCli || args.Any(x =>
            x.Equals("--ad-password-prompt", StringComparison.OrdinalIgnoreCase));
        if (needsConsole) ConsoleHelper.EnsureConsole();

        var applyUpdatePlanRequested =
            args.Any(
                x =>
                    x.Equals(
                        "--apply-update-plan",
                        StringComparison.OrdinalIgnoreCase));

        if (applyUpdatePlanRequested)
        {
            var applyPlanPath =
                GetOptionValue(
                    args,
                    "--apply-update-plan");
            var applyPlanSha256 =
                GetOptionValue(
                    args,
                    "--apply-update-plan-sha256");

            if (string.IsNullOrWhiteSpace(
                    applyPlanPath) ||
                string.IsNullOrWhiteSpace(
                    applyPlanSha256))
            {
                return 2;
            }

            return UpdateService.ApplyPlan(
                applyPlanPath,
                applyPlanSha256);
        }

        AppConfig config;
        try
        {
            config =
                ConfigService.LoadAppConfig();
        }
        catch (Exception ex)
            when (ex is ConfigurationLoadException or
                  FutureConfigurationSchemaException)
        {
            WindowsEventLogService.TryWrite(
                "BitKeyBridge startup blocked because application configuration could not be loaded safely. " +
                ex.Message,
                EventLogSeverity.Error,
                4613,
                "Configuration");

            var message =
                "BitKeyBridge will not start with default settings because the existing application configuration could not be loaded safely." +
                Environment.NewLine +
                Environment.NewLine +
                ex.Message +
                Environment.NewLine +
                Environment.NewLine +
                "Repair or restore appsettings.json, then start BitKeyBridge again.";

            if (needsConsole ||
                !Environment.UserInteractive)
            {
                Console.Error.WriteLine(
                    message);
            }
            else
            {
                MessageBox.Show(
                    message,
                    "BitKeyBridge Configuration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return 12;
        }

        if (args.Any(x => x.Equals("--service", StringComparison.OrdinalIgnoreCase)))
            return WindowsServiceHost.RunService(config);

        var noElevation = args.Any(x => x.Equals("--no-elevation", StringComparison.OrdinalIgnoreCase));
        var readOnlyStatusCommand = args.Any(x =>
            x.Equals("--health", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--service-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--check-update", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--dc-test", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--ad-test", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--cloud-machine-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--cert-key-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--coverage-policy-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--rbac-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-verify", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-signing-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--audit-signing-verify", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--remote-token-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--config-backup", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--diagnostics-bundle", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--incident-verify", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--housekeeping-dry-run", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--storage-acl-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--privileged-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approval-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-status", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--vault-status", StringComparison.OrdinalIgnoreCase));
        var currentUserVaultCommand = args.Any(x =>
            x.Equals("--vault-save-user", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--vault-delete-user", StringComparison.OrdinalIgnoreCase));
        var identityBoundCommand = args.Any(x =>
            x.Equals("--jit-grant", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--jit-revoke", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approval-approve", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--approval-deny", StringComparison.OrdinalIgnoreCase) ||
            x.Equals("--siem-flush", StringComparison.OrdinalIgnoreCase));
        if (!noElevation &&
            !readOnlyStatusCommand &&
            !currentUserVaultCommand &&
            !identityBoundCommand &&
            !SecurityContext.IsAdministrator())
        {
            if (!Environment.UserInteractive)
            {
                if (isCli) Console.Error.WriteLine("Administrative rights are required. Configure the scheduled task to run with highest privileges.");
                return 5;
            }

            if (SecurityContext.RelaunchElevated(args.Concat(["--no-elevation"]).ToArray())) return 0;
            if (isCli)
            {
                Console.Error.WriteLine("Administrative rights are required.");
                return 5;
            }
        }

        ApplySessionCommandLine(config, args);

        if (args.Any(x => x.Equals("--install-service", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                WindowsServiceHost.InstallOrUpdate();
                WindowsServiceHost.Start();
                Console.WriteLine($"Installed and started {WindowsServiceHost.DisplayName}.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--uninstall-service", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                WindowsServiceHost.Uninstall();
                Console.WriteLine($"Uninstalled {WindowsServiceHost.DisplayName}.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--start-service", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                WindowsServiceHost.Start();
                Console.WriteLine("BitKeyBridge service state: " + WindowsServiceHost.GetInfo().State);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--stop-service", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                WindowsServiceHost.Stop();
                Console.WriteLine("BitKeyBridge service state: " + WindowsServiceHost.GetInfo().State);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--service-status", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var service = WindowsServiceHost.GetInfo();
                Console.WriteLine(service.Installed
                    ? $"Installed; State={service.State}; Identity={service.Identity}; Binary={service.BinaryPath}"
                    : "Not installed");
                return service.Installed ? 0 : 3;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (args.Any(x => x.Equals("--cloud-machine-status", StringComparison.OrdinalIgnoreCase)))
            return MachineCloudConfigService.ShowStatus();

        if (args.Any(x => x.Equals("--cloud-machine-save", StringComparison.OrdinalIgnoreCase)))
            return MachineCloudConfigService.Save(args);

        if (args.Any(x => x.Equals("--cloud-machine-delete", StringComparison.OrdinalIgnoreCase)))
            return MachineCloudConfigService.Delete();

        if (args.Any(x => x.Equals("--cert-key-status", StringComparison.OrdinalIgnoreCase)))
            return CertificateKeyCliService.ShowStatus(args);

        if (args.Any(x => x.Equals("--cert-key-grant", StringComparison.OrdinalIgnoreCase)))
            return CertificateKeyCliService.Grant(args);

        if (args.Any(x => x.Equals("--cert-key-revoke", StringComparison.OrdinalIgnoreCase)))
            return CertificateKeyCliService.Revoke(args);

        if (args.Any(x => x.Equals("--rbac-status", StringComparison.OrdinalIgnoreCase)))
            return RbacCliService.ShowStatus(config);

        if (args.Any(x => x.Equals("--audit-verify", StringComparison.OrdinalIgnoreCase)))
        {
            var integrity =
                AuditIntegrityService.VerifyAndPersist(
                    AppPaths.AuditLogFile);
            Console.WriteLine(
                JsonSerializer.Serialize(
                    integrity,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
            return integrity.Valid ? 0 : 4;
        }

        if (args.Any(x => x.Equals("--audit-signing-status", StringComparison.OrdinalIgnoreCase)))
            return AuditSigningCliService.ShowStatus(config);

        if (args.Any(x => x.Equals("--audit-signing-setup", StringComparison.OrdinalIgnoreCase)))
            return AuditSigningCliService.Setup(config, args);

        if (args.Any(x => x.Equals("--audit-signing-sign", StringComparison.OrdinalIgnoreCase)))
            return AuditSigningCliService.Sign(config);

        if (args.Any(x => x.Equals("--audit-signing-verify", StringComparison.OrdinalIgnoreCase)))
            return AuditSigningCliService.Verify(config);

        if (args.Any(x => x.Equals("--audit-signing-disable", StringComparison.OrdinalIgnoreCase)))
            return AuditSigningCliService.Disable(config);

        if (args.Any(x => x.Equals("--audit-signing-rollover", StringComparison.OrdinalIgnoreCase)))
            return AuditSigningCliService.Rollover(config, args);

        if (args.Any(x => x.Equals(
                "--remote-token-status",
                StringComparison.OrdinalIgnoreCase)))
        {
            return RemoteApiCliService.ShowTokenStatus(
                config);
        }

        var generateRemoteScope =
            GetOptionValue(
                args,
                "--remote-token-generate");
        if (args.Any(x => x.Equals(
                "--remote-token-generate",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    generateRemoteScope))
            {
                Console.Error.WriteLine(
                    "--remote-token-generate requires <read|coverage-run|export>.");
                return 2;
            }

            return RemoteApiCliService.GenerateScopedToken(
                config,
                generateRemoteScope);
        }

        var revokeRemoteScope =
            GetOptionValue(
                args,
                "--remote-token-revoke");
        if (args.Any(x => x.Equals(
                "--remote-token-revoke",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    revokeRemoteScope))
            {
                Console.Error.WriteLine(
                    "--remote-token-revoke requires <read|coverage-run|export>.");
                return 2;
            }

            return RemoteApiCliService.RevokeScopedToken(
                config,
                revokeRemoteScope);
        }

        var configBackupPath =
            GetOptionValue(
                args,
                "--config-backup");
        if (args.Any(x => x.Equals(
                "--config-backup",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    configBackupPath))
            {
                Console.Error.WriteLine(
                    "--config-backup requires <path>.");
                return 2;
            }

            return ConfigurationMaintenanceCliService.Backup(
                configBackupPath);
        }

        var configRestorePath =
            GetOptionValue(
                args,
                "--config-restore");
        if (args.Any(x => x.Equals(
                "--config-restore",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    configRestorePath))
            {
                Console.Error.WriteLine(
                    "--config-restore requires <path>.");
                return 2;
            }

            return ConfigurationMaintenanceCliService.Restore(
                configRestorePath);
        }

        var diagnosticsPath =
            GetOptionValue(
                args,
                "--diagnostics-bundle");
        if (args.Any(x => x.Equals(
                "--diagnostics-bundle",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    diagnosticsPath))
            {
                Console.Error.WriteLine(
                    "--diagnostics-bundle requires <path>.");
                return 2;
            }

            return ConfigurationMaintenanceCliService.Diagnostics(
                diagnosticsPath);
        }

        var incidentSessionId =
            GetOptionValue(
                args,
                "--incident-verify");
        if (args.Any(x => x.Equals(
                "--incident-verify",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    incidentSessionId))
            {
                Console.Error.WriteLine(
                    "--incident-verify requires <session-id>.");
                return 2;
            }

            return RecoveryIncidentCliService.Verify(
                incidentSessionId);
        }

        if (args.Any(x => x.Equals(
                "--privileged-status",
                StringComparison.OrdinalIgnoreCase)))
        {
            return PrivilegedAccessCliService.ShowStatus(
                config);
        }

        var jitSubject =
            GetOptionValue(
                args,
                "--jit-grant");
        if (args.Any(x => x.Equals(
                "--jit-grant",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    jitSubject))
            {
                Console.Error.WriteLine(
                    "--jit-grant requires <DOMAIN\\user|group|SID>.");
                return 2;
            }

            return PrivilegedAccessCliService.GrantJit(
                config,
                jitSubject,
                GetOptionValue(
                    args,
                    "--jit-reason"));
        }

        var jitGrantId =
            GetOptionValue(
                args,
                "--jit-revoke");
        if (args.Any(x => x.Equals(
                "--jit-revoke",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    jitGrantId))
            {
                Console.Error.WriteLine(
                    "--jit-revoke requires <grant-id>.");
                return 2;
            }

            return PrivilegedAccessCliService.RevokeJit(
                config,
                jitGrantId,
                GetOptionValue(
                    args,
                    "--jit-reason"));
        }

        var approvalStatusSession =
            GetOptionValue(
                args,
                "--approval-status");
        if (args.Any(x => x.Equals(
                "--approval-status",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    approvalStatusSession))
            {
                Console.Error.WriteLine(
                    "--approval-status requires <session-id>.");
                return 2;
            }

            return PrivilegedAccessCliService.ShowApproval(
                config,
                approvalStatusSession);
        }

        var approvalApproveSession =
            GetOptionValue(
                args,
                "--approval-approve");
        if (args.Any(x => x.Equals(
                "--approval-approve",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    approvalApproveSession))
            {
                Console.Error.WriteLine(
                    "--approval-approve requires <session-id>.");
                return 2;
            }

            return PrivilegedAccessCliService.DecideApproval(
                config,
                approvalApproveSession,
                approved: true,
                GetOptionValue(
                    args,
                    "--approval-comment"));
        }

        var approvalDenySession =
            GetOptionValue(
                args,
                "--approval-deny");
        if (args.Any(x => x.Equals(
                "--approval-deny",
                StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(
                    approvalDenySession))
            {
                Console.Error.WriteLine(
                    "--approval-deny requires <session-id>.");
                return 2;
            }

            return PrivilegedAccessCliService.DecideApproval(
                config,
                approvalDenySession,
                approved: false,
                GetOptionValue(
                    args,
                    "--approval-comment"));
        }

        if (args.Any(x => x.Equals(
                "--siem-status",
                StringComparison.OrdinalIgnoreCase)))
        {
            return PrivilegedAccessCliService.ShowSiemStatus(
                config);
        }

        if (args.Any(x => x.Equals(
                "--siem-flush",
                StringComparison.OrdinalIgnoreCase)))
        {
            return PrivilegedAccessCliService
                .FlushSiemAsync(
                    config)
                .GetAwaiter()
                .GetResult();
        }

        if (args.Any(x =>
                x.Equals("--jit-enable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--jit-disable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--jit-minutes", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--jit-grantor-add", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--jit-grantor-remove", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--approval-enable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--approval-disable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--approval-minutes", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--approver-add", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--approver-remove", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-enable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-disable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-mode", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-file", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-webhook", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-client-cert", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-timeout-seconds", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-flush-minutes", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-max-outbox", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-fail-closed", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--siem-fail-open", StringComparison.OrdinalIgnoreCase)))
        {
            return PrivilegedAccessCliService.ApplySettings(
                config,
                args);
        }

        if (args.Any(x => x.Equals(
                "--housekeeping-status",
                StringComparison.OrdinalIgnoreCase)))
        {
            return HousekeepingCliService.ShowStatus(
                config);
        }

        if (args.Any(x => x.Equals(
                "--housekeeping-dry-run",
                StringComparison.OrdinalIgnoreCase)))
        {
            return HousekeepingCliService.Run(
                config,
                dryRun: true);
        }

        if (args.Any(x => x.Equals(
                "--housekeeping-run",
                StringComparison.OrdinalIgnoreCase)))
        {
            return HousekeepingCliService.Run(
                config,
                dryRun: false);
        }

        if (args.Any(x => x.Equals(
                "--storage-acl-status",
                StringComparison.OrdinalIgnoreCase)))
        {
            return HousekeepingCliService.ShowStorageAclStatus(
                config);
        }

        if (args.Any(x => x.Equals(
                "--storage-acl-repair",
                StringComparison.OrdinalIgnoreCase)))
        {
            return HousekeepingCliService.RepairStorageAcl(
                config);
        }

        if (args.Any(x =>
                x.Equals("--housekeeping-enable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--housekeeping-disable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--housekeeping-run-on-start", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--housekeeping-no-run-on-start", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--housekeeping-interval-hours", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--incident-retention-days", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--backup-retention-days", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--backup-minimum-files", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--temp-retention-days", StringComparison.OrdinalIgnoreCase)))
        {
            return HousekeepingCliService.ApplySettings(
                config,
                args);
        }

        if (args.Any(x => x.Equals(
                "--entra-cert-rollover",
                StringComparison.OrdinalIgnoreCase)))
        {
            return EntraCertificateLifecycleCliService.Rollover();
        }

        if (args.Any(x =>
                x.Equals("--rbac-enable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-disable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-admin-bypass", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-reader-add", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-reader-remove", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-rotator-add", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-rotator-remove", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-ui-admin-add", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--rbac-ui-admin-remove", StringComparison.OrdinalIgnoreCase)))
        {
            return RbacCliService.Apply(config, args);
        }

        if (args.Any(x => x.Equals("--vault-status", StringComparison.OrdinalIgnoreCase)))
            return ShowVaultStatus(config);

        if (args.Any(x => x.Equals("--vault-save-user", StringComparison.OrdinalIgnoreCase)))
            return SaveVaultCredential(config, "CurrentUser");

        if (args.Any(x => x.Equals("--vault-save-machine", StringComparison.OrdinalIgnoreCase)))
            return SaveVaultCredential(config, "LocalMachine");

        if (args.Any(x => x.Equals("--vault-delete-user", StringComparison.OrdinalIgnoreCase)))
            return DeleteVaultCredential(config, "CurrentUser");

        if (args.Any(x => x.Equals("--vault-delete-machine", StringComparison.OrdinalIgnoreCase)))
            return DeleteVaultCredential(config, "LocalMachine");

        if (args.Any(x => x.Equals("--service-identity-local-system", StringComparison.OrdinalIgnoreCase)))
            return ConfigureServiceIdentityCli(config, "LocalSystem", null);

        var hasGmsaOption = args.Any(x =>
            x.Equals("--service-identity-gmsa", StringComparison.OrdinalIgnoreCase));
        var gmsaAccount = GetOptionValue(args, "--service-identity-gmsa");
        if (hasGmsaOption)
        {
            if (string.IsNullOrWhiteSpace(gmsaAccount))
            {
                Console.Error.WriteLine(
                    "--service-identity-gmsa requires <DOMAIN\\account$>.");
                return 2;
            }
            return ConfigureServiceIdentityCli(config, "gMSA", gmsaAccount);
        }

        var hasServiceUserOption = args.Any(x =>
            x.Equals("--service-identity-user", StringComparison.OrdinalIgnoreCase));
        var serviceUser = GetOptionValue(args, "--service-identity-user");
        if (hasServiceUserOption)
        {
            if (string.IsNullOrWhiteSpace(serviceUser))
            {
                Console.Error.WriteLine(
                    "--service-identity-user requires <DOMAIN\\user>.");
                return 2;
            }
            return ConfigureServiceIdentityCli(
                config,
                "DomainAccount",
                serviceUser);
        }

        if (args.Any(x =>
                x.Equals("--service-coverage-enable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--service-coverage-disable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--service-coverage-interval", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--service-coverage-run-on-start", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--service-coverage-no-run-on-start", StringComparison.OrdinalIgnoreCase)))
        {
            return ConfigureServiceCoverageCli(config, args);
        }

        if (args.Any(x => x.Equals("--coverage-policy-status", StringComparison.OrdinalIgnoreCase)))
            return CoveragePolicyCliService.ShowStatus(config);

        if (args.Any(x =>
                x.Equals("--coverage-policy-enable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--coverage-policy-disable", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--coverage-policy-max-no-key", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--coverage-policy-max-unencrypted", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--coverage-policy-max-stale", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--coverage-policy-max-old-key", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--coverage-policy-severity-no-key", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--coverage-policy-severity-unencrypted", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--coverage-policy-severity-stale", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("--coverage-policy-severity-old-key", StringComparison.OrdinalIgnoreCase)))
        {
            return CoveragePolicyCliService.Configure(config, args);
        }

        if (args.Any(x => x.Equals("--health", StringComparison.OrdinalIgnoreCase)))
        {
            var health = new HealthService(config).GetSnapshot();
            Console.WriteLine(JsonSerializer.Serialize(health, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            return health.OverallStatus == "Error" ? 2 : 0;
        }

        if (args.Any(x => x.Equals("--check-update", StringComparison.OrdinalIgnoreCase)))
            return CheckUpdateAsync(config).GetAwaiter().GetResult();

        if (args.Any(x => x.Equals("--update", StringComparison.OrdinalIgnoreCase)))
            return InstallUpdateAsync(config).GetAwaiter().GetResult();

        if (args.Any(x => x.Equals("--ad-test", StringComparison.OrdinalIgnoreCase)))
            return RunAdTest(config);

        if (args.Any(x => x.Equals("--dc-test", StringComparison.OrdinalIgnoreCase)))
            return RunDcTest(config, args).GetAwaiter().GetResult();

        if (args.Any(x => x.Equals("--coverage", StringComparison.OrdinalIgnoreCase)))
            return CoverageCliService.RunAsync(config, args).GetAwaiter().GetResult();

        if (isCli)
            return RunExport(config, args).GetAwaiter().GetResult();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(config));
        return 0;
    }

    private static async Task<int> RunExport(AppConfig config, string[] args)
    {
        var dryRun = args.Any(x => x.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
        var force = args.Any(x => x.Equals("--force-publish", StringComparison.OrdinalIgnoreCase));
        var scopes = ParseScopes(args);
        var progress = new Progress<string>(m => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {m}"));
        var service = new ExportService(config);
        var result = await service.RunAsync(dryRun, force, scopes, progress);
        Console.WriteLine(result.Success
            ? $"Completed. Rows={result.ValidRows}; DC={result.AdServer}; Published={result.Published}"
            : "FAILED: " + result.ErrorMessage);
        return result.ExitCode;
    }

    private static async Task<int> RunDcTest(AppConfig config, string[] args)
    {
        var scopes = ParseScopes(args);
        var progress = new Progress<string>(m => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {m}"));
        var service = new DomainControllerComparisonService(config);
        try
        {
            var rows = await service.RunAsync(scopes, progress);
            foreach (var row in rows)
            {
                Console.WriteLine($"{row.Name,-18} Site={row.Site,-18} RODC={row.IsReadOnly,-5} Status={row.Status,-8} Objects={row.ObjectsFound,-8} ReplErrors={row.ReplicationErrors}");
            }
            return rows.Any(x => x.Status == "ERROR") ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<int> CheckUpdateAsync(AppConfig config)
    {
        try
        {
            using var updater = new UpdateService(config);
            var info = await updater.CheckAsync();
            Console.WriteLine(JsonSerializer.Serialize(info, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            return string.IsNullOrWhiteSpace(info.Error) ? (info.UpdateAvailable ? 10 : 0) : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<int> InstallUpdateAsync(AppConfig config)
    {
        try
        {
            using var updater = new UpdateService(config);
            var info = await updater.CheckAsync();
            if (!string.IsNullOrWhiteSpace(info.Error))
            {
                Console.Error.WriteLine(info.Error);
                return 1;
            }

            if (!info.UpdateAvailable)
            {
                Console.WriteLine($"BitKeyBridge {info.CurrentVersion} is already current.");
                return 0;
            }

            Console.WriteLine(
                $"Preparing BitKeyBridge {info.LatestVersion} for {info.Architecture}...");
            var prepared = await updater.PrepareAsync(info);
            Console.WriteLine(
                $"Verified {info.AssetName}; launching elevated update helper.");
            updater.LaunchApplyHelper(prepared, restartGui: false);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int ShowVaultStatus(AppConfig config)
    {
        try
        {
            var vault = new CredentialVaultService();
            var user = vault.GetUserMetadata(config.AdCredentialTarget);
            CredentialVaultMetadata? machine = null;
            string machineError = string.Empty;

            try { machine = vault.GetMachineMetadata(); }
            catch (Exception ex) { machineError = ex.Message; }

            Console.WriteLine($"Configured storage: {config.AdCredentialStorageMode}");
            Console.WriteLine(
                $"CurrentUser: Exists={user.Exists}; User={user.Username}; Target={user.Target}; Protection={user.ProtectedBy}");
            if (machine is not null)
            {
                Console.WriteLine(
                    $"LocalMachine: Exists={machine.Exists}; User={machine.Username}; Location={machine.Location}; Protection={machine.ProtectedBy}");
            }
            else
            {
                Console.WriteLine("LocalMachine: unavailable to current identity: " + machineError);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int SaveVaultCredential(AppConfig config, string storageMode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config.AdUsername))
                throw new InvalidOperationException(
                    "Specify --ad-user <DOMAIN\\user|user@domain> before saving a credential.");

            var password = AdSessionCredentials.HasPassword
                ? AdSessionCredentials.GetPasswordCopy()
                : ReadSecretFromConsole("AD password: ");

            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("AD password is required.");

            var vault = new CredentialVaultService();
            if (storageMode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase))
            {
                vault.SaveUserCredential(
                    config.AdCredentialTarget,
                    config.AdUsername,
                    password);
            }
            else
            {
                vault.SaveMachineCredential(
                    config.AdUsername,
                    config.AdDomain,
                    password);
            }

            config.AdUseExplicitCredentials = true;
            config.AdCredentialStorageMode = storageMode;
            ConfigService.SaveAppConfig(config);
            AdSessionCredentials.Clear();

            Console.WriteLine(
                $"AD credential saved in {storageMode} vault. Plaintext password was not written to appsettings.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            AdSessionCredentials.Clear();
        }
    }

    private static int DeleteVaultCredential(AppConfig config, string storageMode)
    {
        try
        {
            var vault = new CredentialVaultService();
            if (storageMode.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase))
                vault.DeleteUserCredential(config.AdCredentialTarget);
            else
                vault.DeleteMachineCredential();

            Console.WriteLine($"{storageMode} AD credential deleted.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int ConfigureServiceIdentityCli(
        AppConfig config,
        string mode,
        string? account)
    {
        try
        {
            string? password = null;
            if (mode.Equals("DomainAccount", StringComparison.OrdinalIgnoreCase))
                password = ReadSecretFromConsole("Windows Service account password: ");

            WindowsServiceHost.ConfigureIdentity(
                mode,
                account,
                password,
                restartIfRunning: true);

            config.ServiceIdentityMode = mode;
            config.ServiceIdentityAccount = account ?? string.Empty;
            ConfigService.SaveAppConfig(config);

            var info = WindowsServiceHost.GetInfo();
            Console.WriteLine(
                $"BitKeyBridge service identity: {info.Identity}; State={info.State}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int ConfigureServiceCoverageCli(
        AppConfig config,
        string[] args)
    {
        try
        {
            var enable = args.Any(x =>
                x.Equals("--service-coverage-enable", StringComparison.OrdinalIgnoreCase));
            var disable = args.Any(x =>
                x.Equals("--service-coverage-disable", StringComparison.OrdinalIgnoreCase));

            if (enable && disable)
                throw new ArgumentException(
                    "Specify either --service-coverage-enable or --service-coverage-disable, not both.");

            if (enable)
            {
                if (!File.Exists(AppPaths.MachineCloudConfigFile))
                {
                    throw new InvalidOperationException(
                        "Machine cloud configuration is required before enabling scheduled Coverage. " +
                        "Run --cloud-machine-save first.");
                }

                config.ServiceCoverageEnabled = true;
            }
            else if (disable)
            {
                config.ServiceCoverageEnabled = false;
            }

            var intervalText = GetOptionValue(args, "--service-coverage-interval");
            if (!string.IsNullOrWhiteSpace(intervalText))
            {
                if (!int.TryParse(intervalText, out var interval) ||
                    interval is < 15 or > 10080)
                {
                    throw new ArgumentException(
                        "--service-coverage-interval must be between 15 and 10080 minutes.");
                }

                config.ServiceCoverageIntervalMinutes = interval;
            }

            if (args.Any(x =>
                    x.Equals("--service-coverage-run-on-start", StringComparison.OrdinalIgnoreCase)))
            {
                config.ServiceRunCoverageOnStart = true;
            }

            if (args.Any(x =>
                    x.Equals("--service-coverage-no-run-on-start", StringComparison.OrdinalIgnoreCase)))
            {
                config.ServiceRunCoverageOnStart = false;
            }

            ConfigService.SaveAppConfig(config);

            var service = WindowsServiceHost.GetInfo();
            if (service.Installed &&
                string.Equals(service.State, "Running", StringComparison.OrdinalIgnoreCase))
            {
                WindowsServiceHost.Stop();
                WindowsServiceHost.Start();
            }

            Console.WriteLine(
                $"Scheduled Coverage: Enabled={config.ServiceCoverageEnabled}; " +
                $"IntervalMinutes={config.ServiceCoverageIntervalMinutes}; " +
                $"RunOnStart={config.ServiceRunCoverageOnStart}");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string? GetOptionValue(string[] args, string option)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(option, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static int RunAdTest(AppConfig config)
    {
        try
        {
            var ad = new ActiveDirectoryService(config);
            var server = ad.GetPreferredWritableDc();
            var root = ad.TestConnection(server);
            Console.WriteLine("AD connection OK");
            Console.WriteLine($"Server: {server}");
            Console.WriteLine($"DNS host: {root.GetValueOrDefault("dnsHostName", server)}");
            Console.WriteLine($"Default naming context: {root.GetValueOrDefault("defaultNamingContext", string.Empty)}");
            Console.WriteLine($"RODC: {root.GetValueOrDefault("isRODC", "Unknown")}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("AD connection failed: " + ex.Message);
            return 1;
        }
        finally
        {
            AdSessionCredentials.Clear();
        }
    }

    private static void ApplySessionCommandLine(AppConfig config, string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--ad-auto", StringComparison.OrdinalIgnoreCase))
            {
                config.AdConnectionMode = "Auto";
                continue;
            }

            if (arg.Equals("--ad-integrated", StringComparison.OrdinalIgnoreCase))
            {
                config.AdUseExplicitCredentials = false;
                AdSessionCredentials.Clear();
                continue;
            }

            if (arg.Equals("--ad-ldaps", StringComparison.OrdinalIgnoreCase))
            {
                config.AdUseLdaps = true;
                if (config.AdPort == 389)
                    config.AdPort = 636;
                continue;
            }

            if (arg.Equals("--ad-ldap", StringComparison.OrdinalIgnoreCase))
            {
                config.AdUseLdaps = false;
                if (config.AdPort == 636)
                    config.AdPort = 389;
                continue;
            }

            if (arg.Equals("--ad-password-prompt", StringComparison.OrdinalIgnoreCase))
            {
                config.AdUseExplicitCredentials = true;
                AdSessionCredentials.SetPassword(ReadPasswordFromConsole());
                continue;
            }

            if (i + 1 >= args.Length) continue;
            var value = args[i + 1];

            if (arg.Equals("--ad-server", StringComparison.OrdinalIgnoreCase))
            {
                config.AdServer = value.Trim();
                config.AdConnectionMode = "Explicit";
                i++;
            }
            else if (arg.Equals("--ad-domain", StringComparison.OrdinalIgnoreCase))
            {
                config.AdDomain = value.Trim();
                i++;
            }
            else if (arg.Equals("--ad-user", StringComparison.OrdinalIgnoreCase))
            {
                config.AdUsername = value.Trim();
                config.AdUseExplicitCredentials = true;
                i++;
            }
            else if (arg.Equals("--ad-port", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
                    throw new ArgumentException("--ad-port must be between 1 and 65535.");
                config.AdPort = port;
                i++;
            }
            else if (arg.Equals("--output-root", StringComparison.OrdinalIgnoreCase))
            {
                config.OutputRoot = value.Trim();
                i++;
            }
            else if (arg.Equals("--output-subdirectory", StringComparison.OrdinalIgnoreCase))
            {
                config.OutputSubdirectory = value.Trim();
                i++;
            }
        }
    }

    private static string ReadPasswordFromConsole() =>
        ReadSecretFromConsole("AD password (session only): ");

    private static string ReadSecretFromConsole(string prompt)
    {
        Console.Write(prompt);
        var secret = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return secret.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0)
                    secret.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar))
                secret.Append(key.KeyChar);
        }
    }

    private static int RunSelfTest()
    {
        var failures = new List<string>();
        var tempDirectory = Path.Combine(Path.GetTempPath(), "BitKeyBridge-SelfTest-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var parsed = ActiveDirectoryService.GetParentComputerName(@"CN={00000000-0000-0000-0000-000000000000},CN=PC\,LAB,OU=Computers,DC=example,DC=com");
                if (!string.Equals(parsed, "PC,LAB", StringComparison.Ordinal))
                    failures.Add("LDAP DN parent parsing returned an unexpected computer name.");
            }
            catch (Exception ex) { failures.Add("LDAP DN parsing: " + ex.Message); }

            try
            {
                var namingContext = "DC=yosh,DC=ac,DC=il";

                if (!ActiveDirectoryService.IsSearchBaseWithinNamingContext(
                        "OU=Clients,DC=yosh,DC=ac,DC=il",
                        namingContext) ||
                    !ActiveDirectoryService.IsSearchBaseWithinNamingContext(
                        namingContext,
                        namingContext) ||
                    ActiveDirectoryService.IsSearchBaseWithinNamingContext(
                        "OU=Clients,DC=other,DC=example",
                        namingContext))
                {
                    failures.Add(
                        "Remembered recovery scope naming-context validation failed.");
                }
            }
            catch (Exception ex)
            {
                failures.Add(
                    "Recovery scope naming-context validation: " +
                    ex.Message);
            }

            try
            {
                var csv = Path.Combine(tempDirectory, "recovery.csv");
                var rows = new List<RecoveryRecord>
                {
                    new("PC-01", "11111111-2222-3333-4444-555555555555", "111111-222222-333333-444444-555555-666666-777777-888888", new DateTime(2026, 1, 2, 3, 4, 5)),
                    new("PC,02", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "999999-888888-777777-666666-555555-444444-333333-222222", new DateTime(2026, 6, 7, 8, 9, 10))
                };
                CsvUtility.WriteRecoveryCsvAtomic(csv, rows);
                var read = CsvUtility.ReadRecoveryCsv(csv);
                if (read.Count != 2 || read[1].ComputerName != "PC,02" || read[0].RecoveryKey != rows[0].RecoveryKey)
                    failures.Add("CSV atomic write/read round-trip failed.");
            }
            catch (Exception ex) { failures.Add("CSV round-trip: " + ex.Message); }

            try
            {
                var a = new[]
                {
                    new BitLockerScope("One", "OU=One,DC=example,DC=com"),
                    new BitLockerScope("Two", "OU=Two,DC=example,DC=com")
                };
                var b = a.Reverse();
                if (!string.Equals(ExportService.GetScopeFingerprint(a), ExportService.GetScopeFingerprint(b), StringComparison.Ordinal))
                    failures.Add("Scope fingerprint changes when scope order changes.");
            }
            catch (Exception ex) { failures.Add("Scope fingerprint: " + ex.Message); }

            try
            {
                if (!Guid.TryParse(EntraSetupService.DefaultBootstrapClientId, out var bootstrapId) ||
                    bootstrapId == Guid.Empty)
                    failures.Add("Default Microsoft bootstrap Client ID is invalid.");
                if (!string.Equals(
                        EntraSetupService.DefaultBootstrapDisplayName,
                        "Microsoft Graph Command Line Tools",
                        StringComparison.Ordinal))
                    failures.Add("Default Microsoft bootstrap display name is unexpected.");
            }
            catch (Exception ex) { failures.Add("Entra bootstrap defaults: " + ex.Message); }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var serviceInfo = WindowsServiceHost.GetInfo();
                    if (string.IsNullOrWhiteSpace(serviceInfo.State))
                        failures.Add("Windows Service SCM query returned an empty state.");
                }
            }
            catch (Exception ex) { failures.Add("Windows Service SCM query: " + ex.Message); }

            try
            {
                if (!string.Equals(
                        UpdateService.NormalizeRepository("sgennadi/BitKeyBridge"),
                        "sgennadi/BitKeyBridge",
                        StringComparison.Ordinal))
                    failures.Add("Updater repository normalization failed.");

                var expectedHash = new string('a', 64);
                var sums = expectedHash + "  BitKeyBridge-win-x64.zip" + Environment.NewLine;
                var parsedHash = UpdateService.ParseChecksum(sums, "BitKeyBridge-win-x64.zip");
                if (!string.Equals(parsedHash, expectedHash, StringComparison.Ordinal))
                    failures.Add("Updater SHA256SUMS parsing failed.");

                var parsedVersion = UpdateService.ParseVersion("v0.4.0-beta.1");
                if (parsedVersion.Major != 0 || parsedVersion.Minor != 4 || parsedVersion.Build != 0)
                    failures.Add("Updater version normalization failed.");

                var rid = UpdateService.GetRid();
                if (rid is not "win-x64" and not "win-x86" and not "win-arm64")
                    failures.Add("Updater architecture RID detection returned an unexpected value.");
            }
            catch (Exception ex) { failures.Add("Updater pure helpers: " + ex.Message); }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var plain = Encoding.UTF8.GetBytes("BitKeyBridge-DPAPI-self-test-" + Guid.NewGuid().ToString("N"));
                    byte[]? protectedBytes = null;
                    byte[]? roundTrip = null;
                    try
                    {
                        protectedBytes = CredentialVaultService.ProtectMachineData(plain);
                        roundTrip = CredentialVaultService.UnprotectMachineData(protectedBytes);
                        if (!plain.SequenceEqual(roundTrip))
                            failures.Add("Machine DPAPI protect/unprotect round-trip failed.");
                    }
                    finally
                    {
                        System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain);
                        if (protectedBytes is not null)
                            System.Security.Cryptography.CryptographicOperations.ZeroMemory(protectedBytes);
                        if (roundTrip is not null)
                            System.Security.Cryptography.CryptographicOperations.ZeroMemory(roundTrip);
                    }
                }
            }
            catch (Exception ex) { failures.Add("Machine DPAPI round-trip: " + ex.Message); }

            try
            {
                var coverageType = typeof(CoverageDeviceRow);
                if (coverageType.GetProperties().Any(p =>
                        p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                        p.Name.Equals("KeyValue", StringComparison.OrdinalIgnoreCase)))
                {
                    failures.Add("Coverage model contains a recovery-secret field.");
                }

                var coverageCsv = Path.Combine(tempDirectory, "coverage.csv");
                CoverageService.ExportCsv(
                    coverageCsv,
                    [
                        new CoverageDeviceRow
                        {
                            ComputerName = "=HYPERLINK(\"https://example.invalid\",\"PC-01\")",
                            CoverageStatus = "AD + Entra",
                            FoundInAd = true,
                            FoundInIntune = true,
                            AdRecoveryKeyCount = 1,
                            EntraRecoveryKeyCount = 1,
                            IsEncrypted = true,
                            ComplianceState = "compliant"
                        }
                    ]);

                var coverageText = File.ReadAllText(coverageCsv);
                if (coverageText.Contains("RecoveryPassword", StringComparison.OrdinalIgnoreCase) ||
                    coverageText.Contains("KeyValue", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add("Coverage CSV contains a recovery-secret column.");
                }

                if (coverageText.Contains(
                        "\"=HYPERLINK(",
                        StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add("Coverage CSV did not neutralize a formula-like value.");
                }

                if (!coverageText.Contains(
                        "\"'=HYPERLINK(",
                        StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add("Coverage CSV formula-neutralization marker is missing.");
                }
            }
            catch (Exception ex) { failures.Add("Coverage metadata-only CSV: " + ex.Message); }

            try
            {
                var rbacConfig = new AppConfig
                {
                    RbacEnabled = false
                };
                var auth = new AuthorizationService(rbacConfig);

                if (!auth.Check(BitKeyBridgePermission.RecoveryRead).Allowed ||
                    !auth.Check(BitKeyBridgePermission.Rotate).Allowed)
                {
                    failures.Add(
                        "RBAC backward-compatible disabled mode failed.");
                }

                if (!string.Equals(
                        CoveragePolicyService.NormalizeSeverity("Critical"),
                        "Error",
                        StringComparison.Ordinal))
                {
                    failures.Add(
                        "Coverage policy severity normalization failed.");
                }
            }
            catch (Exception ex)
            {
                failures.Add(
                    "RBAC/policy pure helpers: " + ex.Message);
            }

            try
            {
                var exportDefaults =
                    new AppConfig();

                if (!string.IsNullOrWhiteSpace(
                        exportDefaults.SysvolScriptsRoot) ||
                    !string.IsNullOrWhiteSpace(
                        exportDefaults.OutputSubdirectory) ||
                    !string.Equals(
                        exportDefaults.EffectiveOutputRoot,
                        AppConfig.DefaultOutputRoot,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        exportDefaults.OutputDirectory,
                        AppConfig.DefaultOutputRoot,
                        StringComparison.OrdinalIgnoreCase) ||
                    exportDefaults.EffectiveOutputRoot.Contains(
                        "SYSVOL",
                        StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(
                        "Default recovery export path unexpectedly uses SYSVOL or a forced subdirectory.");
                }
            }
            catch (Exception ex)
            {
                failures.Add(
                    "Recovery export defaults: " +
                    ex.Message);
            }

            try
            {
                var uiDefaults =
                    new AppConfig();

                if (!uiDefaults.AutoConnectOnStart ||
                    !string.Equals(
                        uiDefaults.RecoverySearchSource,
                        "LiveAD",
                        StringComparison.Ordinal) ||
                    !string.IsNullOrWhiteSpace(
                        uiDefaults.LastRecoveryScopeName) ||
                    !string.IsNullOrWhiteSpace(
                        uiDefaults.LastRecoveryScopeSearchBase) ||
                    uiDefaults.RbacAdministrators.Count != 0 ||
                    uiDefaults.SchemaVersion !=
                        ConfigSchema.CurrentVersion)
                {
                    failures.Add(
                        "Schema v4 helpdesk UI defaults are invalid.");
                }

                if (OperatingSystem.IsWindows())
                {
                    using var currentIdentity =
                        System.Security.Principal.WindowsIdentity.GetCurrent();
                    var currentPrincipal =
                        new System.Security.Principal.WindowsPrincipal(
                            currentIdentity);
                    var isLocalAdministrator =
                        currentPrincipal.IsInRole(
                            System.Security.Principal.WindowsBuiltInRole.Administrator);

                    var adminDecision =
                        new AuthorizationService(
                            uiDefaults)
                            .Check(
                                BitKeyBridgePermission.Administrator);

                    if (adminDecision.Allowed !=
                        isLocalAdministrator)
                    {
                        failures.Add(
                            "Administration UI default authorization is not restricted to local Administrators.");
                    }

                    var recoveryDecision =
                        new AuthorizationService(
                            uiDefaults)
                            .Check(
                                BitKeyBridgePermission.RecoveryRead);

                    if (!recoveryDecision.Allowed)
                    {
                        failures.Add(
                            "RBAC-disabled RecoveryRead backward compatibility was broken.");
                    }
                }

                var metadataCsv =
                    Path.Combine(
                        tempDirectory,
                        "metadata-only-recovery.csv");

                const string selfTestKey =
                    "111111-222222-333333-444444-555555-666666-777777-888888";

                CsvUtility.WriteRecoveryCsvAtomic(
                    metadataCsv,
                    [
                        new RecoveryRecord(
                            "PC-SELFTEST",
                            "11111111-2222-3333-4444-555555555555",
                            selfTestKey,
                            new DateTime(
                                2026,
                                1,
                                2,
                                3,
                                4,
                                5,
                                DateTimeKind.Local))
                    ]);

                var metadataOnly =
                    CsvUtility.ReadRecoveryMetadata(
                        metadataCsv,
                        "PC-SELFTEST",
                        10);

                if (metadataOnly.Count != 1 ||
                    metadataOnly[0].ComputerName !=
                        "PC-SELFTEST" ||
                    metadataOnly[0].RecoveryId !=
                        "11111111-2222-3333-4444-555555555555" ||
                    metadataOnly[0].Source !=
                        "Local cache")
                {
                    failures.Add(
                        "Metadata-only local recovery search failed.");
                }

                var onDemandKey =
                    CsvUtility.GetRecoveryPassword(
                        metadataCsv,
                        "PC-SELFTEST",
                        "11111111-2222-3333-4444-555555555555");

                if (!string.Equals(
                        onDemandKey,
                        selfTestKey,
                        StringComparison.Ordinal))
                {
                    failures.Add(
                        "On-demand local recovery-key read failed.");
                }
            }
            catch (Exception ex)
            {
                failures.Add(
                    "Schema v4 recovery UI/cache helpers: " +
                    ex.Message);
            }

            try
            {
                var privilegedDefaults =
                    new AppConfig();

                if (privilegedDefaults.JitRecoveryEnabled ||
                    privilegedDefaults.TwoPersonApprovalEnabled ||
                    privilegedDefaults.SiemEnabled ||
                    privilegedDefaults.SiemFailClosed)
                {
                    failures.Add(
                        "Privileged-access features are not opt-in by default.");
                }

                var disabledDecision =
                    new PrivilegedAccessPolicyService(
                        privilegedDefaults,
                        auditPath:
                            Path.Combine(
                                tempDirectory,
                                "privileged-disabled-audit.jsonl"))
                        .EvaluateRecoveryAccess(
                            new RecoveryAccessContext
                            {
                                SessionId =
                                    "1234567890abcdef1234567890abcdef"
                            },
                            "PC-SELFTEST",
                            "{SELFTEST}",
                            "AD");

                if (!disabledDecision.Allowed ||
                    disabledDecision.Status !=
                        "Allowed" ||
                    disabledDecision.Jit.Status !=
                        "Disabled" ||
                    disabledDecision.Approval.Status !=
                        "Disabled" ||
                    disabledDecision.SiemStatus !=
                        "Disabled")
                {
                    failures.Add(
                        "Disabled privileged-access policy changed backward-compatible recovery behavior.");
                }

                if (SiemForwardingService.NormalizeMode(
                        "jsonl") !=
                        "FileJsonl" ||
                    SiemForwardingService.NormalizeMode(
                        "https") !=
                        "Webhook")
                {
                    failures.Add(
                        "SIEM mode normalization failed.");
                }
            }
            catch (Exception ex)
            {
                failures.Add(
                    "Privileged-access opt-in defaults: " +
                    ex.Message);
            }

            try
            {
                var normalizedSystem =
                    CertificatePrivateKeyAccessService.NormalizeServiceIdentity(
                        "LocalSystem");
                if (!string.Equals(
                        normalizedSystem,
                        @"NT AUTHORITY\SYSTEM",
                        StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(
                        "Certificate ACL LocalSystem normalization failed.");
                }

                if (!CertificatePrivateKeyAccessService.IsLocalSystem(
                        @"NT AUTHORITY\SYSTEM"))
                {
                    failures.Add(
                        "Certificate ACL LocalSystem detection failed.");
                }
            }
            catch (Exception ex)
            {
                failures.Add(
                    "Certificate ACL pure helpers: " + ex.Message);
            }

            try
            {
                var policyConfig = new AppConfig
                {
                    CoveragePolicyEnabled = true,
                    CoveragePolicyMaxNoRecoveryKey = 0,
                    CoveragePolicyNoRecoveryKeySeverity = "Error"
                };
                var policy = new CoveragePolicyService(policyConfig)
                    .Evaluate(new CoverageSummary
                    {
                        NoRecoveryKey = 1
                    });

                if (policy.Compliant ||
                    policy.ErrorCount != 1 ||
                    policy.Violations.Count != 1 ||
                    policy.Violations[0].Code != "NO_RECOVERY_KEY")
                {
                    failures.Add("Coverage policy evaluation failed.");
                }

                if (CoveragePolicyService.NormalizeSeverity("critical") !=
                    "Error")
                {
                    failures.Add("Coverage policy severity normalization failed.");
                }
            }
            catch (Exception ex)
            {
                failures.Add("Coverage policy pure helpers: " + ex.Message);
            }

            try
            {
                var summary = new CoverageSummary
                {
                    NoRecoveryKey = 1,
                    IntuneNotEncrypted = 1,
                    IntuneStale = 1
                };

                if (CoverageCliService.EvaluateExitCode(summary, ["--coverage-fail-no-key"]) !=
                    CoverageCliService.ExitNoRecoveryKey)
                    failures.Add("Coverage CLI no-key exit code failed.");

                if (CoverageCliService.EvaluateExitCode(summary, ["--coverage-fail-unencrypted"]) !=
                    CoverageCliService.ExitIntuneNotEncrypted)
                    failures.Add("Coverage CLI unencrypted exit code failed.");

                if (CoverageCliService.EvaluateExitCode(summary, ["--coverage-fail-stale"]) !=
                    CoverageCliService.ExitIntuneStale)
                    failures.Add("Coverage CLI stale exit code failed.");

                if (CoverageCliService.EvaluateExitCode(new CoverageSummary(), []) != 0)
                    failures.Add("Coverage CLI healthy exit code failed.");
            }
            catch (Exception ex) { failures.Add("Coverage CLI exit codes: " + ex.Message); }

            try
            {
                var metricSecret =
                    "123456-123456-123456-123456-123456-123456-123456-123456";

                var metrics =
                    MetricsService.BuildPrometheus(
                        new HealthSnapshot
                        {
                            OverallStatus = "OK",
                            MachineName = metricSecret,
                            OutputDirectory = metricSecret,
                            LastRunDc = metricSecret,
                            AuditIntegrityError = metricSecret,
                            ServiceState = "Running",
                            ServiceInstalled = true,
                            CoveragePolicyCompliant = true
                        });

                if (!metrics.Contains(
                        "bitkeybridge_up 1",
                        StringComparison.Ordinal) ||
                    !metrics.Contains(
                        "bitkeybridge_ready 1",
                        StringComparison.Ordinal) ||
                    metrics.Contains(
                        metricSecret,
                        StringComparison.Ordinal))
                {
                    failures.Add(
                        "Secret-free Prometheus metrics formatting failed.");
                }
            }
            catch (Exception ex)
            {
                failures.Add(
                    "Prometheus metrics helper: " +
                    ex.Message);
            }

            try
            {
                var legacyConfigPath =
                    Path.Combine(
                        tempDirectory,
                        "legacy-appsettings.json");
                var legacyStatusPath =
                    Path.Combine(
                        tempDirectory,
                        "legacy-migration-status.json");
                var legacyBackups =
                    Path.Combine(
                        tempDirectory,
                        "legacy-backups");

                File.WriteAllText(
                    legacyConfigPath,
                    "{\"HealthEndpointPort\":8750,\"RemoteApiPort\":8751,\"ServiceIntervalMinutes\":60,\"ServiceCoverageIntervalMinutes\":1440,\"AdPort\":389}");

                var migrator =
                    new ConfigMigrationService(
                        legacyConfigPath,
                        legacyStatusPath,
                        legacyBackups);

                var migrated =
                    migrator.LoadAndMigrate();
                var migrationStatus =
                    migrator.ReadStatus();

                if (migrated.SchemaVersion !=
                        ConfigSchema.CurrentVersion ||
                    ConfigMigrationService
                        .DetectStoredSchemaVersion(
                            legacyConfigPath) !=
                        ConfigSchema.CurrentVersion ||
                    migrationStatus is null ||
                    !migrationStatus.Migrated ||
                    string.IsNullOrWhiteSpace(
                        migrationStatus.BackupPath) ||
                    !File.Exists(
                        migrationStatus.BackupPath))
                {
                    failures.Add(
                        "Legacy configuration schema migration failed.");
                }

                var futureConfigPath =
                    Path.Combine(
                        tempDirectory,
                        "future-appsettings.json");
                const string futureText =
                    "{\"SchemaVersion\":999,\"HealthEndpointPort\":8750}";

                File.WriteAllText(
                    futureConfigPath,
                    futureText);

                var futureRejected = false;
                try
                {
                    _ = new ConfigMigrationService(
                            futureConfigPath,
                            Path.Combine(
                                tempDirectory,
                                "future-status.json"),
                            Path.Combine(
                                tempDirectory,
                                "future-backups"))
                        .LoadAndMigrate();
                }
                catch (FutureConfigurationSchemaException)
                {
                    futureRejected = true;
                }

                if (!futureRejected ||
                    File.ReadAllText(
                        futureConfigPath) !=
                    futureText)
                {
                    failures.Add(
                        "Future configuration schema refusal failed.");
                }

                var missingConfigPath =
                    Path.Combine(
                        tempDirectory,
                        "missing-appsettings.json");
                var missingDefaults =
                    ConfigService.LoadAppConfig(
                        missingConfigPath,
                        Path.Combine(
                            tempDirectory,
                            "missing-status.json"),
                        Path.Combine(
                            tempDirectory,
                            "missing-backups"));

                if (missingDefaults.SchemaVersion !=
                    ConfigSchema.CurrentVersion)
                {
                    failures.Add(
                        "Missing configuration did not load safe defaults.");
                }

                var corruptConfigPath =
                    Path.Combine(
                        tempDirectory,
                        "corrupt-appsettings.json");
                File.WriteAllText(
                    corruptConfigPath,
                    "{ not valid json");

                var corruptRejected = false;
                try
                {
                    _ = ConfigService.LoadAppConfig(
                        corruptConfigPath,
                        Path.Combine(
                            tempDirectory,
                            "corrupt-status.json"),
                        Path.Combine(
                            tempDirectory,
                            "corrupt-backups"));
                }
                catch (ConfigurationLoadException)
                {
                    corruptRejected = true;
                }

                if (!corruptRejected)
                {
                    failures.Add(
                        "Corrupt application configuration did not fail closed.");
                }

                var invalidConfigPath =
                    Path.Combine(
                        tempDirectory,
                        "invalid-appsettings.json");
                File.WriteAllText(
                    invalidConfigPath,
                    JsonSerializer.Serialize(
                        new AppConfig
                        {
                            SchemaVersion =
                                ConfigSchema.CurrentVersion,
                            HealthEndpointPort = 80
                        }));

                var invalidRejected = false;
                try
                {
                    _ = ConfigService.LoadAppConfig(
                        invalidConfigPath,
                        Path.Combine(
                            tempDirectory,
                            "invalid-status.json"),
                        Path.Combine(
                            tempDirectory,
                            "invalid-backups"));
                }
                catch (ConfigurationLoadException)
                {
                    invalidRejected = true;
                }

                if (!invalidRejected)
                {
                    failures.Add(
                        "Invalid application configuration did not fail closed.");
                }
            }
            catch (Exception ex)
            {
                failures.Add(
                    "Configuration schema migration: " +
                    ex.Message);
            }

            try
            {
                var auditPath = Path.Combine(tempDirectory, "audit.jsonl");
                var audit = new AuditService(auditPath, 1);
                var fakeKey = "111111-222222-333333-444444-555555-666666-777777-888888";
                audit.Write(
                    "SelfTest",
                    details: "Sensitive=" + fakeKey,
                    reference: "INC-12345",
                    reason: "Recovery validation");
                audit.Write(
                    "SelfTest2",
                    details: "Second chained audit record");

                var entries = audit.ReadRecent(10);
                if (entries.Count != 2 ||
                    entries.Any(x =>
                        x.Details.Contains(
                            fakeKey,
                            StringComparison.Ordinal)) ||
                    !entries.Any(x =>
                        x.Details.Contains(
                            "[REDACTED-BITLOCKER-KEY]",
                            StringComparison.Ordinal)) ||
                    entries.All(x => x.Reference != "INC-12345") ||
                    entries.All(x => x.Reason != "Recovery validation"))
                {
                    failures.Add(
                        "Audit redaction/reference/reason round-trip failed.");
                }

                var integrity =
                    AuditIntegrityService.Verify(auditPath);
                if (!integrity.Valid ||
                    integrity.ChainedEntries != 2)
                {
                    failures.Add(
                        "Audit hash-chain verification failed.");
                }

                var mixedPath =
                    Path.Combine(
                        tempDirectory,
                        "audit-v1-v2.jsonl");

                var legacyEntry = new AuditEntry
                {
                    TimestampUtc = DateTime.UtcNow.AddSeconds(-1),
                    User = "SELFTEST\\Legacy",
                    Host = "SELFTEST",
                    Action = "LegacyV1",
                    Result = "Success",
                    ChainVersion = 1,
                    PreviousHash = string.Empty
                };
                legacyEntry.EntryHash =
                    AuditIntegrityService.ComputeHash(
                        legacyEntry);

                File.WriteAllText(
                    mixedPath,
                    JsonSerializer.Serialize(legacyEntry) +
                    Environment.NewLine);

                var mixedAudit =
                    new AuditService(mixedPath, 1);
                var sessionId =
                    "00112233445566778899aabbccddeeff";
                var context =
                    new RecoveryAccessContext
                    {
                        SessionId = sessionId,
                        Reference = "INC-54321",
                        Reason = "Mixed-chain test"
                    };

                var v2Entry = mixedAudit.Write(
                    "RecoveryReadV2",
                    computerName: "PC-SELFTEST",
                    recoveryId: "{SELFTEST}",
                    source: "AD",
                    details:
                        "Sensitive=" + fakeKey,
                    reference:
                        context.Reference,
                    reason:
                        context.Reason,
                    correlationId:
                        context.SessionId);

                var mixedIntegrity =
                    AuditIntegrityService.Verify(
                        mixedPath);

                if (!mixedIntegrity.Valid ||
                    mixedIntegrity.ChainedEntries != 2 ||
                    mixedIntegrity.LastChainVersion !=
                    AuditIntegrityService.CurrentChainVersion ||
                    v2Entry is null ||
                    v2Entry.ChainVersion != 2 ||
                    v2Entry.CorrelationId != sessionId)
                {
                    failures.Add(
                        "Audit v1-to-v2 compatibility/correlation failed.");
                }

                var incidentDir =
                    Path.Combine(
                        tempDirectory,
                        "Incidents");
                var incident =
                    new RecoveryIncidentService(
                        incidentDir)
                    .Append(
                        context,
                        v2Entry);

                var incidentPath =
                    Path.Combine(
                        incidentDir,
                        sessionId + ".json");
                var incidentText =
                    File.Exists(incidentPath)
                        ? File.ReadAllText(incidentPath)
                        : string.Empty;

                if (incident is null ||
                    !File.Exists(incidentPath) ||
                    !incidentText.Contains(
                        sessionId,
                        StringComparison.OrdinalIgnoreCase) ||
                    incidentText.Contains(
                        fakeKey,
                        StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(
                        incident.Actions
                            .FirstOrDefault()?
                            .AuditEntryHash))
                {
                    failures.Add(
                        "Recovery incident metadata-only bundle failed.");
                }

                if (incident is not null)
                {
                    var incidentVerifier =
                        new RecoveryIncidentVerificationService(
                            incidentDir,
                            mixedPath);

                    var verifiedIncident =
                        incidentVerifier.Verify(
                            sessionId);

                    if (!verifiedIncident.Valid ||
                        verifiedIncident.Status != "Valid" ||
                        verifiedIncident.ActionsAnchored != 1)
                    {
                        failures.Add(
                            "Recovery incident verification failed.");
                    }

                    incident.Actions[0].Source =
                        "TamperedSource";
                    JsonStore.WriteAtomic(
                        incidentPath,
                        incident);

                    var tamperedIncident =
                        incidentVerifier.Verify(
                            sessionId);

                    if (tamperedIncident.MetadataMismatches == 0 ||
                        tamperedIncident.Status != "MetadataMismatch")
                    {
                        failures.Add(
                            "Recovery incident metadata tamper detection failed.");
                    }

                    incident.Actions[0].Source =
                        v2Entry?.Source ?? "AD";
                    incident.Reason =
                        fakeKey;
                    JsonStore.WriteAtomic(
                        incidentPath,
                        incident);

                    var sensitiveIncident =
                        incidentVerifier.Verify(
                            sessionId);

                    if (!sensitiveIncident.SensitiveDataDetected ||
                        sensitiveIncident.Status != "SensitiveDataDetected")
                    {
                        failures.Add(
                            "Recovery incident sensitive-data detection failed.");
                    }
                }

                var housekeepingRoot =
                    Path.Combine(
                        tempDirectory,
                        "Housekeeping");
                var housekeepingIncidents =
                    Path.Combine(
                        housekeepingRoot,
                        "Incidents");
                var housekeepingBackups =
                    Path.Combine(
                        housekeepingRoot,
                        "Backups");
                var housekeepingTransitions =
                    Path.Combine(
                        housekeepingRoot,
                        "Transitions");
                var housekeepingAudit =
                    Path.Combine(
                        housekeepingRoot,
                        "audit.jsonl");
                var housekeepingStatus =
                    Path.Combine(
                        housekeepingRoot,
                        "housekeeping-status.json");

                Directory.CreateDirectory(
                    housekeepingIncidents);
                Directory.CreateDirectory(
                    housekeepingBackups);
                Directory.CreateDirectory(
                    housekeepingTransitions);

                var housekeepingAuditService =
                    new AuditService(
                        housekeepingAudit,
                        1);

                var housekeepingSession =
                    "11223344556677889900aabbccddeeff";
                var housekeepingContext =
                    new RecoveryAccessContext
                    {
                        SessionId =
                            housekeepingSession,
                        Reference =
                            "INC-HOUSEKEEPING",
                        Reason =
                            "Retention self-test"
                    };

                var housekeepingEntry =
                    housekeepingAuditService.Write(
                        "GetCloudRecoveryKey",
                        computerName:
                            "PC-HOUSEKEEPING",
                        recoveryId:
                            "{HK-SELFTEST}",
                        source:
                            "Entra",
                        authMode:
                            "Certificate",
                        reference:
                            housekeepingContext.Reference,
                        reason:
                            housekeepingContext.Reason,
                        correlationId:
                            housekeepingContext.SessionId);

                var housekeepingBundle =
                    new RecoveryIncidentService(
                        housekeepingIncidents)
                        .Append(
                            housekeepingContext,
                            housekeepingEntry);

                if (housekeepingBundle is null)
                {
                    failures.Add(
                        "Housekeeping incident setup failed.");
                }
                else
                {
                    housekeepingBundle.UpdatedAtUtc =
                        DateTime.UtcNow.AddDays(-10);

                    JsonStore.WriteAtomic(
                        Path.Combine(
                            housekeepingIncidents,
                            housekeepingSession +
                            ".json"),
                        housekeepingBundle);
                }

                var preservedSession =
                    "ffeeddccbbaa00998877665544332211";
                var preservedBundle =
                    new RecoveryIncidentBundle
                    {
                        SessionId =
                            preservedSession,
                        CreatedAtUtc =
                            DateTime.UtcNow.AddDays(-20),
                        UpdatedAtUtc =
                            DateTime.UtcNow.AddDays(-10),
                        Operator =
                            "SELFTEST\\Operator",
                        Host =
                            "SELFTEST",
                        ComputerName =
                            "PC-OLD",
                        RecoveryId =
                            "{OLD-SELFTEST}",
                        Reference =
                            "INC-OLD",
                        Reason =
                            "Retention evidence",
                        Actions =
                        [
                            new RecoveryIncidentAction
                            {
                                TimestampUtc =
                                    DateTime.UtcNow.AddDays(-20),
                                Action =
                                    "GetCloudRecoveryKey",
                                Result =
                                    "Success",
                                Source =
                                    "Entra",
                                AuthMode =
                                    "Certificate",
                                AuditEntryHash =
                                    new string('A', 64)
                            }
                        ]
                    };

                var preservedPath =
                    Path.Combine(
                        housekeepingIncidents,
                        preservedSession +
                        ".json");

                JsonStore.WriteAtomic(
                    preservedPath,
                    preservedBundle);

                for (var backupIndex = 0;
                     backupIndex < 4;
                     backupIndex++)
                {
                    var backupPath =
                        Path.Combine(
                            housekeepingBackups,
                            $"backup-{backupIndex}.json");

                    File.WriteAllText(
                        backupPath,
                        "{}");

                    File.SetLastWriteTimeUtc(
                        backupPath,
                        DateTime.UtcNow.AddDays(
                            -10 + backupIndex));
                }

                var oldTemp =
                    Path.Combine(
                        housekeepingRoot,
                        "orphan.tmp");
                File.WriteAllText(
                    oldTemp,
                    "temporary");
                File.SetLastWriteTimeUtc(
                    oldTemp,
                    DateTime.UtcNow.AddDays(-10));

                var housekeepingConfig =
                    new AppConfig
                    {
                        IncidentRetentionDays = 1,
                        BackupRetentionDays = 1,
                        BackupMinimumFiles = 2,
                        TemporaryFileRetentionDays = 1
                    };

                var housekeepingResult =
                    new HousekeepingService(
                        housekeepingIncidents,
                        housekeepingBackups,
                        housekeepingRoot,
                        housekeepingAudit,
                        housekeepingStatus)
                        .Run(
                            housekeepingConfig);

                var housekeepingAuditEntries =
                    housekeepingAuditService
                        .ReadRecent(20);

                if (!housekeepingResult.Success ||
                    housekeepingResult.IncidentDeleted != 1 ||
                    housekeepingResult.IncidentPreservedNotFullyRetained != 1 ||
                    housekeepingResult.BackupDeleted != 2 ||
                    housekeepingResult.BackupPreservedMinimum != 2 ||
                    housekeepingResult.TemporaryFilesDeleted < 1 ||
                    File.Exists(
                        Path.Combine(
                            housekeepingIncidents,
                            housekeepingSession +
                            ".json")) ||
                    !File.Exists(
                        preservedPath) ||
                    housekeepingAuditEntries.All(x =>
                        x.Action !=
                        "HousekeepingDeleteIncidentPlan") ||
                    housekeepingAuditEntries.All(x =>
                        x.Action !=
                        "HousekeepingDeleteIncident" ||
                        !string.Equals(
                            x.Result,
                            "Success",
                            StringComparison.OrdinalIgnoreCase)))
                {
                    failures.Add(
                        "Evidence-aware housekeeping retention failed.");
                }

                if (OperatingSystem.IsWindows() &&
                    SecurityContext.IsAdministrator())
                {
                    var aclRoot =
                        Path.Combine(
                            tempDirectory,
                            "StorageAcl");
                    var aclIncidents =
                        Path.Combine(
                            aclRoot,
                            "Incidents");
                    var aclBackups =
                        Path.Combine(
                            aclRoot,
                            "Backups");
                    var aclTransitions =
                        Path.Combine(
                            aclRoot,
                            "Transitions");
                    var aclStatus =
                        Path.Combine(
                            aclRoot,
                            "status.json");

                    var storageService =
                        new StorageSecurityService(
                            aclIncidents,
                            aclBackups,
                            aclTransitions,
                            serviceIdentityOverride:
                                string.Empty,
                            statusPath:
                                aclStatus);

                    var repaired =
                        storageService.Repair();
                    var checkedStorage =
                        storageService.Check();

                    if (!repaired.RepairSucceeded ||
                        !checkedStorage.Valid ||
                        checkedStorage.Directories.Count != 3 ||
                        checkedStorage.Directories.Any(x =>
                            !x.InheritanceProtected ||
                            !x.SystemFullControl ||
                            !x.AdministratorsFullControl))
                    {
                        failures.Add(
                            "Protected storage ACL repair/validation failed.");
                    }
                }

                using (var signingRsa = RSA.Create(2048))
                {
                    var checkpoint = new AuditSigningCheckpoint
                    {
                        Version = 1,
                        CreatedAtUtc = DateTime.UtcNow,
                        MachineName = "SELFTEST",
                        ChainVersion =
                            AuditIntegrityService.CurrentChainVersion,
                        FilesChecked = integrity.FilesChecked,
                        TotalEntries = integrity.TotalEntries,
                        LegacyEntries = integrity.LegacyEntries,
                        ChainedEntries = integrity.ChainedEntries,
                        LastHash = integrity.LastHash,
                        CertificateThumbprint = "00112233445566778899AABBCCDDEEFF00112233",
                        SignatureAlgorithm = "RSA-SHA256-PKCS1"
                    };

                    var payload =
                        AuditSigningService.BuildPayload(
                            checkpoint);
                    var signature =
                        signingRsa.SignData(
                            payload,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1);

                    if (!signingRsa.VerifyData(
                            payload,
                            signature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1))
                    {
                        failures.Add(
                            "Audit signing checkpoint signature round-trip failed.");
                    }

                    checkpoint.LastHash += "00";
                    if (signingRsa.VerifyData(
                            AuditSigningService.BuildPayload(
                                checkpoint),
                            signature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1))
                    {
                        failures.Add(
                            "Audit signing payload tamper detection failed.");
                    }
                }

                using (var oldSigner = RSA.Create(2048))
                using (var newSigner = RSA.Create(2048))
                {
                    var transition =
                        new AuditSigningTransition
                        {
                            CreatedAtUtc =
                                DateTime.UtcNow,
                            MachineName =
                                "SELFTEST",
                            ChainVersion =
                                AuditIntegrityService.CurrentChainVersion,
                            AuditHeadHash =
                                integrity.LastHash,
                            PreviousCertificateThumbprint =
                                "111122223333444455556666777788889999AAAA",
                            NewCertificateThumbprint =
                                "AAAABBBBCCCCDDDDEEEEFFFF0000111122223333",
                            PreviousCertificateNotAfterUtc =
                                DateTime.UtcNow.AddYears(1),
                            NewCertificateNotAfterUtc =
                                DateTime.UtcNow.AddYears(5)
                        };

                    var transitionPayload =
                        AuditSigningService.BuildTransitionPayload(
                            transition);

                    var oldSignature =
                        oldSigner.SignData(
                            transitionPayload,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1);
                    var newSignature =
                        newSigner.SignData(
                            transitionPayload,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1);

                    if (!oldSigner.VerifyData(
                            transitionPayload,
                            oldSignature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1) ||
                        !newSigner.VerifyData(
                            transitionPayload,
                            newSignature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1))
                    {
                        failures.Add(
                            "Audit signing rollover dual-signature round-trip failed.");
                    }

                    transition.AuditHeadHash += "00";
                    var tamperedPayload =
                        AuditSigningService.BuildTransitionPayload(
                            transition);

                    if (oldSigner.VerifyData(
                            tamperedPayload,
                            oldSignature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1) ||
                        newSigner.VerifyData(
                            tamperedPayload,
                            newSignature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1))
                    {
                        failures.Add(
                            "Audit signing rollover transition tamper detection failed.");
                    }
                }

                if (RemoteApiSetupService.NormalizeScope(
                        "read-only") != "read" ||
                    RemoteApiSetupService.NormalizeScope(
                        "coverage") != "coverage-run" ||
                    RemoteApiSetupService.NormalizeScope(
                        "export") != "export")
                {
                    failures.Add(
                        "Remote API scope normalization failed.");
                }

                using (var requestLineStream =
                       new MemoryStream(
                           Encoding.ASCII.GetBytes(
                               "GET /api/v1/health HTTP/1.1\r\n")))
                {
                    var parsedRequestLine =
                        RemoteApiServer.ReadAsciiLineAsync(
                                requestLineStream,
                                RemoteApiServer.MaximumRequestLineBytes,
                                CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();

                    if (!string.Equals(
                            parsedRequestLine,
                            "GET /api/v1/health HTTP/1.1",
                            StringComparison.Ordinal))
                    {
                        failures.Add(
                            "Remote API bounded HTTP line reader returned unexpected content.");
                    }
                }

                var oversizedRejected = false;
                try
                {
                    using var oversizedStream =
                        new MemoryStream(
                            Encoding.ASCII.GetBytes(
                                new string(
                                    'A',
                                    RemoteApiServer.MaximumRequestLineBytes +
                                    1) +
                                "\r\n"));

                    _ = RemoteApiServer.ReadAsciiLineAsync(
                            oversizedStream,
                            RemoteApiServer.MaximumRequestLineBytes,
                            CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (InvalidDataException)
                {
                    oversizedRejected = true;
                }

                if (!oversizedRejected)
                {
                    failures.Add(
                        "Remote API bounded HTTP line reader accepted an oversized line.");
                }

                var updatePlanPath =
                    Path.Combine(
                        tempDirectory,
                        "update-plan-selftest.json");

                JsonStore.WriteAtomic(
                    updatePlanPath,
                    new UpdateApplyPlan
                    {
                        StagedExecutable =
                            "C:\\selftest\\BitKeyBridge.exe",
                        StagedExecutableSha256 =
                            new string(
                                'a',
                                64),
                        ExpectedVersion =
                            "0.18.2",
                        TargetExecutables =
                        [
                            "C:\\selftest\\target.exe"
                        ]
                    });

                var updatePlanHash =
                    UpdateService.ComputeSha256File(
                        updatePlanPath);
                var verifiedPlan =
                    UpdateService.ReadVerifiedApplyPlan(
                        updatePlanPath,
                        updatePlanHash);

                if (verifiedPlan.ExpectedVersion !=
                    "0.18.2")
                {
                    failures.Add(
                        "Verified update-plan round-trip failed.");
                }

                File.AppendAllText(
                    updatePlanPath,
                    " ");

                var tamperedPlanRejected = false;
                try
                {
                    _ = UpdateService.ReadVerifiedApplyPlan(
                        updatePlanPath,
                        updatePlanHash);
                }
                catch (InvalidDataException)
                {
                    tamperedPlanRejected = true;
                }

                if (!tamperedPlanRejected)
                {
                    failures.Add(
                        "Tampered elevated update plan was not rejected.");
                }

                try
                {
                    ConfigurationMaintenanceService.ValidateAppConfig(
                        new AppConfig
                        {
                            HealthEndpointPort = 80
                        });
                    failures.Add(
                        "Configuration validation accepted an invalid health port.");
                }
                catch (InvalidOperationException)
                {
                }

                var auditText = File.ReadAllText(auditPath);
                auditText = auditText.Replace(
                    "\"SelfTest2\"",
                    "\"SelfTest2Tampered\"",
                    StringComparison.Ordinal);
                File.WriteAllText(auditPath, auditText);

                var tampered =
                    AuditIntegrityService.Verify(auditPath);
                if (tampered.Valid)
                {
                    failures.Add(
                        "Audit tamper detection failed.");
                }
            }
            catch (Exception ex) { failures.Add("Audit redaction: " + ex.Message); }
        }
        finally
        {
            try { if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true); } catch { }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("SELF-TEST OK");
            return 0;
        }

        foreach (var failure in failures) Console.Error.WriteLine("SELF-TEST FAILED: " + failure);
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("BitKeyBridge");
        Console.WriteLine();
        Console.WriteLine("GUI:");
        Console.WriteLine("  BitKeyBridge.exe");
        Console.WriteLine();
        Console.WriteLine("CLI:");
        Console.WriteLine("  --cli                 Export using saved scopes");
        Console.WriteLine("  --dry-run             Read and validate without publishing CSV");
        Console.WriteLine("  --force-publish       Override row-drop/scope-change publish guards");
        Console.WriteLine("  --dc-test             Discover and compare domain controllers");
        Console.WriteLine("  --ad-test             Test the effective Active Directory connection");
        Console.WriteLine("  --coverage            Generate AD + Entra + Intune metadata coverage CSV/JSON");
        Console.WriteLine("  --coverage-output <dir>  Default directory for coverage files");
        Console.WriteLine("  --coverage-csv <path>   Coverage CSV path (metadata only)");
        Console.WriteLine("  --coverage-json <path>  Coverage JSON path (metadata only)");
        Console.WriteLine("  --coverage-json-stdout  Also print full coverage JSON to stdout");
        Console.WriteLine("  --coverage-machine-config  Use ProgramData machine cloud certificate config");
        Console.WriteLine("  --coverage-fail-no-key  Exit 20 when any device has no recovery metadata");
        Console.WriteLine("  --coverage-fail-unencrypted  Exit 21 when Intune reports unencrypted devices");
        Console.WriteLine("  --coverage-fail-stale   Exit 22 when stale Intune devices are found");
        Console.WriteLine("  --coverage-fail-policy  Exit 23 when configured Coverage policy is violated");
        Console.WriteLine("  --coverage-policy-status  Show configured policy and last evaluated result");
        Console.WriteLine("  --coverage-policy-enable|--coverage-policy-disable");
        Console.WriteLine("  --coverage-policy-max-no-key <n>");
        Console.WriteLine("  --coverage-policy-max-unencrypted <n>");
        Console.WriteLine("  --coverage-policy-max-stale <n>");
        Console.WriteLine("  --coverage-policy-max-old-key <n>");
        Console.WriteLine("  --coverage-policy-severity-no-key <Error|Warning|Info>");
        Console.WriteLine("  --coverage-policy-severity-unencrypted <Error|Warning|Info>");
        Console.WriteLine("  --coverage-policy-severity-stale <Error|Warning|Info>");
        Console.WriteLine("  --coverage-policy-severity-old-key <Error|Warning|Info>");
        Console.WriteLine("  --cloud-auth <mode>   DeviceCode, Certificate, or Password for --coverage");
        Console.WriteLine("  --tenant-id <id>      Override saved Entra tenant ID for this run");
        Console.WriteLine("  --client-id <id>      Override saved BitKeyBridge client ID for this run");
        Console.WriteLine("  --cert-thumbprint <t> Override saved certificate thumbprint for this run");
        Console.WriteLine("  --cloud-user <upn>    Override saved cloud username for Password mode");
        Console.WriteLine("  --cloud-machine-status Show ProgramData cloud certificate config metadata");
        Console.WriteLine("  --cloud-machine-save   Save certificate cloud config for LocalSystem/gMSA");
        Console.WriteLine("  --cloud-machine-delete Delete ProgramData machine cloud config");
        Console.WriteLine("  --cert-key-status     Check service/account access to the machine certificate private key");
        Console.WriteLine("  --cert-key-grant      Grant private-key Read to service/account");
        Console.WriteLine("  --cert-key-revoke     Remove BitKeyBridge-style private-key Read ACE");
        Console.WriteLine("  --cert-account <acct> Override service identity for certificate ACL commands");
        Console.WriteLine("  --rbac-status         Show current RBAC config and effective permissions");
        Console.WriteLine("  --rbac-enable         Enable Windows user/group authorization");
        Console.WriteLine("  --rbac-disable        Disable RBAC (backward-compatible mode)");
        Console.WriteLine("  --rbac-admin-bypass <on|off>  Allow local Administrators privileged actions");
        Console.WriteLine("  --rbac-reader-add <principal>  Add DOMAIN\\group/user or SID to RecoveryRead");
        Console.WriteLine("  --rbac-reader-remove <principal>  Remove RecoveryRead principal");
        Console.WriteLine("  --rbac-rotator-add <principal> Add DOMAIN\\group/user or SID to Rotate");
        Console.WriteLine("  --rbac-rotator-remove <principal> Remove Rotate principal");
        Console.WriteLine("  --rbac-ui-admin-add <principal> Add DOMAIN\\group/user or SID to Administration UI");
        Console.WriteLine("  --rbac-ui-admin-remove <principal> Remove Administration UI principal");
        Console.WriteLine("  --audit-verify        Verify tamper-evident audit hash chain");
        Console.WriteLine("  --audit-signing-status  Show signed-checkpoint and certificate state");
        Console.WriteLine("  --audit-signing-setup   Create/reuse non-exportable machine signing certificate");
        Console.WriteLine("  --audit-signing-years <1-10>  Certificate lifetime for setup");
        Console.WriteLine("  --audit-signing-sign    Sign the current audit-chain checkpoint");
        Console.WriteLine("  --audit-signing-verify  Verify checkpoint signature + audit chain");
        Console.WriteLine("  --audit-signing-disable Disable new checkpoints; retain cert/checkpoint");
        Console.WriteLine("  --audit-signing-rollover  Dual-sign transition to a new audit-signing certificate");
        Console.WriteLine("  --audit-signing-rollover-years <1-10>  New signing certificate lifetime");
        Console.WriteLine("  --remote-token-status  Show configured Remote API token scopes");
        Console.WriteLine("  --remote-token-generate <scope>  Create read, coverage-run, or export token");
        Console.WriteLine("  --remote-token-revoke <scope>    Revoke read, coverage-run, or export token");
        Console.WriteLine("  --config-backup <path>     Create JSON config backup without credential/private-key material");
        Console.WriteLine("  --config-restore <path>    Validate and restore config; creates rollback backup");
        Console.WriteLine("  --diagnostics-bundle <zip> Create sanitized troubleshooting ZIP");
        Console.WriteLine("  --incident-verify <session-id> Verify incident bundle against retained audit hashes");
        Console.WriteLine("  --housekeeping-status    Show housekeeping policy and last result");
        Console.WriteLine("  --housekeeping-run       Run retention cleanup now");
        Console.WriteLine("  --housekeeping-dry-run   Preview retention cleanup without deleting");
        Console.WriteLine("  --housekeeping-enable|--housekeeping-disable");
        Console.WriteLine("  --housekeeping-run-on-start|--housekeeping-no-run-on-start");
        Console.WriteLine("  --housekeeping-interval-hours <1-168>");
        Console.WriteLine("  --incident-retention-days <0-36500>  0 keeps incidents forever");
        Console.WriteLine("  --backup-retention-days <0-36500>    0 keeps backups forever");
        Console.WriteLine("  --backup-minimum-files <0-1000>");
        Console.WriteLine("  --temp-retention-days <0-3650>       0 disables temp cleanup");
        Console.WriteLine("  --storage-acl-status   Check Incidents/Backups/transition-history ACLs");
        Console.WriteLine("  --storage-acl-repair   Harden storage ACLs and enable ACL health enforcement");
        Console.WriteLine("  --privileged-status    Show JIT / two-person approval / SIEM status");
        Console.WriteLine("  --jit-enable|--jit-disable");
        Console.WriteLine("  --jit-minutes <1-1440> Time-limited recovery grant lifetime");
        Console.WriteLine("  --jit-grantor-add <principal>|--jit-grantor-remove <principal>");
        Console.WriteLine("  --jit-grant <principal> [--jit-reason <text>]");
        Console.WriteLine("  --jit-revoke <grant-id> [--jit-reason <text>]");
        Console.WriteLine("  --approval-enable|--approval-disable");
        Console.WriteLine("  --approval-minutes <1-1440> Approval request lifetime");
        Console.WriteLine("  --approver-add <principal>|--approver-remove <principal>");
        Console.WriteLine("  --approval-status <session-id>");
        Console.WriteLine("  --approval-approve <session-id> [--approval-comment <text>]");
        Console.WriteLine("  --approval-deny <session-id> [--approval-comment <text>]");
        Console.WriteLine("  --siem-enable|--siem-disable");
        Console.WriteLine("  --siem-mode <FileJsonl|Webhook>");
        Console.WriteLine("  --siem-file <path>");
        Console.WriteLine("  --siem-webhook <https-url>");
        Console.WriteLine("  --siem-client-cert <thumbprint>");
        Console.WriteLine("  --siem-timeout-seconds <2-120>");
        Console.WriteLine("  --siem-flush-minutes <1-1440>");
        Console.WriteLine("  --siem-max-outbox <100-100000>");
        Console.WriteLine("  --siem-fail-closed|--siem-fail-open");
        Console.WriteLine("  --siem-status");
        Console.WriteLine("  --siem-flush");
        Console.WriteLine("  --entra-cert-rollover    Add/test/switch Entra certificate; retain previous credential");
        Console.WriteLine("  --ad-auto             Use domain-joined workstation/DC auto discovery");
        Console.WriteLine("  --ad-server <host>    Use an explicit DC (standalone/workstation mode)");
        Console.WriteLine("  --ad-domain <domain>  AD DNS/NetBIOS domain for explicit connection");
        Console.WriteLine("  --ad-user <user>      AD user (DOMAIN\\user or user@domain)");
        Console.WriteLine("  --ad-password-prompt  Prompt securely for AD password; never saved");
        Console.WriteLine("  --ad-integrated       Use current Windows credentials");
        Console.WriteLine("  --ad-port <port>      LDAP/LDAPS port (default 389)");
        Console.WriteLine("  --ad-ldaps            Enable LDAPS/TLS (normally port 636)");
        Console.WriteLine("  --ad-ldap             Use LDAP with signing/sealing (normally port 389)");
        Console.WriteLine("  --output-root <path>  Local/UNC export root for this run");
        Console.WriteLine("  --output-subdirectory <name>  Export subdirectory for this run");
        Console.WriteLine("  --health              Print the local health snapshot as JSON");
        Console.WriteLine("  --install-service     Install/update and start the native Windows Service");
        Console.WriteLine("  --uninstall-service   Stop and remove the native Windows Service");
        Console.WriteLine("  --start-service       Start the installed BitKeyBridge service");
        Console.WriteLine("  --stop-service        Stop the installed BitKeyBridge service");
        Console.WriteLine("  --service-status      Show installed service state and identity");
        Console.WriteLine("  --vault-status        Show AD credential vault metadata (never plaintext)");
        Console.WriteLine("  --vault-save-user     Save AD credential in current-user Credential Manager");
        Console.WriteLine("  --vault-save-machine  Save AD credential with machine DPAPI + restricted ACL");
        Console.WriteLine("  --vault-delete-user   Delete current-user stored AD credential");
        Console.WriteLine("  --vault-delete-machine Delete machine/service stored AD credential");
        Console.WriteLine("  --service-identity-local-system  Run installed service as LocalSystem");
        Console.WriteLine("  --service-identity-gmsa <DOMAIN\\account$>  Configure gMSA/managed account");
        Console.WriteLine("  --service-identity-user <DOMAIN\\user>  Configure regular service account; prompts for password");
        Console.WriteLine("  --service-coverage-enable  Enable scheduled metadata-only Coverage in the service");
        Console.WriteLine("  --service-coverage-disable Disable scheduled Coverage");
        Console.WriteLine("  --service-coverage-interval <minutes>  Coverage interval (15-10080)");
        Console.WriteLine("  --service-coverage-run-on-start  Run Coverage when the service starts");
        Console.WriteLine("  --service-coverage-no-run-on-start  Do not run Coverage immediately on start");
        Console.WriteLine("  --check-update        Check the configured GitHub repository for a newer release");
        Console.WriteLine("  --update              Verify and install the latest stable release");
        Console.WriteLine("  --search-base <DN>    Override scopes for this run; may be repeated");
        Console.WriteLine("  --no-elevation        Do not relaunch through UAC");
        Console.WriteLine("  --self-test           Run offline smoke tests and exit");
        Console.WriteLine("  --version             Show application version");
        Console.WriteLine("  --help                Show this help");
    }

    private static List<BitLockerScope>? ParseScopes(string[] args)
    {
        var result = new List<BitLockerScope>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--search-base", StringComparison.OrdinalIgnoreCase) || i + 1 >= args.Length) continue;
            var dn = args[++i];
            result.Add(new BitLockerScope($"Custom OU {result.Count + 1}", dn));
        }
        return result.Count == 0 ? null : result;
    }
}
