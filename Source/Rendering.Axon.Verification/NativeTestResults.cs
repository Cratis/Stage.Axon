// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace Cratis.Stage.Rendering.Axon.Verification;

internal static class NativeTestResults
{
    internal static IReadOnlyDictionary<string, int> Disabled(IEnumerable<XDocument> reports)
    {
        var cases = reports.SelectMany(report => report.Descendants("testcase")).ToArray();
        if (!cases.Any(test => test.Element("skipped") is null) || cases.Any(Failed))
        {
            throw new NativeVerificationFailed("Every application must execute passing native tests; empty, failed or entirely skipped reports are rejected.");
        }

        return cases.Select(test => test.Element("skipped")).OfType<XElement>()
            .Select(skipped => ((string?)skipped.Attribute("message") ?? skipped.Value).Split(':', 2)[0].Trim())
            .GroupBy(reason => reason, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    internal static void RequireWithinBudget(IReadOnlyDictionary<string, int> disabled, IReadOnlyDictionary<string, int> budget)
    {
        foreach (var (reason, count) in disabled.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"Disabled native tests: {reason}: {count}");
        }

        Console.WriteLine($"Disabled native tests total: {disabled.Values.Sum()}");
        if (disabled.Any(entry => !budget.TryGetValue(entry.Key, out var allowed) || entry.Value > allowed))
        {
            throw new NativeVerificationFailed("Disabled native test counts exceed the checked-in per-reason allowlist.");
        }
    }

    internal static void Verify(string repository, string output, IReadOnlyDictionary<string, int> budget)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, _) in CorpusApplications.Find(repository))
        {
            var reports = Directory.GetFiles(Path.Combine(output, name, "target/surefire-reports"), "TEST-*.xml");
            var disabled = Disabled(reports.Select(XDocument.Load));
            Console.WriteLine($"{name}: {reports.Length} native test reports");
            foreach (var (reason, count) in disabled)
            {
                totals[reason] = totals.GetValueOrDefault(reason) + count;
            }
        }

        RequireWithinBudget(totals, budget);
    }

    static bool Failed(XElement test) => test.Element("failure") is not null || test.Element("error") is not null;
}
