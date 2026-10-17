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
                    !defaults.RemoteApiEnabled &&
                    !defaults.RequireTrustedUpdateSignature &&
                    !defaults.CoveragePolicyEnabled &&
                    !defaults.ServiceCoverageEnabled &&
                    !defaults.StorageAclHardeningEnabled,
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

                var row = merged.Values.Single();

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
            "AD and Entra same-name records remain distinct",
            () =>
            {
                var merged = UnifiedDeviceMerge.Merge(
                    [new AdComputerInfo
                    {
                        ComputerName = "LAB-01",
                        ObjectGuid = "11111111-1111-1111-1111-111111111111",
                        DistinguishedName = "CN=LAB-01,OU=Lab,DC=example,DC=test"
                    }],
                    [new ManagedDeviceInfo
                    {
                        DeviceName = "LAB-01",
                        ManagedDeviceId = "intune-lab-01",
                        EntraDeviceId = "22222222-2222-2222-2222-222222222222"
                    }],
                    [new CloudRecoveryMetadata
                    {
                        ComputerName = "LAB-01",
                        DeviceId = "22222222-2222-2222-2222-222222222222",
                        RecoveryId = "recovery-lab-01"
                    }]);

                Assert(merged.Count == 2,
                    "Same-named AD and cloud records were incorrectly merged.");
                var ad = merged.Values.Single(x => x.FoundInAd);
                var cloud = merged.Values.Single(x => x.FoundInIntune);
                Assert(!ad.FoundInIntune &&
                       ad.RecoveryIds.Count == 0 &&
                       cloud.RecoveryIds.SequenceEqual(["recovery-lab-01"]) &&
                       ad.IdentityStatus == "Name collision" &&
                       cloud.IdentityStatus == "Name collision",
                    "Name collision was not marked or cloud recovery key was assigned to AD.");
            });

        Check(
            "same computer name with different AD GUIDs remains separate",
            () =>
            {
                var merged = UnifiedDeviceMerge.Merge(
                    [
                        new AdComputerInfo
                        {
                            ComputerName = "LAB-02",
                            ObjectGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                            DistinguishedName = "CN=LAB-02,OU=Old,DC=example,DC=test"
                        },
                        new AdComputerInfo
                        {
                            ComputerName = "LAB-02",
                            ObjectGuid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                            DistinguishedName = "CN=LAB-02,OU=New,DC=example,DC=test"
                        }
                    ],
                    [], []);

                Assert(merged.Count == 2 &&
                       merged.Values.All(row => row.IdentityStatus == "Name collision") &&
                       merged.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2,
                    "Distinct AD computer objectGUIDs collapsed into one device.");
            });

        Check(
            "conflicting Entra IDs do not attach recovery keys to managed devices",
            () =>
            {
                const string sameId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
                var merged = UnifiedDeviceMerge.Merge(
                    [],
                    [
                        new ManagedDeviceInfo
                        {
                            DeviceName = "DUPLICATE-PC",
                            ManagedDeviceId = "intune-one",
                            EntraDeviceId = sameId
                        },
                        new ManagedDeviceInfo
                        {
                            DeviceName = "DUPLICATE-PC",
                            ManagedDeviceId = "intune-two",
                            EntraDeviceId = sameId
                        }
                    ],
                    [new CloudRecoveryMetadata
                    {
                        ComputerName = "DUPLICATE-PC",
                        DeviceId = sameId,
                        RecoveryId = "must-remain-isolated"
                    }]);

                Assert(merged.Count == 3 &&
                       merged.Values.Where(x => x.FoundInIntune)
                           .All(x => x.RecoveryIds.Count == 0 &&
                                     x.IdentityStatus == "Ambiguous device ID") &&
                       merged.Values.Count(x => x.RecoveryIds.Count > 0) == 1 &&
                       merged.Values.Any(x => !x.FoundInIntune &&
                           x.RecoveryIds.Contains("must-remain-isolated")),
                    "An ambiguous Entra ID caused incorrect recovery-key attachment.");
            });

        Check(
            "conflicting Entra IDs for a single Intune managedDeviceId fail closed",
            () =>
            {
                var merged = UnifiedDeviceMerge.Merge(
                    [],
                    [
                        new ManagedDeviceInfo
                        {
                            DeviceName = "BAD-INTUNE",
                            ManagedDeviceId = "same-managed-id",
                            EntraDeviceId = "11111111-1111-1111-1111-111111111111"
                        },
                        new ManagedDeviceInfo
                        {
                            DeviceName = "BAD-INTUNE",
                            ManagedDeviceId = "same-managed-id",
                            EntraDeviceId = "22222222-2222-2222-2222-222222222222"
                        }
                    ],
                    [
                        new CloudRecoveryMetadata
                        {
                            ComputerName = "BAD-INTUNE",
                            DeviceId = "11111111-1111-1111-1111-111111111111",
                            RecoveryId = "separate-key"
                        }
                    ]);
                var managed = merged.Values.Single(x => x.FoundInIntune);
                Assert(managed.IdentityStatus == "Ambiguous device ID" &&
                       !managed.CanRotateExplicitIntuneDevice &&
                       managed.RecoveryIds.Count == 0 &&
                       merged.Values.Single(x => x.RecoveryIds.Count > 0).FoundInIntune == false,
                    "Conflicting Entra identities must never inherit key IDs or enable rotation.");
            });

        Check(
            "manually selected Intune target can rotate despite a hostname collision",
            () =>
            {
                var row = new UnifiedDeviceInfo
                {
                    ComputerName = "LAB-DEVICE",
                    FoundInIntune = true,
                    ManagedDeviceId = "exact-managed-object",
                    EntraDeviceId = "10101010-1010-1010-1010-101010101010",
                    IdentityStatus = "Name collision"
                };
                Assert(row.CanRotateExplicitIntuneDevice,
                    "A manually selected unique managedDeviceId was incorrectly blocked by an unrelated same-name AD device.");

                row.IdentityStatus = "Ambiguous device ID";
                Assert(!row.CanRotateExplicitIntuneDevice,
                    "Conflicting Entra device IDs must block explicit Intune rotation.");

                row.IdentityStatus = "Intune object";
                row.ManagedDeviceId = string.Empty;
                Assert(!row.CanRotateExplicitIntuneDevice,
                    "Missing managedDeviceId must not be treated as a valid rotation target.");
            });

        Check(
            "independent metadata sources retain success and error",
            () =>
            {
                using var cts = new CancellationTokenSource();
                var good = MetadataSourceIsolation.ExecuteAsync<int>(
                    () => Task.FromResult(new List<int> { 1, 2 }),
                    cts.Token);
                var bad = MetadataSourceIsolation.ExecuteAsync<int>(
                    () => Task.FromException<List<int>>(
                        new InvalidOperationException("LDAP unavailable")),
                    cts.Token);

                Task.WhenAll(good, bad).GetAwaiter().GetResult();
                Assert(good.Result.Succeeded && good.Result.Rows.Count == 2 &&
                       !bad.Result.Succeeded &&
                       bad.Result.Rows.Count == 0 &&
                       bad.Result.Error!.Contains("LDAP unavailable",
                           StringComparison.Ordinal),
                    "A failure of one metadata source discarded the other source.");

                cts.Cancel();
                var canceled = false;
                try
                {
                    MetadataSourceIsolation.ExecuteAsync<int>(
                        () => Task.FromResult(new List<int> { 99 }),
                        cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }

                Assert(canceled,
                    "Canceled metadata search was incorrectly treated as an empty result.");
            });

        Check(
            "Devices AD source survives failed Intune and Entra",
            () =>
            {
                var report = UnifiedDeviceSearchReport.Combine(
                    new MetadataLookupResult<AdComputerInfo>(
                        [new AdComputerInfo
                        {
                            ComputerName = "LAB-REPORT-01",
                            ObjectGuid = "19191919-1919-1919-1919-191919191919",
                            DistinguishedName = "CN=LAB-REPORT-01,DC=example,DC=test"
                        }], null),
                    new MetadataLookupResult<ManagedDeviceInfo>(
                        [], "Graph returned 403 for Intune"),
                    new MetadataLookupResult<CloudRecoveryMetadata>(
                        [], "Graph returned 429 for Entra"));

                Assert(report.IsPartial && report.Rows.Count == 1 &&
                       report.Rows[0].FoundInAd &&
                       report.Rows[0].RecoveryMetadataIncomplete &&
                       !report.IntuneSucceeded && !report.EntraSucceeded &&
                       report.DiagnosticSummary.Contains("403", StringComparison.Ordinal) &&
                       report.DiagnosticSummary.Contains("429", StringComparison.Ordinal),
                    "Partial Graph failure should preserve AD and mark recovery keys unverified.");
            });

        Check(
            "Devices Intune source survives failed Entra metadata",
            () =>
            {
                var report = UnifiedDeviceSearchReport.Combine(
                    null,
                    new MetadataLookupResult<ManagedDeviceInfo>(
                        [new ManagedDeviceInfo
                        {
                            DeviceName = "LAB-CLOUD-02",
                            ManagedDeviceId = "managed-two",
                            EntraDeviceId = "20202020-2020-2020-2020-202020202020"
                        }], null),
                    new MetadataLookupResult<CloudRecoveryMetadata>(
                        [], "403 insufficient Graph permissions"));

                Assert(report.IsPartial && report.Rows.Count == 1 &&
                       report.Rows[0].FoundInIntune &&
                       report.Rows[0].RecoveryIds.Count == 0 &&
                       report.Rows[0].RecoveryMetadataIncomplete &&
                       report.SourceSummary.Contains("Entra BitLocker: failed",
                           StringComparison.Ordinal),
                    "Entra permission denial should not hide Intune device information.");
            });

        Check(
            "Devices cloud source survives failed AD lookup",
            () =>
            {
                var report = UnifiedDeviceSearchReport.Combine(
                    new MetadataLookupResult<AdComputerInfo>(
                        [], "LDAP server unavailable"),
                    new MetadataLookupResult<ManagedDeviceInfo>(
                        [new ManagedDeviceInfo
                        {
                            DeviceName = "LAB-CLOUD-03",
                            ManagedDeviceId = "managed-three",
                            EntraDeviceId = "30303030-3030-3030-3030-303030303030"
                        }], null),
                    new MetadataLookupResult<CloudRecoveryMetadata>(
                        [new CloudRecoveryMetadata
                        {
                            ComputerName = "LAB-CLOUD-03",
                            DeviceId = "30303030-3030-3030-3030-303030303030",
                            RecoveryId = "cloud-key-three"
                        }], null));

                Assert(report.IsPartial && !report.AdSucceeded &&
                       report.Rows.Count == 1 &&
                       report.Rows[0].RecoveryIds.SequenceEqual(["cloud-key-three"]) &&
                       report.Rows[0].FoundInIntune,
                    "Failed AD must not discard independently retrieved cloud key metadata.");
            });

        Check(
            "Devices all attempted source failures are explicit",
            () =>
            {
                var failed = false;
                try
                {
                    _ = UnifiedDeviceSearchReport.Combine(
                        null,
                        new MetadataLookupResult<ManagedDeviceInfo>(
                            [], "Intune unavailable"),
                        new MetadataLookupResult<CloudRecoveryMetadata>(
                            [], "Entra unavailable"));
                }
                catch (InvalidOperationException ex)
                {
                    failed = ex.Message.Contains("All device inventory sources failed",
                        StringComparison.Ordinal);
                }

                Assert(failed, "All failed sources must not become a successful empty result.");
            });

        Check(
            "Cloud Graph permissions default to read-only",
            () =>
            {
                var config = new CloudAuthConfig();
                Assert(!config.EnableIntuneRotationPermissions &&
                       ManagedPermissionPolicy.Permission(false) ==
                           "DeviceManagementManagedDevices.Read.All" &&
                       ManagedPermissionPolicy.Permission(true) ==
                           "DeviceManagementManagedDevices.ReadWrite.All",
                    "Intune application setup was not read-only by default.");
            });

        Check(
            "Start cloud identity mapping remains metadata-only",
            () =>
            {
                var cloud = new UnifiedDeviceInfo
                {
                    ComputerName = "CLOUD-01",
                    IdentityKey = "INTUNE:MANAGED:01",
                    EntraDeviceId = "entra-01",
                    FoundInIntune = true,
                    RecoveryMetadataIncomplete = true
                };
                cloud.RecoveryIds.Add("guid-only");
                var startRows = HomeCloudSearchMapper.Map([cloud]);
                Assert(startRows.Count == 1 &&
                       startRows[0].IsCloudRecord &&
                       startRows[0].SourceIdentity == cloud.IdentityKey &&
                       startRows[0].BitLockerLookupFailed &&
                       HelpdeskStatus.ResolveHomeDefaultAction(startRows[0]) ==
                           HomeDefaultAction.Devices,
                    "Start treated Cloud recovery metadata as AD Recovery.");
            });

        Check(
            "Start does not hide conflicting cloud identities",
            () =>
            {
                var first = new UnifiedDeviceInfo
                {
                    ComputerName = "SHARED-PC",
                    IdentityKey = "INTUNE:MANAGED:duplicate",
                    FoundInIntune = true,
                    EntraDeviceId = "entra-device-one"
                };
                first.RecoveryIds.Add("recovery-id-one");
                var second = new UnifiedDeviceInfo
                {
                    ComputerName = "SHARED-PC",
                    IdentityKey = "INTUNE:MANAGED:duplicate",
                    FoundInIntune = true,
                    EntraDeviceId = "entra-device-two"
                };
                var mapped = HomeCloudSearchMapper.Map([first, second]);
                Assert(mapped.Count == 2 &&
                       mapped.All(row =>
                           row.IdentityAmbiguous &&
                           row.BitLockerLookupFailed &&
                           !RecoveryWorkflowNavigationPolicy.HasAuthoritativeCloudDeviceId(row)),
                    "Duplicate source ID was collapsed or enabled guided recovery.");

                var unknown = HomeCloudSearchMapper.Map(
                    [new UnifiedDeviceInfo
                    {
                        ComputerName = "UNRESOLVED-PC",
                        IdentityKey = string.Empty,
                        FoundInIntune = true
                    }]);
                Assert(unknown.Count == 1 &&
                       unknown[0].IdentityAmbiguous &&
                       !RecoveryWorkflowNavigationPolicy.HasAuthoritativeCloudDeviceId(
                           unknown[0]),
                    "Missing cloud source ID was treated as authoritative.");
            });

        Check(
            "Device 360 summary excludes recovery passwords",
            () =>
            {
                var row = new UnifiedDeviceInfo
                {
                    ComputerName = "PC-360",
                    IdentityKey = "INTUNE:MANAGED:abc",
                    IdentityStatus = "Name collision",
                    FoundInIntune = true,
                    ManagedDeviceId = "abc",
                    EntraDeviceId = "entra-abc",
                    RecoveryMetadataIncomplete = true
                };
                row.RecoveryIds.Add("recovery-guid-metadata-only");
                var summary = Device360Formatter.Format(row);
                Assert(summary.Contains("PC-360", StringComparison.Ordinal) &&
                       summary.Contains("abc", StringComparison.Ordinal) &&
                       summary.Contains("unverified", StringComparison.OrdinalIgnoreCase) &&
                       !summary.Contains("recovery-guid-metadata-only", StringComparison.Ordinal),
                    "Device 360 must not leak key IDs into copied summary.");
            });

        Check(
            "LAPS Coverage expiry classification never implies missing backup",
            () =>
            {
                var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
                var devices = new[]
                {
                    new LapsCoverageDevice
                    {
                        ComputerName = "LAB-WINDOWS",
                        IdentityKey = "AD:GUID:one",
                        WindowsMetadataVisible = true,
                        WindowsExpirationUtc = now.AddHours(-1)
                    },
                    new LapsCoverageDevice
                    {
                        ComputerName = "LAB-LEGACY",
                        IdentityKey = "AD:GUID:two",
                        LegacyMetadataVisible = true,
                        LegacyExpirationUtc = now.AddDays(2)
                    },
                    new LapsCoverageDevice
                    {
                        ComputerName = "LAB-HIDDEN",
                        IdentityKey = "AD:GUID:three"
                    }
                };
                var summary = LapsCoverageService.Summarize(devices, now);
                Assert(summary.Scanned == 3 &&
                       summary.WindowsMetadata == 1 &&
                       summary.LegacyMetadata == 1 &&
                       summary.Unverified == 1 &&
                       summary.Expired == 1 &&
                       summary.ExpiringSoon == 1 &&
                       devices[2].BackupEvidence == "Not verified",
                    "LAPS expiry/no-metadata classification is incorrect.");
            });

        Check(
            "LAPS snapshot comparison uses stable AD IDs, not computer names",
            () =>
            {
                var first = new LapsCoverageSnapshot
                {
                    SearchBaseDn = "DC=example,DC=test",
                    GeneratedAtUtc = DateTime.UtcNow.AddDays(-1),
                    Devices =
                    [
                        new LapsCoverageDevice
                        {
                            ComputerName = "PC-ORIGINAL",
                            IdentityKey = "AD:GUID:abc"
                        }
                    ]
                };
                var next = new LapsCoverageSnapshot
                {
                    SearchBaseDn = first.SearchBaseDn,
                    GeneratedAtUtc = DateTime.UtcNow,
                    Devices =
                    [
                        new LapsCoverageDevice
                        {
                            ComputerName = "PC-RENAMED",
                            IdentityKey = "AD:GUID:abc",
                            WindowsMetadataVisible = true,
                            WindowsExpirationUtc = DateTime.UtcNow.AddDays(14)
                        }
                    ]
                };
                var delta = LapsCoverageSnapshotService.Compare(first, next);
                Assert(delta.Changes.Any(x =>
                           x.ChangeType == "Backup metadata became visible") &&
                       !delta.Changes.Any(x => x.ChangeType == "New AD object") &&
                       !delta.Changes.Any(x => x.ChangeType == "No longer visible in scan"),
                    "Renamed AD computer generated a false object removal/addition.");
                next.Truncated = true;
                var refused = false;
                try
                {
                    LapsCoverageSnapshotService.Compare(first, next);
                }
                catch (InvalidOperationException)
                {
                    refused = true;
                }
                Assert(refused, "Truncated scan must not be compared.");
            });

        Check(
            "LAPS expiration request requires a single verified AD policy",
            () =>
            {
                var windows = new LapsCoverageDevice
                {
                    ComputerName = "PC-001",
                    ObjectGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    DistinguishedName = "CN=PC-001,DC=example,DC=test",
                    WindowsMetadataVisible = true
                };
                Assert(windows.CanRequestAdExpiration &&
                       AdLapsExpirationService.AttributeFor(windows) ==
                           "msLAPS-PasswordExpirationTime",
                    "Windows LAPS expiration target did not resolve.");
                var legacy = new LapsCoverageDevice
                {
                    LegacyMetadataVisible = true
                };
                Assert(AdLapsExpirationService.AttributeFor(legacy) ==
                           "ms-Mcs-AdmPwdExpirationTime",
                    "Legacy LAPS expiration target did not resolve.");
                windows.LegacyMetadataVisible = true;
                var blocked = false;
                try
                {
                    AdLapsExpirationService.AttributeFor(windows);
                }
                catch (InvalidOperationException)
                {
                    blocked = true;
                }
                Assert(blocked && !windows.CanRequestAdExpiration,
                    "Mixed LAPS metadata must never be auto-expired.");
            });

        Check(
            "LAPS Coverage CSV escapes spreadsheet formulas",
            () =>
            {
                var csv = LapsCoverageCsvService.Quote("=1+2");
                Assert(csv.StartsWith("\"'=", StringComparison.Ordinal) &&
                       LapsCoverageCsvService.Quote("x\"y") ==
                           "\"x\"\"y\"",
                    "CSV formula and quote escaping failed.");
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
