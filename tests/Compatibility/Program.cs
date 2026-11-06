using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;
using InfiniteRotator;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal sealed class CecilView : IContractView
{
    private readonly ModuleDefinition module;
    internal CecilView(ModuleDefinition module) { this.module = module; }
    public ContractType Read(string name)
    {
        var type = module.GetType(name);
        if (type == null) return null;
        var t = new ContractType { Name = type.FullName, BaseType = type.BaseType?.FullName, Sealed = type.IsSealed };
        foreach (var f in type.Fields.Where(f => GameContract.NeedsField(name, f.Name))) t.Fields[f.Name] = new ContractField { Type = f.FieldType.FullName,
            Static = f.IsStatic, ReadOnly = f.IsInitOnly || f.IsLiteral, Constant = f.HasConstant ? f.Constant : null };
        foreach (var p in type.Properties.Where(p => name == GameContract.Rotor + "/RotatorSaveData"))
            foreach (var a in p.CustomAttributes.Where(a => a.AttributeType.FullName == "MessagePack.KeyAttribute"))
                t.SaveKeys[a.ConstructorArguments[0].Value.ToString()] = p.Name + ":" + p.PropertyType.FullName;
        foreach (var m in type.Methods.Where(m => GameContract.RequiredMethods.Contains(m.Name)))
        {
            var shape = new ContractMethod { Name = m.Name, Returns = m.ReturnType.FullName, Static = m.IsStatic,
                Virtual = m.IsVirtual, Final = m.IsFinal, Accessible = m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
                Parameters = m.Parameters.Select(p => p.ParameterType.FullName).ToArray() };
            if (m.HasBody)
                foreach (var i in m.Body.Instructions)
                {
                    if (i.Operand is MethodReference call && (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt)) shape.Operations.Add("call:" + call.Name);
                    if (i.Operand is FieldReference field) shape.Operations.Add("field:" + field.Name);
                }
            if (m.HasBody && NativeLifecyclePatterns.Expected.ContainsKey(name + "::" + m.Name)) shape.Flow = Flow(m);
            t.Methods.Add(shape);
        }
        return t;
    }
    private static string Target(MethodReference reference)
    {
        var m = reference.GetElementMethod();
        return TypeName(m.DeclaringType) + "::" + m.Name + (m.GenericParameters.Count == 0 ? "" : "`" + m.GenericParameters.Count)
            + "(" + string.Join(",", m.Parameters.Select(p => TypeName(p.ParameterType))) + ")->" + TypeName(m.ReturnType);
    }
    private static string TypeName(TypeReference type)
    {
        if (type is GenericParameter parameter) return "!" + parameter.Position;
        if (type is GenericInstanceType generic) return generic.ElementType.FullName + "<" + string.Join(",", generic.GenericArguments.Select(TypeName)) + ">";
        if (type is ByReferenceType byref) return TypeName(byref.ElementType) + "&";
        if (type is PointerType pointer) return TypeName(pointer.ElementType) + "*";
        if (type is ArrayType array) return TypeName(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
        return type.FullName;
    }
    internal static string Flow(MethodDefinition m)
    {
        var instructions = m.Body.Instructions;
        return "locals=" + string.Join(",", m.Body.Variables.Select(v => TypeName(v.VariableType)))
            + ";init=" + m.Body.InitLocals + ";eh=" + m.Body.ExceptionHandlers.Count + "|"
            + string.Join("|", instructions.Select(i =>
            {
                string operand = i.Operand switch
                {
                    null => "",
                    MethodReference method => Target(method),
                    FieldReference f => TypeName(f.DeclaringType) + "::" + f.Name + ":" + TypeName(f.FieldType),
                    Instruction branch => instructions.IndexOf(branch).ToString(CultureInfo.InvariantCulture),
                    Instruction[] branches => string.Join(",", branches.Select(b => instructions.IndexOf(b))),
                    VariableDefinition variable => variable.Index.ToString(CultureInfo.InvariantCulture),
                    ParameterDefinition parameter => (parameter.Index + (m.HasThis ? 1 : 0)).ToString(CultureInfo.InvariantCulture),
                    TypeReference type => TypeName(type),
                    float value => value.ToString("R", CultureInfo.InvariantCulture),
                    double value => value.ToString("R", CultureInfo.InvariantCulture),
                    _ => Convert.ToString(i.Operand, CultureInfo.InvariantCulture)
                };
                return i.OpCode.Name + " " + operand;
            }));
    }
}

internal static class Program
{
    private static int checks;
    private static DefaultAssemblyResolver resolver;
    private static readonly List<string> passed = new List<string>();
    private static IEnumerable<TypeDefinition> Types(TypeDefinition type)
    {
        yield return type;
        foreach (var child in type.NestedTypes)
            foreach (var descendant in Types(child)) yield return descendant;
    }
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAIL " + name);
        checks++; passed.Add(name);
    }
    private static ModuleDefinition Read(byte[] bytes) => ModuleDefinition.ReadModule(new MemoryStream(bytes),
        new ReaderParameters { InMemory = true, ReadingMode = ReadingMode.Deferred, AssemblyResolver = resolver });
    private static void Reject(byte[] bytes, string name, Action<ModuleDefinition> change)
    {
        using var m = Read(bytes);
        change(m);
        Check(GameContract.Validate(new CecilView(m)).Count > 0, name);
    }
    private static MethodDefinition Method(ModuleDefinition m, string owner, string name)
        => m.GetType(owner).Methods.Single(x => x.Name == name);
    private static void RemoveCall(ModuleDefinition m, string owner, string method, string call)
    {
        var i = Method(m, owner, method).Body.Instructions.First(x => x.Operand is MethodReference c && c.Name == call);
        i.OpCode = OpCodes.Nop; i.Operand = null;
    }
    public static void Main(string[] args)
    {
        if (args[0] == "--capture-reviewed-patterns")
        {
            string[] methods = { "UpdateTransform", "CheckForCollision", "RestoreStateIfCollision", "StartMoving", "StopMovement", "Propagate",
                "ApplyTransform", "ForceState", "ComputeStateFromAngle", "SetJobData" };
            var patterns = new Dictionary<string, HashSet<string>>();
            foreach (string path in args.Skip(1).Take(args.Length - 2))
            {
                using var module = Read(File.ReadAllBytes(path));
                foreach (string name in methods)
                {
                    string owner = methods.Take(6).Contains(name) ? GameContract.Children : GameContract.Rotor;
                    string key = owner + "::" + name;
                    if (!patterns.ContainsKey(key)) patterns[key] = new HashSet<string>();
                    var reviewed = Method(module, owner, name);
                    if (reviewed.Body.ExceptionHandlers.Count != 0) throw new Exception("Exception regions require explicit review; capture is restricted to EH-free lifecycle methods");
                    patterns[key].Add(CecilView.Flow(reviewed));
                }
            }
            string entries = string.Join(",\n", patterns.Select(p => "            { " + JsonSerializer.Serialize(p.Key) + ", new[] { "
                + string.Join(", ", p.Value.Select(s => "@\"" + s.Replace("\"", "\"\"") + "\"")) + " } }"));
            File.WriteAllText(args.Last(), "// Reviewed feature-level IL contracts. No build, assembly or metadata-token identity.\nusing System.Collections.Generic;\nnamespace InfiniteRotator { internal static class NativeLifecyclePatterns {\n        internal static readonly Dictionary<string, string[]> Expected = new Dictionary<string, string[]> {\n" + entries + "\n        };\n} }\n");
            Console.WriteLine("Captured " + patterns.Count + " reviewed lifecycle contracts");
            return;
        }
        var files = args.Take(args.Length - 1).ToArray();
        var reports = new List<object>();
        foreach (string path in files)
        {
            using var dependencies = new DefaultAssemblyResolver();
            dependencies.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            resolver = dependencies;
            var bytes = File.ReadAllBytes(path);
            using var original = Read(bytes);
            var failures = GameContract.Validate(new CecilView(original));
            Console.WriteLine(path + "\n" + string.Join("\n", failures));
            Check(failures.Count == 0, "Actual engine contracts: " + path);
            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            using (var changed = Read(bytes))
            {
                changed.Mvid = Guid.NewGuid();
                changed.Assembly.Name.Version = new Version(99, 0);
                changed.Assembly.Name.Name += ".identity-test";
                using var serialized = new MemoryStream();
                changed.Write(serialized);
                var changedBytes = serialized.ToArray();
                Check(Convert.ToHexString(SHA256.HashData(changedBytes)) != hash, "Identity mutation changes actual assembly bytes");
                if (path == files[0]) File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(args.Last()), "engine-identity-fixture.dll"), changedBytes);
                using var reread = Read(changedBytes);
                Check(GameContract.Validate(new CecilView(reread)).Count == 0, "Changed MVID/version/hash accepted with unchanged contracts");
                reread.Types.Add(new TypeDefinition("Unrelated", "ExtraFeature", TypeAttributes.Public, reread.TypeSystem.Object));
                Check(GameContract.Validate(new CecilView(reread)).Count == 0, "Unrelated new type accepted");
            }
            Reject(bytes, "Changed private running field rejected", m => m.GetType(GameContract.Children).Fields.Single(f => f.Name == "isRunning").FieldType = m.TypeSystem.Int32);
            Reject(bytes, "Missing rollback field rejected", m => m.GetType(GameContract.Children).Fields.Single(f => f.Name == "prevState").Name = "renamed");
            Reject(bytes, "Static lifecycle field rejected", m => m.GetType(GameContract.Children).Fields.Single(f => f.Name == "isRunning").IsStatic = true);
            Reject(bytes, "Readonly rollback field rejected", m => m.GetType(GameContract.Children).Fields.Single(f => f.Name == "prevState").IsInitOnly = true);
            Reject(bytes, "Changed update signature rejected", m => Method(m, GameContract.Children, "UpdateTransform").Parameters.Add(new ParameterDefinition(m.TypeSystem.Boolean)));
            Reject(bytes, "Jump bypassing collision checks rejected", m =>
            {
                var body = Method(m, GameContract.Children, "UpdateTransform").Body;
                var afterRollback = body.Instructions.Single(i => i.Operand is MethodReference c && c.Name == "RestoreStateIfCollision").Next;
                body.GetILProcessor().InsertBefore(body.Instructions[0], Instruction.Create(OpCodes.Br, afterRollback));
            });
            Reject(bytes, "Same-named call on wrong owner rejected", m =>
            {
                var body = Method(m, GameContract.Children, "UpdateTransform").Body;
                var call = body.Instructions.First(i => i.Operand is MethodReference c && c.Name == "CheckForCollision");
                call.Operand = new MethodReference("CheckForCollision", m.TypeSystem.Void, new TypeReference("Unrelated", "WrongOwner", m, m)) { HasThis = true };
            });
            Reject(bytes, "Function pointer cannot masquerade as collision call", m =>
                Method(m, GameContract.Children, "UpdateTransform").Body.Instructions.First(i => i.Operand is MethodReference c && c.Name == "CheckForCollision").OpCode = OpCodes.Ldftn);
            Reject(bytes, "Added exception region with unchanged IL rejected", m =>
            {
                var body = Method(m, GameContract.Children, "UpdateTransform").Body;
                body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally) { TryStart = body.Instructions[0],
                    TryEnd = body.Instructions.Last(), HandlerStart = body.Instructions.Last() });
            });
            Reject(bytes, "Sealed rotor type rejected", m => m.GetType(GameContract.Rotor).IsSealed = true);
            Reject(bytes, "Sealed stock top base rejected", m => m.GetType("VoxelEngine.BrickPipe").IsSealed = true);
            Reject(bytes, "Final resource override rejected", m => Method(m, "VoxelEngine.Brick", "LoadResourcesRuntime").IsFinal = true);
            foreach (string name in new[] { "OnInitialize", "InitChildrenTransform", "Activate", "OnGenericParamsUpdated", "GetMoveSpeed", "get_OverTextIndirect" })
                Reject(bytes, "Final override rejected: " + name, m => Method(m, GameContract.Rotor, name).IsFinal = true);
            foreach (string name in new[] { "ForceState", "get_saveData", "set_saveData" })
                Reject(bytes, "Inaccessible direct call rejected: " + name, m => Method(m, GameContract.Rotor, name).IsPrivate = true);
            Reject(bytes, "Inaccessible SetStateFromShipyard rejected", m => Method(m, GameContract.Children, "SetStateFromShipyard").IsPrivate = true);
            Reject(bytes, "Missing collision check rejected", m => RemoveCall(m, GameContract.Children, "UpdateTransform", "CheckForCollision"));
            Reject(bytes, "Missing native rollback rejected", m => RemoveCall(m, GameContract.Children, "UpdateTransform", "RestoreStateIfCollision"));
            Reject(bytes, "Missing state buffer publication rejected", m => RemoveCall(m, GameContract.Children, "UpdateTransform", "Set"));
            Reject(bytes, "Missing query allocation rejected", m => RemoveCall(m, GameContract.Children, "StartMoving", "SetQuery"));
            Reject(bytes, "Missing query release rejected", m => RemoveCall(m, GameContract.Children, "StopMovement", "ReleaseId"));
            Reject(bytes, "Missing movement removal rejected", m => RemoveCall(m, GameContract.Children, "StopMovement", "Remove"));
            Reject(bytes, "Missing worker data publication rejected", m => RemoveCall(m, GameContract.Rotor, "SetJobData", "set_Item"));
            Reject(bytes, "Changed native save version rejected", m => m.GetType(GameContract.Rotor).Fields.Single(f => f.Name == "CURRENT_VERSION").Constant = 3);
            Reject(bytes, "Changed native save key rejected", m => m.GetType(GameContract.Rotor + "/RotatorSaveData").Properties.Single(p => p.Name == "velocity")
                .CustomAttributes.Single(a => a.AttributeType.FullName == "MessagePack.KeyAttribute").ConstructorArguments[0] = new CustomAttributeArgument(m.TypeSystem.Int32, 99));
            Reject(bytes, "Changed material patch signature rejected", m => Method(m, "VoxelEngine.VoxelMaterial", "CreateFaceIDArrayRuntime").ReturnType = m.TypeSystem.Void);
            if (path == files[0])
            {
                using var broken = Read(bytes);
                RemoveCall(broken, GameContract.Children, "StopMovement", "ReleaseId");
                broken.Write(Path.Combine(Path.GetDirectoryName(args.Last()), "engine-missing-release-fixture.dll"));
            }
            var releases = original.Types.SelectMany(Types).SelectMany(t =>
                t.Fields.Where(f => f.HasConstant && f.Constant is string s && s.Contains("26w"))
                    .Select(f => t.FullName + "." + f.Name + "=" + f.Constant)
                    .Concat(t.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions
                        .Where(i => i.Operand is string s && s.Contains("26w"))
                        .Select(i => t.FullName + "." + m.Name + "=" + i.Operand)))).Distinct().ToArray();
            Console.WriteLine("Release literals: " + string.Join("; ", releases));
            reports.Add(new { path, sha256 = hash, mvid = original.Mvid, contracts = "passed", releases });
        }
        File.WriteAllText(args.Last(), JsonSerializer.Serialize(new { checks, passed, engines = reports }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS " + checks + " compatibility checks");
    }
}
