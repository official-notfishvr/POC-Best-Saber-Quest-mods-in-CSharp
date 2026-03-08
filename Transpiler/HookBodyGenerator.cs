using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Transpiler;

internal sealed class HookBodyGenerator
{
    private readonly Dictionary<int, string> _localNames;
    private readonly Dictionary<Instruction, int> _instructionIndices;
    private readonly Dictionary<string, ConfigValueInfo> _configByGetter;
    private readonly Dictionary<string, ConfigValueInfo> _configBySetter;
    private readonly Dictionary<string, ConfigValueInfo> _configByField;
    private readonly HashSet<int> _declaredLocals = new();
    private readonly Stack<CppValue> _stack = new();
    private readonly TypeMetadataIndex _metadataIndex;
    private readonly CppTypeSystem _typeSystem;
    private readonly MethodDefinition _method;
    private readonly IList<Instruction> _instructions;

    public HookBodyGenerator(HookInfo hook, CppTypeSystem typeSystem, IEnumerable<ConfigValueInfo> configValues, TypeMetadataIndex metadataIndex)
    {
        Hook = hook;
        _typeSystem = typeSystem;
        _metadataIndex = metadataIndex;
        _method = hook.Method;
        _instructions = _method.Body != null ? _method.Body.Instructions : Array.Empty<Instruction>();
        _localNames = BuildLocalNameMap(_method);
        _instructionIndices = _instructions.Select((instruction, index) => (instruction, index)).ToDictionary(item => item.instruction, item => item.index);
        _configByGetter = configValues.ToDictionary(config => $"get_{config.Name}", config => config, StringComparer.Ordinal);
        _configBySetter = configValues.ToDictionary(config => $"set_{config.Name}", config => config, StringComparer.Ordinal);
        _configByField = configValues.ToDictionary(config => config.Name, config => config, StringComparer.Ordinal);
    }

    public HookInfo Hook { get; }
    public List<string> Lines { get; } = new();
    public HashSet<string> RequiredIncludes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Generate()
    {
        if (!_method.HasBody || _method.Body == null)
            return;

        GenerateRange(0, _instructions.Count, 0);

        while (Lines.Count > 0 && string.IsNullOrWhiteSpace(Lines[^1]))
            Lines.RemoveAt(Lines.Count - 1);

        if (_method.ReturnType.FullName == "System.Void" && Lines.Count > 0 && Lines[^1].Trim() == "return;")
            Lines.RemoveAt(Lines.Count - 1);
    }

    private void GenerateRange(int startIndex, int endIndex, int indentLevel)
    {
        for (var index = startIndex; index < endIndex; index++)
        {
            var instruction = _instructions[index];

            if (TryEmitStructuredIfFromLocalTemp(index, endIndex, indentLevel, out var consumedUntil))
            {
                index = consumedUntil - 1;
                continue;
            }

            if (TryEmitStructuredIf(instruction, index, endIndex, indentLevel, out consumedUntil))
            {
                index = consumedUntil - 1;
                continue;
            }

            EmitInstruction(instruction, indentLevel);
        }
    }

    private bool TryEmitStructuredIfFromLocalTemp(int index, int endIndex, int indentLevel, out int consumedUntil)
    {
        consumedUntil = index;
        if (index + 2 >= endIndex)
            return false;

        if (!TryGetLocalIndex(_instructions[index], out var storedLocalIndex))
            return false;

        var next = _instructions[index + 1];
        if (!TryGetLoadedLocalIndex(next, out var loadedLocalIndex) || loadedLocalIndex != storedLocalIndex)
            return false;

        var branch = _instructions[index + 2];
        if (branch.OpCode.Code is not (Code.Brfalse or Code.Brfalse_S or Code.Brtrue or Code.Brtrue_S))
            return false;

        var targetInstruction = (Instruction)branch.Operand;
        if (!_instructionIndices.TryGetValue(targetInstruction, out var targetIndex) || targetIndex <= index + 2 || targetIndex > endIndex)
            return false;

        var condition = Pop().Code;
        EmitStructuredIf(condition, branch.OpCode.Code, index + 3, targetIndex, indentLevel);
        consumedUntil = targetIndex;
        return true;
    }

