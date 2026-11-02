using System;
using System.IO;
namespace InfiniteRotator
{
    internal struct SpinSave
    {
        public byte[] Native;
        public bool Run, Reverse;
        public float Speed, Phase;
    }
    internal static class SpinPolicy
    {
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static float Normalize(float phase) => Finite(phase) ? phase - (float)Math.Floor(phase) : 0;
        public static void Rebase(ref float phase, ref float rollback)
        {
            float turns = (float)Math.Floor(phase);
            phase -= turns;
            rollback -= turns;
        }
        public static bool CanRegister(int id, bool occupied) => !occupied && id > 0 && id < 1024 && id != 25 && id != 26 && id != 330;
        public static float SpeedOrDefault(float speed) => Finite(speed) ? Math.Max(0, Math.Min(180, speed)) : 45;
        public static byte[] Encode(byte[] native, bool run, bool reverse, float speed, float phase)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0x31525249); // IRR1, little endian
                writer.Write((byte)1);
                writer.Write(run); writer.Write(reverse);
                writer.Write(SpeedOrDefault(speed)); writer.Write(Normalize(phase));
                writer.Write(native.Length); writer.Write(native);
                return stream.ToArray();
            }
        }
        public static SpinSave Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 19) throw new InvalidDataException("Missing infinite rotator data.");
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != 0x31525249 || reader.ReadByte() != 1)
                    throw new InvalidDataException("Unsupported infinite rotator save format.");
                byte run = reader.ReadByte(), reverse = reader.ReadByte();
                float speed = reader.ReadSingle(), phase = reader.ReadSingle();
                int length = reader.ReadInt32();
                if (run > 1 || reverse > 1 || !Finite(speed) || speed < 0 || speed > 180 || !Finite(phase)
                    || phase < 0 || phase >= 1 || length <= 0 || length != stream.Length-stream.Position)
                    throw new InvalidDataException("Invalid infinite rotator save payload.");
                return new SpinSave { Native=reader.ReadBytes(length), Run=run==1, Reverse=reverse==1, Speed=speed, Phase=phase };
            }
        }
    }
}
