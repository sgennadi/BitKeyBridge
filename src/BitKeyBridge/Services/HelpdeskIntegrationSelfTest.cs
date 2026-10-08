using System.Net;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public static class HelpdeskIntegrationSelfTest
{
    public static IReadOnlyList<string> Run(
        string tempRoot)
    {
        var failures =
            new List<string>();

        void Check(
            string name,
            Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failures.Add(
                    "Helpdesk integration: " +
                    name +
                    " - " +
                    ex.Message);
            }
        }

        void Assert(
            bool condition,
            string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    message);
            }
        }

        Check(
            "enterprise policies are opt-in",
            () =>
            {
                var defaults =
                    new AppConfig();

                Assert(
                    !defaults.RbacEnabled &&
                    !defaults.JitRecoveryEnabled &&
                    !defaults.TwoPersonApprovalEnabled &&
                    !defaults.SiemEnabled &&
                    !defaults.RequireRecoveryAccessReference &&
                    !defaults.SuggestRotationAfterCloudKeyRetrieval &&
                    !defaults.AuditSigningEnabled &&
                    !defaults.RemoteApiEnabled,
                    "A fresh AppConfig enabled an enterprise recovery policy.");
            });

        Check(
            "schema 4 helpdesk prompt migration",
            () =>
            {
                var directory =
                    Path.Combine(
                        tempRoot,
                        "migration-defaults");
                Directory.CreateDirectory(
                    directory);

                var configPath =
                    Path.Combine(
                        directory,
                        "appsettings.json");
                var statusPath =
                    Path.Combine(
                        directory,
                        "status.json");
                var backups =
                    Path.Combine(
                        directory,
                        "backups");

                var legacy =
                    new AppConfig
                    {
                        SchemaVersion =
                            4,
                        RequireRecoveryAccessReference =
                            true,
                        SuggestRotationAfterCloudKeyRetrieval =
                            true,
                        RbacEnabled =
                            false,
                        JitRecoveryEnabled =
                            false,
                        TwoPersonApprovalEnabled =
                            false,
                        SiemEnabled =
                            false
                    };

                JsonStore.WriteAtomic(
                    configPath,
                    legacy);

                var migrated =
                    new ConfigMigrationService(
                        configPath,
                        statusPath,
                        backups)
                    .LoadAndMigrate();

                Assert(
                    migrated.SchemaVersion ==
                    ConfigSchema.CurrentVersion,
                    "Configuration schema did not migrate.");

                Assert(
                    !migrated.RequireRecoveryAccessReference &&
                    !migrated.SuggestRotationAfterCloudKeyRetrieval,
                    "Legacy non-enterprise ticket/rotation prompts were not reset.");
            });

        Check(
            "configured enterprise policy is preserved",
            () =>
            {
                var directory =
                    Path.Combine(
                        tempRoot,
                        "migration-enterprise");
                Directory.CreateDirectory(
                    directory);

                var configPath =
                    Path.Combine(
                        directory,
                        "appsettings.json");

                JsonStore.WriteAtomic(
                    configPath,
                    new AppConfig
                    {
                        SchemaVersion =
                            4,
                        RbacEnabled =
                            true,
                        RequireRecoveryAccessReference =
                            true
                    });

                var migrated =
                    new ConfigMigrationService(
                        configPath,
                        Path.Combine(
                            directory,
                            "status.json"),
                        Path.Combine(
                            directory,
                            "backups"))
                    .LoadAndMigrate();

                Assert(
                    migrated.RbacEnabled &&
                    migrated.RequireRecoveryAccessReference,
                    "An explicitly configured enterprise policy was reset.");
            });

        Check(
            "AD unavailable plus Cloud available merge",
            () =>
            {
                var merged =
                    UnifiedDeviceMerge.Merge(
                        [],
                        [
                            new ManagedDeviceInfo
                            {
                                DeviceName =
                                    "PC-CLOUD",
                                EntraDeviceId =
                                    "entra-id",
                                ManagedDeviceId =
                                    "intune-id",
                                SerialNumber =
                                    "SERIAL-1"
                            }
                        ],
                        [
                            new CloudRecoveryMetadata
                            {
                                ComputerName =
                                    "PC-CLOUD",
                                DeviceId =
                                    "entra-id",
                                RecoveryId =
                                    "key-1"
                            },
                            new CloudRecoveryMetadata
                            {
                                ComputerName =
                                    "PC-CLOUD",
                                DeviceId =
                                    "entra-id",
                                RecoveryId =
                                    "key-2"
                            }
                        ]);

                var row =
                    merged["PC-CLOUD"];

                Assert(
                    !row.FoundInAd &&
                    row.FoundInEntra &&
                    row.FoundInIntune,
                    "Cloud-only result was not preserved when AD rows were absent.");

                Assert(
                    row.RecoveryIds.Count ==
                    2,
                    "Multiple recovery IDs were not retained.");
            });

        Check(
            "Graph 403 remains an explicit failure",
            () =>
            {
                using var graph =
                    new CloudGraphService(
                        new FixedResponseHandler(
                            HttpStatusCode.Forbidden,
                            "{\"error\":{\"code\":\"Authorization_RequestDenied\"}}"));

                var failed =
                    false;

                try
                {
                    _ =
                        graph.SearchManagedDevicesAsync(
                                "fake-token",
                                "PC-403",
                                10)
                            .GetAwaiter()
                            .GetResult();
                }
                catch
                {
                    failed =
                        true;
                }

                Assert(
                    failed,
                    "Mock Graph 403 was incorrectly treated as a successful empty result.");
            });

        Check(
            "missing session credential fails before LDAP",
            () =>
            {
                AdSessionCredentials.Clear();

                try
                {
                    var config =
                        new AppConfig
                        {
                            AdUseExplicitCredentials =
                                true,
                            AdCredentialStorageMode =
                                "Session",
                            AdUsername =
                                "DOMAIN\\operator",
                            AdDomain =
                                "example.test"
                        };

                    var failed =
                        false;

                    try
                    {
                        _ =
                            AdSessionCredentials
                                .CreateNetworkCredential(
                                    config);
                    }
                    catch (InvalidOperationException ex)
                    {
                        failed =
                            ex.Message.Contains(
                                "no session password",
                                StringComparison.OrdinalIgnoreCase);
                    }

                    Assert(
                        failed,
                        "Missing Session-only AD password did not fail with the expected safe error.");
                }
                finally
                {
                    AdSessionCredentials.Clear();
                }
            });

        Check(
            "LAPS history regression suite",
            () =>
            {
                var lapsFailures =
                    LapsSelfTest.Run();

                Assert(
                    lapsFailures.Count ==
                    0,
                    string.Join(
                        "; ",
                        lapsFailures));
            });

        Check(
            "update N to N+1 and history persistence",
            () =>
            {
                var current =
                    UpdateService.ParseVersion(
                        "0.24.3");
                var next =
                    UpdateService.ParseVersion(
                        "0.25.0");

                Assert(
                    next > current,
                    "Version ordering did not detect N+1.");

                var historyPath =
                    Path.Combine(
                        tempRoot,
                        "update-history-test.json");

                var history =
                    new UpdateHistoryService(
                        historyPath);

                history.Append(
                    new UpdateHistoryEntry
                    {
                        Action =
                            "Install",
                        FromVersion =
                            "0.24.3",
                        ToVersion =
                            "0.25.0",
                        Result =
                            "Success",
                        Automatic =
                            true
                    });

                history.Append(
                    new UpdateHistoryEntry
                    {
                        Action =
                            "Rollback",
                        FromVersion =
                            "0.25.0",
                        ToVersion =
                            "0.24.3",
                        Result =
                            "Success"
                    });

                var rows =
                    history.Read();

                Assert(
                    rows.Count ==
                        2 &&
                    rows.Any(
                        x =>
                            x.Action ==
                            "Rollback"),
                    "Update/rollback history did not round-trip.");
            });

        Check(
            "helpdesk profiles contain no secret fields",
            () =>
            {
                var profile =
                    new HelpdeskProfile
                    {
                        Name =
                            "Lab",
                        AdServer =
                            "dc.example.test",
                        AdUsername =
                            "DOMAIN\\operator",
                        CloudTenant =
                            "tenant.example",
                        CloudClientId =
                            "client-id"
                    };

                var json =
                    JsonSerializer.Serialize(
                        profile);

                Assert(
                    !json.Contains(
                        "password",
                        StringComparison.OrdinalIgnoreCase) &&
                    !json.Contains(
                        "token",
                        StringComparison.OrdinalIgnoreCase) &&
                    !json.Contains(
                        "recoverykey",
                        StringComparison.OrdinalIgnoreCase),
                    "Profile serialization contains a secret-bearing field.");
            });

        return failures;
    }

    private sealed class FixedResponseHandler :
        HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public FixedResponseHandler(
            HttpStatusCode status,
            string body)
        {
            _status =
                status;
            _body =
                body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ =
                request;

            return Task.FromResult(
                new HttpResponseMessage(
                    _status)
                {
                    Content =
                        new StringContent(
                            _body,
                            Encoding.UTF8,
                            "application/json")
                });
        }
    }
}
