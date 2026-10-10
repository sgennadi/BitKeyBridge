using BitKeyBridge;

var failures = LapsSelfTest.Run().ToList();
failures.AddRange(AdvancedDiagnosticRulesSelfTest.Run());
foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.WriteLine(failures.Count == 0 ? "LAPS regression checks passed." : "LAPS regression checks failed.");
return failures.Count == 0 ? 0 : 1;