    private bool TryEmitStructuredIf(Instruction instruction, int index, int endIndex, int indentLevel, out int consumedUntil)
    {
        consumedUntil = index;
        switch (instruction.OpCode.Code)
        {
            case Code.Brfalse:
            case Code.Brfalse_S:
            case Code.Brtrue:
            case Code.Brtrue_S:
            {
                var targetInstruction = (Instruction)instruction.Operand;
                if (!_instructionIndices.TryGetValue(targetInstruction, out var targetIndex) || targetIndex <= index || targetIndex > endIndex)
                    return false;

                var condition = Pop().Code;
                EmitStructuredIf(condition, instruction.OpCode.Code, index + 1, targetIndex, indentLevel);
                consumedUntil = targetIndex;
                return true;
            }
            case Code.Beq:
            case Code.Beq_S:
            case Code.Bne_Un:
            case Code.Bne_Un_S:
            case Code.Bge:
            case Code.Bge_S:
            case Code.Bge_Un:
            case Code.Bge_Un_S:
            case Code.Bgt:
            case Code.Bgt_S:
            case Code.Bgt_Un:
            case Code.Bgt_Un_S:
            case Code.Ble:
            case Code.Ble_S:
            case Code.Ble_Un:
            case Code.Ble_Un_S:
            case Code.Blt:
            case Code.Blt_S:
            case Code.Blt_Un:
            case Code.Blt_Un_S:
            {
                var targetInstruction = (Instruction)instruction.Operand;
                if (!_instructionIndices.TryGetValue(targetInstruction, out var targetIndex) || targetIndex <= index || targetIndex > endIndex)
                    return false;

                var right = Pop();
                var left = Pop();
                var condition = BuildBodyConditionForCompareBranch(instruction.OpCode.Code, left.Code, right.Code);
                EmitStructuredIf(condition, Code.Brfalse, index + 1, targetIndex, indentLevel);
                consumedUntil = targetIndex;
                return true;
            }
            default:
                return false;
        }
    }

    private void EmitStructuredIf(string condition, Code branchCode, int bodyStartIndex, int bodyEndIndex, int indentLevel)
    {
        var positiveCondition = branchCode is Code.Brfalse or Code.Brfalse_S ? condition : $"!({condition})";

        AppendLine(indentLevel, $"if ({positiveCondition}) {{");
        GenerateRange(bodyStartIndex, bodyEndIndex, indentLevel + 1);
        AppendLine(indentLevel, "}");
    }

