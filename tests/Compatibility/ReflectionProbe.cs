using System;
using System.IO;
using System.Reflection;
using InfiniteRotator;

internal static class ReflectionProbe
{
    public static int Main(string[] args)
    {
        string engine = Path.GetFullPath(args[0]);
        string dependencies = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.GetDirectoryName(engine);
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            string dependency = Path.Combine(dependencies, new AssemblyName(eventArgs.Name).Name + ".dll");
            return File.Exists(dependency) ? Assembly.LoadFrom(dependency) : null;
        };
        var failures = GameContract.Validate(new ReflectionContractView(Assembly.LoadFrom(engine)));
        foreach (string failure in failures) Console.WriteLine(failure);
        if (failures.Count > 0 && args.Length > 2 && args[2] == "diagnose")
        {
            var view = new ReflectionContractView(Assembly.LoadFrom(engine));
            foreach (var expected in NativeLifecyclePatterns.Expected)
            {
                int separator = expected.Key.LastIndexOf("::", StringComparison.Ordinal);
                var type = view.Read(expected.Key.Substring(0, separator));
                var method = type.Methods.Find(m => m.Name == expected.Key.Substring(separator + 2));
                if (Array.IndexOf(expected.Value, method.Flow) >= 0) continue;
                string[] a = expected.Value[0].Split('|'), b = method.Flow.Split('|');
                for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
                    if (a[i] != b[i]) Console.WriteLine(expected.Key + " #" + i + "\nEXPECTED " + a[i] + "\nACTUAL " + b[i]);
            }
        }
        bool expectedRejection = args.Length > 2 && args[2] == "reject";
        bool passed = (failures.Count == 0) != expectedRejection;
        Console.WriteLine((passed ? "PASS " : "FAIL ") + "runtime reflection "
            + (expectedRejection ? "rejects broken contracts: " : "accepts compatible contracts: ") + engine);
        return passed ? 0 : 1;
    }
}
