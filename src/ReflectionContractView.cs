using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Globalization;

namespace InfiniteRotator
{
    internal sealed class ReflectionContractView : IContractView
    {
        private readonly Assembly engine;
        private readonly Dictionary<string, ContractType> cache = new Dictionary<string, ContractType>();
        private static readonly Dictionary<short, OpCode> opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
        internal ReflectionContractView(Assembly assembly) { engine = assembly; }
        private static string Name(Type t) => (t.FullName ?? t.Name).Replace('+', '/');
        public ContractType Read(string name)
        {
            ContractType result;
            if (cache.TryGetValue(name, out result)) return result;
            var type = engine.GetType(name.Replace('/', '+'), false);
            if (type == null) { cache[name] = null; return null; }
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            result = new ContractType { Name = name, BaseType = type.BaseType == null ? null : Name(type.BaseType), Sealed = type.IsSealed };
            cache[name] = result;
            foreach (var f in type.GetFields(flags).Where(f => GameContract.NeedsField(name, f.Name))) result.Fields[f.Name] = new ContractField
                { Type = Name(f.FieldType), Static = f.IsStatic, ReadOnly = f.IsInitOnly || f.IsLiteral,
                    Constant = f.IsLiteral ? f.GetRawConstantValue() : null };
            foreach (var p in name == GameContract.Rotor + "/RotatorSaveData" ? type.GetProperties(flags) : new PropertyInfo[0])
                foreach (var a in p.GetCustomAttributesData())
                    if (a.AttributeType.FullName == "MessagePack.KeyAttribute" && a.ConstructorArguments.Count == 1)
                        result.SaveKeys[a.ConstructorArguments[0].Value.ToString()] = p.Name + ":" + Name(p.PropertyType);
            foreach (var m in type.GetMethods(flags).Where(m => GameContract.RequiredMethods.Contains(m.Name)))
            {
                var shape = new ContractMethod { Name = m.Name, Returns = Name(m.ReturnType), Static = m.IsStatic,
                    Virtual = m.IsVirtual, Final = m.IsFinal, Accessible = m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
                    Parameters = m.GetParameters().Select(p => Name(p.ParameterType)).ToArray() };
                var body = NativeLifecyclePatterns.Expected.ContainsKey(name + "::" + m.Name) ? m.GetMethodBody() : null;
                if (body != null) shape.Flow = "locals=" + string.Join(",", body.LocalVariables.Select(v => TypeName(v.LocalType)))
                    + ";init=" + body.InitLocals + ";eh=" + body.ExceptionHandlingClauses.Count + "|" + ReadBody(m, body.GetILAsByteArray(), shape.Operations);
                result.Methods.Add(shape);
            }
            return result;
        }
        private static string TypeName(Type t)
        {
            if (t.IsGenericParameter) return "!" + t.GenericParameterPosition;
            if (t.IsArray) return TypeName(t.GetElementType()) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
            if (t.IsByRef) return TypeName(t.GetElementType()) + "&";
            if (t.IsPointer) return TypeName(t.GetElementType()) + "*";
            if (t.IsGenericType) return Name(t.GetGenericTypeDefinition()) + "<" + string.Join(",", t.GetGenericArguments().Select(TypeName)) + ">";
            return Name(t);
        }
        private static string MemberName(MemberInfo member)
        {
            string owner = TypeName(member.DeclaringType);
            if (member is MethodBase)
            {
                var m = (MethodBase)member;
                if (m.DeclaringType.IsGenericType)
                {
                    var definition = m.DeclaringType.GetGenericTypeDefinition();
                    m = m.IsConstructor ? (MethodBase)definition.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Single(x => x.MetadataToken == m.MetadataToken)
                        : definition.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.MetadataToken == m.MetadataToken);
                }
                var info = m as MethodInfo;
                if (info != null && info.IsGenericMethod) m = info.GetGenericMethodDefinition();
                int genericCount = m.IsGenericMethod ? m.GetGenericArguments().Length : 0;
                return owner + "::" + m.Name + (genericCount == 0 ? "" : "`" + genericCount)
                    + "(" + string.Join(",", m.GetParameters().Select(p => TypeName(p.ParameterType)))
                    + ")->" + (m is MethodInfo ? TypeName(((MethodInfo)m).ReturnType) : "System.Void");
            }
            var field = (FieldInfo)member;
            if (field.DeclaringType.IsGenericType)
                field = field.DeclaringType.GetGenericTypeDefinition().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Single(f => f.MetadataToken == member.MetadataToken);
            return owner + "::" + field.Name + ":" + TypeName(field.FieldType);
        }
        private static string ReadBody(MethodInfo method, byte[] bytes, List<string> result)
        {
            int at = 0;
            var offsets = new List<int>();
            var instructions = new List<string>();
            var branches = new Dictionary<int, int[]>();
            while (at < bytes.Length)
            {
                offsets.Add(at);
                short value = bytes[at++];
                if (value == 0xfe) value = (short)(0xfe00 | bytes[at++]);
                OpCode op;
                if (!opcodes.TryGetValue(value, out op)) throw new InvalidOperationException("Unknown native IL opcode");
                int count;
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: count = 0; break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: count = 1; break;
                    case OperandType.InlineVar: count = 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: count = 8; break;
                    case OperandType.InlineSwitch: count = checked(4 + 4 * BitConverter.ToInt32(bytes, at)); break;
                    default: count = 4; break;
                }
                if (count < 0 || at + count > bytes.Length) throw new InvalidOperationException("Malformed native IL");
                string operand = "";
                if (op.OperandType == OperandType.InlineMethod || op.OperandType == OperandType.InlineField)
                {
                    var member = method.Module.ResolveMember(BitConverter.ToInt32(bytes, at),
                        method.DeclaringType.GetGenericArguments(), method.GetGenericArguments());
                    if (op.OperandType == OperandType.InlineField) result.Add("field:" + member.Name);
                    else if (op == OpCodes.Call || op == OpCodes.Callvirt) result.Add("call:" + member.Name);
                    operand = MemberName(member);
                }
                else if (op.OperandType == OperandType.InlineBrTarget)
                    branches[instructions.Count] = new[] { at + count + BitConverter.ToInt32(bytes, at) };
                else if (op.OperandType == OperandType.ShortInlineBrTarget)
                    branches[instructions.Count] = new[] { at + count + (sbyte)bytes[at] };
                else if (op.OperandType == OperandType.InlineSwitch)
                    branches[instructions.Count] = Enumerable.Range(0, BitConverter.ToInt32(bytes, at))
                        .Select(i => at + count + BitConverter.ToInt32(bytes, at + 4 + i * 4)).ToArray();
                else if (op.OperandType == OperandType.ShortInlineR) operand = BitConverter.ToSingle(bytes, at).ToString("R", CultureInfo.InvariantCulture);
                else if (op.OperandType == OperandType.InlineR) operand = BitConverter.ToDouble(bytes, at).ToString("R", CultureInfo.InvariantCulture);
                else if (op.OperandType == OperandType.InlineI8) operand = BitConverter.ToInt64(bytes, at).ToString(CultureInfo.InvariantCulture);
                else if (op.OperandType == OperandType.InlineI) operand = BitConverter.ToInt32(bytes, at).ToString(CultureInfo.InvariantCulture);
                else if (op.OperandType == OperandType.ShortInlineI) operand = ((sbyte)bytes[at]).ToString(CultureInfo.InvariantCulture);
                else if (op.OperandType == OperandType.InlineVar) operand = BitConverter.ToUInt16(bytes, at).ToString(CultureInfo.InvariantCulture);
                else if (op.OperandType == OperandType.ShortInlineVar) operand = bytes[at].ToString(CultureInfo.InvariantCulture);
                else if (op.OperandType == OperandType.InlineType) operand = TypeName(method.Module.ResolveType(BitConverter.ToInt32(bytes, at)));
                else if (op.OperandType == OperandType.InlineString) operand = method.Module.ResolveString(BitConverter.ToInt32(bytes, at));
                else if (count != 0) operand = "unsupported-operand";
                instructions.Add(op.Name + " " + operand);
                at += count;
            }
            foreach (var branch in branches)
                instructions[branch.Key] += string.Join(",", branch.Value.Select(target => offsets.IndexOf(target).ToString(CultureInfo.InvariantCulture)));
            return string.Join("|", instructions);
        }
    }
}