    private void EmitInstruction(Instruction instruction, int indentLevel)
    {
        switch (instruction.OpCode.Code)
        {
            case Code.Nop:
                return;
            case Code.Ldarg_0:
                PushArgument(0);
                return;
            case Code.Ldarg_1:
                PushArgument(1);
                return;
            case Code.Ldarg_2:
                PushArgument(2);
                return;
            case Code.Ldarg_3:
                PushArgument(3);
                return;
            case Code.Ldarg:
            case Code.Ldarg_S:
                PushArgument(((ParameterDefinition)instruction.Operand).Index);
                return;
            case Code.Ldloc_0:
            case Code.Ldloc_1:
            case Code.Ldloc_2:
            case Code.Ldloc_3:
                PushLocal((int)instruction.OpCode.Code - (int)Code.Ldloc_0);
                return;
            case Code.Ldloc:
            case Code.Ldloc_S:
                PushLocal(((VariableDefinition)instruction.Operand).Index);
                return;
            case Code.Stloc_0:
            case Code.Stloc_1:
            case Code.Stloc_2:
            case Code.Stloc_3:
                StoreLocal((int)instruction.OpCode.Code - (int)Code.Stloc_0, indentLevel);
                return;
            case Code.Stloc:
            case Code.Stloc_S:
                StoreLocal(((VariableDefinition)instruction.Operand).Index, indentLevel);
                return;
            case Code.Ldc_I4_M1:
                _stack.Push(new CppValue { Code = "-1", Type = _method.Module.TypeSystem.Int32 });
                return;
            case Code.Ldc_I4_0:
            case Code.Ldc_I4_1:
            case Code.Ldc_I4_2:
            case Code.Ldc_I4_3:
            case Code.Ldc_I4_4:
            case Code.Ldc_I4_5:
            case Code.Ldc_I4_6:
            case Code.Ldc_I4_7:
            case Code.Ldc_I4_8:
                _stack.Push(new CppValue { Code = ((int)instruction.OpCode.Code - (int)Code.Ldc_I4_0).ToString(CultureInfo.InvariantCulture), Type = _method.Module.TypeSystem.Int32 });
                return;
            case Code.Ldc_I4:
            case Code.Ldc_I4_S:
                _stack.Push(new CppValue { Code = Convert.ToInt32(instruction.Operand, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture), Type = _method.Module.TypeSystem.Int32 });
                return;
            case Code.Ldc_I8:
                _stack.Push(new CppValue { Code = Convert.ToInt64(instruction.Operand, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture), Type = _method.Module.TypeSystem.Int64 });
                return;
            case Code.Ldc_R4:
                _stack.Push(new CppValue { Code = Convert.ToSingle(instruction.Operand, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture), Type = _method.Module.TypeSystem.Single });
                return;
            case Code.Ldc_R8:
                _stack.Push(new CppValue { Code = Convert.ToDouble(instruction.Operand, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture), Type = _method.Module.TypeSystem.Double });
                return;
            case Code.Ldstr:
                _stack.Push(new CppValue { Code = CppLiteral.String((string)instruction.Operand), Type = _method.Module.TypeSystem.String });
                return;
            case Code.Ldnull:
                _stack.Push(new CppValue { Code = "nullptr", Type = _method.Module.TypeSystem.Object });
                return;
            case Code.Ldfld:
                LoadField((FieldReference)instruction.Operand, isStatic: false);
                return;
            case Code.Ldsfld:
                LoadField((FieldReference)instruction.Operand, isStatic: true);
                return;
            case Code.Stfld:
                StoreField((FieldReference)instruction.Operand, isStatic: false, indentLevel);
                return;
            case Code.Stsfld:
                StoreField((FieldReference)instruction.Operand, isStatic: true, indentLevel);
                return;
            case Code.Call:
            case Code.Callvirt:
                EmitCall((MethodReference)instruction.Operand, indentLevel);
                return;
            case Code.Br:
            case Code.Br_S:
            {
                var target = (Instruction)instruction.Operand;
                if (_instructionIndices.TryGetValue(target, out var targetIndex) && targetIndex == _instructions.Count - 1 && _instructions[targetIndex].OpCode.Code == Code.Ret)
                    return;
                throw new NotSupportedException($"Unsupported non-structured branch in {_method.FullName}");
            }
            case Code.Ret:
                EmitReturn(indentLevel);
                return;
            case Code.Pop:
            {
                var value = Pop();
                if (value.HasSideEffects)
                    AppendLine(indentLevel, $"{value.Code};");
                return;
            }
            case Code.Dup:
            {
                var value = Pop();
                _stack.Push(value);
                _stack.Push(value);
                return;
            }
            case Code.Ceq:
                EmitComparison("==");
                return;
            case Code.Cgt:
                EmitComparison(">");
                return;
            case Code.Clt:
                EmitComparison("<");
                return;
            case Code.Add:
            case Code.Add_Ovf:
            case Code.Add_Ovf_Un:
                EmitBinary("+");
                return;
            case Code.Sub:
            case Code.Sub_Ovf:
            case Code.Sub_Ovf_Un:
                EmitBinary("-");
                return;
            case Code.Mul:
            case Code.Mul_Ovf:
            case Code.Mul_Ovf_Un:
                EmitBinary("*");
                return;
            case Code.Div:
            case Code.Div_Un:
                EmitBinary("/");
                return;
            case Code.Rem:
            case Code.Rem_Un:
                EmitBinary("%");
                return;
            case Code.And:
                EmitBinary("&");
                return;
            case Code.Or:
                EmitBinary("|");
                return;
            case Code.Xor:
                EmitBinary("^");
                return;
            case Code.Neg:
                EmitUnary("-");
                return;
            case Code.Not:
                EmitUnary("~");
                return;
            case Code.Conv_I1:
            case Code.Conv_I2:
            case Code.Conv_I4:
            case Code.Conv_I8:
            case Code.Conv_U1:
            case Code.Conv_U2:
            case Code.Conv_U4:
            case Code.Conv_U8:
            case Code.Conv_R4:
            case Code.Conv_R8:
                EmitConversion(instruction.OpCode.Code);
                return;
            default:
                throw new NotSupportedException($"Unsupported IL opcode {instruction.OpCode.Code} in {_method.FullName}");
        }
    }

