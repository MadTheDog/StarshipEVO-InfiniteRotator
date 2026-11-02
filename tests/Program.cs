using System;
using System.IO;
using InfiniteRotator;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        checks++;
    }
    private static bool Near(float x, float y) => Math.Abs(x-y) < 0.00001f;
    public static void Main()
    {
        Check(Near(SpinPolicy.Normalize(1.01f), .01f), "Forward wrap");
        Check(Near(SpinPolicy.Normalize(-.01f), .99f), "Reverse wrap");
        Check(Near(SpinPolicy.Normalize(0f), 0f) && Near(SpinPolicy.Normalize(1f), 0f), "Exact boundary");
        float phase = .999f, rollback = .998f;
        SpinPolicy.Rebase(ref phase, ref rollback);
        Check(Near(phase, .999f) && Near(rollback, .998f), "No early wrap");
        phase = 1.002f; rollback = .998f;
        SpinPolicy.Rebase(ref phase, ref rollback);
        Check(Near(phase, .002f) && Near(rollback, -.002f), "Forward collision rollback retains previous orientation");
        phase = -.002f; rollback = .002f;
        SpinPolicy.Rebase(ref phase, ref rollback);
        Check(Near(phase, .998f) && Near(rollback, 1.002f), "Reverse collision rollback retains previous orientation");
        double accumulated = 0;
        phase = 0;
        for (int i=0; i<100000; i++)
        {
            rollback=phase; phase += .007f; accumulated+=.007f;
            SpinPolicy.Rebase(ref phase, ref rollback);
            Check(phase >= 0 && phase < 1, "Long-running phase stays bounded");
        }
        Check(Math.Abs(phase - (accumulated % 1)) < .002, "Long-running phase tracks signed motion");
        Check(!SpinPolicy.CanRegister(25, true) && !SpinPolicy.CanRegister(1024, false)
            && !SpinPolicy.CanRegister(-1, false) && !SpinPolicy.CanRegister(26, false)
            && !SpinPolicy.CanRegister(330, false) && SpinPolicy.CanRegister(1000, false), "Registry safety");
        var native = new byte[] { 7, 8, 9 };
        var bytes = SpinPolicy.Encode(native, true, true, 90, .875f);
        var saved = SpinPolicy.Decode(bytes);
        Check(saved.Run && saved.Reverse && Near(saved.Speed, 90) && Near(saved.Phase, .875f)
            && saved.Native.Length==3 && saved.Native[2]==9, "Persistence round trip");
        foreach (var bad in new[] { Array.Empty<byte>(), new byte[] { 1, 2, 3 }, bytes[..^1] })
        {
            bool rejected=false;
            try { SpinPolicy.Decode(bad); } catch (InvalidDataException) { rejected=true; }
            Check(rejected, "Malformed payload rejected");
        }
        var unknown=(byte[])bytes.Clone(); unknown[4]=99;
        bool versionRejected=false;
        try { SpinPolicy.Decode(unknown); } catch (InvalidDataException) { versionRejected=true; }
        Check(versionRejected, "Unknown version rejected");
        Check(SpinPolicy.SpeedOrDefault(float.NaN)==45 && SpinPolicy.SpeedOrDefault(float.PositiveInfinity)==45
            && SpinPolicy.SpeedOrDefault(-3)==0 && SpinPolicy.SpeedOrDefault(999)==180, "Invalid speeds constrained");
        Console.WriteLine($"PASS: {checks} assertions (including 100000 bounded-motion steps).");
    }
}
