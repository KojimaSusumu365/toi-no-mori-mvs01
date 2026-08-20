namespace ToiNoMori.Testing;

public static class SpecTestRunner
{
    public static async Task<int> RunAsync(string suiteName, IReadOnlyList<SpecTest> tests)
    {
        Console.WriteLine($"# {suiteName}");

        var duplicateIds = tests
            .GroupBy(test => test.TestCaseId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            Console.WriteLine("Bail out! duplicate test IDs: " + string.Join(", ", duplicateIds));
            return 1;
        }

        Console.WriteLine($"1..{tests.Count}");

        var failed = 0;
        for (var index = 0; index < tests.Count; index++)
        {
            var test = tests[index];
            try
            {
                await test.Execute();
                Console.WriteLine($"ok {index + 1} - {test.TestCaseId} [{test.RequirementId}] {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine($"not ok {index + 1} - {test.TestCaseId} [{test.RequirementId}] {test.Name}");
                Console.WriteLine($"  ---\n  message: {SingleLine(exception.Message)}\n  ...");
            }
        }

        Console.WriteLine($"# result: {tests.Count - failed} passed; {failed} failed; {tests.Count} total");
        return failed == 0 ? 0 : 1;
    }

    private static string SingleLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