    private void PushArgument(int parameterIndex)
    {
        var parameter = _method.Parameters[parameterIndex];
        RequiredInclude(parameter.ParameterType);
        _stack.Push(new CppValue { Code = CppName.Sanitize(parameter.Name), Type = parameter.ParameterType });
    }

    private void PushLocal(int index)
    {
        var variable = _method.Body!.Variables[index];
        RequiredInclude(variable.VariableType);
        _stack.Push(new CppValue { Code = GetLocalName(index), Type = variable.VariableType });
    }

    private void StoreLocal(int index, int indentLevel)
    {
        var value = Pop();
        var name = GetLocalName(index);
        var variable = _method.Body!.Variables[index];

        if (_declaredLocals.Add(index))
        {
            var declaredType = value.PreferAutoDeclaration ? "auto" : _typeSystem.MapType(variable.VariableType);
            AppendLine(indentLevel, $"{declaredType} {name} = {value.Code};");
            return;
        }

        AppendLine(indentLevel, $"{name} = {value.Code};");
    }

    private void LoadField(FieldReference field, bool isStatic)
    {
        RequiredInclude(field.FieldType);
        RequiredInclude(field.DeclaringType);

        if (isStatic && _configByField.TryGetValue(field.Name, out var config))
        {
            _stack.Push(new CppValue { Code = config.Name, Type = config.Type });
            return;
        }

        if (isStatic)
        {
            var declaringType = $"{_typeSystem.MapNamespace(field.DeclaringType.Namespace)}::{_typeSystem.ComposeTypeName(field.DeclaringType)}";
            _stack.Push(new CppValue { Code = $"{declaringType}::{field.Name}", Type = field.FieldType });
            return;
        }

        var target = Pop();
        _stack.Push(
            new CppValue
            {
                Code = $"{target.Code}->{field.Name}",
                Type = field.FieldType,
                PreferAutoDeclaration = true,
            }
        );
    }

    private void StoreField(FieldReference field, bool isStatic, int indentLevel)
    {
        var value = Pop();

        if (isStatic && _configByField.TryGetValue(field.Name, out var config))
        {
            AppendLine(indentLevel, $"{config.Name} = {value.Code};");
            return;
        }

        if (isStatic)
        {
            var declaringType = $"{_typeSystem.MapNamespace(field.DeclaringType.Namespace)}::{_typeSystem.ComposeTypeName(field.DeclaringType)}";
            AppendLine(indentLevel, $"{declaringType}::{field.Name} = {value.Code};");
            return;
        }

        var target = Pop();
        AppendLine(indentLevel, $"{target.Code}->{field.Name} = {value.Code};");
    }

