using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PrinterHub.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute;

public sealed class AssertException(string message) : Exception(message);

public static class Assert
{
    public static void True(bool cond, string? msg = null, [CallerArgumentExpression(nameof(cond))] string? expr = null)
    {
        if (!cond) throw new AssertException(msg ?? $"Expected true: {expr}");
    }

    public static void False(bool cond, [CallerArgumentExpression(nameof(cond))] string? expr = null) =>
        True(!cond, $"Expected false: {expr}");

    public static void Equal<T>(T expected, T actual, [CallerArgumentExpression(nameof(actual))] string? expr = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertException($"{expr}: expected <{expected}> but was <{actual}>");
    }

    public static void SequenceEqual(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        if (!expected.SequenceEqual(actual))
            throw new AssertException($"Bytes differ.\nExpected: {Convert.ToHexString(expected)}\nActual:   {Convert.ToHexString(actual)}");
    }

    public static void Contains(string expected, string actual) =>
        True(actual.Contains(expected, StringComparison.Ordinal), $"Expected to contain <{expected}> in <{Shorten(actual)}>");

    public static void DoesNotContain(string notExpected, string actual) =>
        True(!actual.Contains(notExpected, StringComparison.Ordinal), $"Expected NOT to contain <{notExpected}> in <{Shorten(actual)}>");

    public static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T ex) { return ex; }
        catch (Exception ex) { throw new AssertException($"Expected {typeof(T).Name} but got {ex.GetType().Name}: {ex.Message}"); }
        throw new AssertException($"Expected {typeof(T).Name} but nothing was thrown");
    }

    private static string Shorten(string s) => s.Length > 300 ? s[..300] + "…" : s;
}

public static class TestRunner
{
    public static async Task<int> RunAsync(string? filter)
    {
        var tests = Assembly.GetExecutingAssembly().GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<TestAttribute>() is not null)
                .Select(m => (type: t, method: m)))
            .Where(x => filter is null || $"{x.type.Name}.{x.method.Name}".Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.type.Name).ThenBy(x => x.method.Name)
            .ToList();

        int passed = 0, failed = 0;
        var total = Stopwatch.StartNew();
        foreach (var (type, method) in tests)
        {
            string name = $"{type.Name}.{method.Name}";
            var sw = Stopwatch.StartNew();
            try
            {
                object? instance = method.IsStatic ? null : Activator.CreateInstance(type);
                object? result = method.Invoke(instance, null);
                if (result is Task task) await task.WaitAsync(TimeSpan.FromSeconds(30));
                if (instance is IAsyncDisposable ad) await ad.DisposeAsync();
                passed++;
                Write(ConsoleColor.Green, "PASS", $"{name} ({sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException { InnerException: { } ie } ? ie : ex;
                failed++;
                Write(ConsoleColor.Red, "FAIL", $"{name}: {inner.GetType().Name}: {inner.Message}");
            }
        }
        Write(failed == 0 ? ConsoleColor.Green : ConsoleColor.Red, "SUM",
            $"{passed} passed, {failed} failed, {tests.Count} total in {total.ElapsedMilliseconds} ms");
        return failed == 0 ? 0 : 1;
    }

    private static void Write(ConsoleColor c, string tag, string msg)
    {
        var old = Console.ForegroundColor;
        Console.ForegroundColor = c;
        Console.Write($"[{tag}] ");
        Console.ForegroundColor = old;
        Console.WriteLine(msg);
    }
}