    private void EmitCall(MethodReference method, int indentLevel)
    {
        var args = new List<CppValue>(method.Parameters.Count);
        for (var i = 0; i < method.Parameters.Count; i++)
            args.Insert(0, Pop());

        CppValue? instance = null;
        if (method.HasThis)
            instance = Pop();

        foreach (var argument in args)
            RequiredInclude(argument.Type);

        if (IsConfigAccessor(method, out var configAccessor))
        {
            if (method.Name.StartsWith("get_", StringComparison.Ordinal))
            {
                _stack.Push(new CppValue { Code = configAccessor.Name, Type = configAccessor.Type });
                return;
            }

            AppendLine(indentLevel, $"{configAccessor.Name} = {args[0].Code};");
            return;
        }

        if (method.DeclaringType.FullName == Hook.Method.DeclaringType.FullName && method.Name == Hook.Method.Name)
        {
            var originalCall = $"{Hook.HookName}({string.Join(", ", args.Select(arg => arg.Code))})";
            if (method.ReturnType.FullName == "System.Void")
                AppendLine(indentLevel, $"{originalCall};");
            else
                _stack.Push(
                    new CppValue
                    {
                        Code = originalCall,
                        Type = method.ReturnType,
                        HasSideEffects = true,
                    }
                );
            return;
        }

        if (method.DeclaringType.FullName == "System.Console" && method.Name is "WriteLine" or "Write")
        {
            AppendLine(indentLevel, $"PaperLogger.info({string.Join(", ", args.Select(arg => arg.Code))});");
            return;
        }

        RequiredInclude(method.ReturnType);
        if (!method.HasThis)
            RequiredInclude(method.DeclaringType);

        var callValue = BuildCallValue(method, instance, args);
        if (method.ReturnType.FullName == "System.Void")
        {
            AppendLine(indentLevel, $"{callValue.Code};");
            return;
        }

        _stack.Push(callValue);
    }

    private CppValue BuildCallValue(MethodReference method, CppValue? instance, IReadOnlyList<CppValue> args)
    {
        var argumentList = string.Join(", ", args.Select(arg => arg.Code));

        if (method.Name.StartsWith("get_", StringComparison.Ordinal) && TryGetPropertyAccessorName(method, out var propertyName) && instance != null)
        {
            return new CppValue
            {
                Code = $"{instance.Code}->{propertyName}",
                Type = method.ReturnType,
                PreferAutoDeclaration = true,
            };
        }

        if (method.Name is "GetComponentInChildren" or "GetComponent" && method is GenericInstanceMethod genericMethod && instance != null)
        {
            var typeArgument = genericMethod.GenericArguments[0];
            RequiredInclude(typeArgument);
            return new CppValue { Code = $"{instance.Code}->{method.Name}<{_typeSystem.MapType(typeArgument)}>({argumentList})", Type = method.ReturnType };
        }

        if (method.Name.StartsWith("get_", StringComparison.Ordinal) || method.Name.StartsWith("set_", StringComparison.Ordinal))
        {
            if (TryGetPropertyAccessorName(method, out var metadataPropertyName) && instance != null)
            {
                if (method.Name.StartsWith("get_", StringComparison.Ordinal))
                {
                    return new CppValue
                    {
                        Code = $"{instance.Code}->{metadataPropertyName}",
                        Type = method.ReturnType,
                        PreferAutoDeclaration = true,
                    };
                }

                return new CppValue
                {
                    Code = $"{instance.Code}->{metadataPropertyName} = {argumentList}",
                    Type = method.ReturnType,
                    HasSideEffects = true,
                };
            }

            var accessorName = NormalizeAccessorName(method.Name);
            return new CppValue
            {
                Code = instance != null ? $"{instance.Code}->{accessorName}({argumentList})" : $"{accessorName}({argumentList})",
                Type = method.ReturnType,
                PreferAutoDeclaration = method.Name.StartsWith("get_", StringComparison.Ordinal),
                HasSideEffects = true,
            };
        }

        if (instance != null)
        {
            return new CppValue
            {
                Code = $"{instance.Code}->{method.Name}({argumentList})",
                Type = method.ReturnType,
                PreferAutoDeclaration = true,
                HasSideEffects = true,
            };
        }

        var declaringType = $"{_typeSystem.MapNamespace(method.DeclaringType.Namespace)}::{_typeSystem.ComposeTypeName(method.DeclaringType)}";
        return new CppValue
        {
            Code = $"{declaringType}::{method.Name}({argumentList})",
            Type = method.ReturnType,
            HasSideEffects = true,
        };
    }

    private static string NormalizeAccessorName(string methodName)
    {
        var prefix = methodName[..4];
        var suffix = methodName[4..];
        if (string.IsNullOrEmpty(suffix))
            return methodName;

        return prefix + char.ToLowerInvariant(suffix[0]) + suffix[1..];
    }

    private bool IsConfigAccessor(MethodReference method, out ConfigValueInfo config)
    {
        if (_configByGetter.TryGetValue(method.Name, out config!))
            return true;

        return _configBySetter.TryGetValue(method.Name, out config!);
    }

    private bool TryGetPropertyAccessorName(MethodReference method, out string propertyName)
    {
        propertyName = "";
        if (!method.Name.StartsWith("get_", StringComparison.Ordinal) && !method.Name.StartsWith("set_", StringComparison.Ordinal))
            return false;

        try
        {
            var resolvedMethod = method.Resolve();
            for (var declaringType = resolvedMethod?.DeclaringType ?? method.DeclaringType.Resolve(); declaringType != null; declaringType = declaringType.BaseType?.Resolve())
            {
                var property = declaringType.Properties.FirstOrDefault(prop => prop.GetMethod?.Name == method.Name || prop.SetMethod?.Name == method.Name);
                if (property != null)
                {
                    propertyName = property.Name;
                    return true;
                }
            }
        }
        catch { }

        if (propertyName.Length > 0)
            return true;

        propertyName = _metadataIndex.ResolvePropertyName(method.DeclaringType.FullName, method.Name) ?? "";
        return propertyName.Length > 0;
    }

    private void EmitReturn(int indentLevel)
    {
        if (_method.ReturnType.FullName == "System.Void")
        {
            AppendLine(indentLevel, "return;");
            return;
        }

        AppendLine(indentLevel, $"return {Pop().Code};");
    }

    private void EmitBinary(string op)
    {
        var right = Pop();
        var left = Pop();
        _stack.Push(new CppValue { Code = $"({left.Code} {op} {right.Code})", Type = left.Type ?? right.Type });
    }

    private void EmitComparison(string op)
    {
        var right = Pop();
        var left = Pop();
        _stack.Push(new CppValue { Code = $"({left.Code} {op} {right.Code})", Type = _method.Module.TypeSystem.Boolean });
    }

    private void EmitUnary(string op)
    {
        var operand = Pop();
        _stack.Push(new CppValue { Code = $"({op}{operand.Code})", Type = operand.Type });
    }

    private void EmitConversion(Code opcode)
    {
        var operand = Pop();
        var targetType = opcode switch
        {
            Code.Conv_I1 => "int8_t",
            Code.Conv_I2 => "int16_t",
            Code.Conv_I4 => "int32_t",
            Code.Conv_I8 => "int64_t",
            Code.Conv_U1 => "uint8_t",
            Code.Conv_U2 => "uint16_t",
            Code.Conv_U4 => "uint32_t",
            Code.Conv_U8 => "uint64_t",
            Code.Conv_R4 => "float",
            Code.Conv_R8 => "double",
            _ => throw new NotSupportedException($"Unsupported conversion opcode {opcode}"),
        };
        _stack.Push(new CppValue { Code = $"static_cast<{targetType}>({operand.Code})", Type = operand.Type });
    }

    private static string BuildBodyConditionForCompareBranch(Code opcode, string left, string right)
    {
        return opcode switch
        {
            Code.Beq or Code.Beq_S => $"({left} != {right})",
            Code.Bne_Un or Code.Bne_Un_S => $"({left} == {right})",
            Code.Bge or Code.Bge_S or Code.Bge_Un or Code.Bge_Un_S => $"({left} < {right})",
            Code.Bgt or Code.Bgt_S or Code.Bgt_Un or Code.Bgt_Un_S => $"({left} <= {right})",
            Code.Ble or Code.Ble_S or Code.Ble_Un or Code.Ble_Un_S => $"({left} > {right})",
            Code.Blt or Code.Blt_S or Code.Blt_Un or Code.Blt_Un_S => $"({left} >= {right})",
            _ => throw new NotSupportedException($"Unsupported compare branch opcode {opcode}"),
        };
    }

    private CppValue Pop()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException($"IL stack underflow while translating {_method.FullName}");

        return _stack.Pop();
    }

    private string GetLocalName(int index)
    {
        return _localNames.TryGetValue(index, out var name) ? name : $"local{index}";
    }

    private void RequiredInclude(TypeReference? type)
    {
        var include = _typeSystem.GetIncludePath(type);
        if (include != null)
            RequiredIncludes.Add(include);
    }

    private void AppendLine(int indentLevel, string line = "")
    {
        Lines.Add($"{new string(' ', indentLevel * 4)}{line}");
    }

    private static bool TryGetLocalIndex(Instruction instruction, out int localIndex)
    {
        switch (instruction.OpCode.Code)
        {
            case Code.Stloc_0:
                localIndex = 0;
                return true;
            case Code.Stloc_1:
                localIndex = 1;
                return true;
            case Code.Stloc_2:
                localIndex = 2;
                return true;
            case Code.Stloc_3:
                localIndex = 3;
                return true;
            case Code.Stloc:
            case Code.Stloc_S:
                localIndex = ((VariableDefinition)instruction.Operand).Index;
                return true;
            default:
                localIndex = -1;
                return false;
        }
    }

    private static bool TryGetLoadedLocalIndex(Instruction instruction, out int localIndex)
    {
        switch (instruction.OpCode.Code)
        {
            case Code.Ldloc_0:
                localIndex = 0;
                return true;
            case Code.Ldloc_1:
                localIndex = 1;
                return true;
            case Code.Ldloc_2:
                localIndex = 2;
                return true;
            case Code.Ldloc_3:
                localIndex = 3;
                return true;
            case Code.Ldloc:
            case Code.Ldloc_S:
                localIndex = ((VariableDefinition)instruction.Operand).Index;
                return true;
            default:
                localIndex = -1;
                return false;
        }
    }

    private static Dictionary<int, string> BuildLocalNameMap(MethodDefinition method)
    {
        var map = new Dictionary<int, string>();
        VisitScope(method.DebugInformation.Scope, map);
        return map;
    }

    private static void VisitScope(ScopeDebugInformation? scope, IDictionary<int, string> map)
    {
        if (scope == null)
            return;

        foreach (var variable in scope.Variables)
        {
            if (!string.IsNullOrWhiteSpace(variable.Name))
                map[variable.Index] = CppName.Sanitize(variable.Name);
        }

        foreach (var nestedScope in scope.Scopes)
            VisitScope(nestedScope, map);
    }
}

internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new();

    public void WriteLine(string line = "")
    {
        _builder.AppendLine(line);
    }

    public override string ToString() => _builder.ToString();
}

internal static class CppLiteral
{
    public static string String(string value)
    {
        return $"il2cpp_utils::newcsstr(\"{Escape(value)}\")";
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
    }
}

internal static class CppName
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "alignas",
        "alignof",
        "and",
        "asm",
        "auto",
        "bool",
        "break",
        "case",
        "catch",
        "char",
        "class",
        "const",
        "constexpr",
        "continue",
        "default",
        "delete",
        "do",
        "double",
        "else",
        "enum",
        "explicit",
        "export",
        "extern",
        "false",
        "float",
        "for",
        "friend",
        "goto",
        "if",
        "inline",
        "int",
        "long",
        "namespace",
        "new",
        "noexcept",
        "nullptr",
        "operator",
        "private",
        "protected",
        "public",
        "register",
        "reinterpret_cast",
        "return",
        "short",
        "signed",
        "sizeof",
        "static",
        "struct",
        "switch",
        "template",
        "this",
        "throw",
        "true",
        "try",
        "typedef",
        "typename",
        "union",
        "unsigned",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    };

    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "value";

        var sanitized = name.Replace(".", "_", StringComparison.Ordinal);
        return Keywords.Contains(sanitized) ? $"{sanitized}_" : sanitized;
    }
}
