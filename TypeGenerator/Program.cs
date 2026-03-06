#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TypeGenerator;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: TypeGenerator <include-folder> <output-folder>");
            Console.WriteLine("Example: TypeGenerator C:\\path\\to\\bs-cordl\\include C:\\output\\Types");
            return;
        }

        var includeFolder = args[0];
        var outputFolder = args[1];

        var generator = new TypeStubGenerator();
        generator.Generate(includeFolder, outputFolder);
    }
}

class TypeStubGenerator
{
    private readonly Dictionary<(string ns, string typeName), TypeData> _types = new();
    private readonly Dictionary<string, TypeData> _fullNames = new();
    private readonly HashSet<(string ns, string parentType, string nestedType, int arity)> _nestedTypes = new();
    private readonly HashSet<string> _generatedMembers = new();

    private static readonly HashSet<string> SystemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Object", "String", "Boolean", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32",
        "Int64", "UInt64", "IntPtr", "UIntPtr", "Char", "Double", "Single", "Decimal",
        "Type", "Array", "Exception", "Delegate", "MulticastDelegate", "Enum", "ValueType",
        "Void", "IEnumerator", "IEnumerable", "ICollection", "IList", "IDictionary",
        "Stream", "Task", "Func", "Action", "CultureInfo", "DateTime", "TimeSpan",
        "Guid", "Math", "Convert", "Activator", "Attribute", "ICloneable", "IComparable",
        "IFormattable", "IConvertible", "IServiceProvider", "IAsyncResult", "AsyncCallback",
        "ISerializable", "RuntimeTypeHandle", "DBNull", "Nullable", "Lazy", "Tuple", "ValueTuple",
        "Span", "ReadOnlySpan", "Memory", "ReadOnlyMemory", "ArraySegment", "Range", "Index",
        "Random", "MathF", "GC", "BitConverter", "Buffer", "BigInteger", "Complex"
    };

    public void Generate(string includeFolder, string outputFolder)
    {
        Console.WriteLine($"Scanning {includeFolder}...");

        foreach (var dir in Directory.GetDirectories(includeFolder))
        {
            var ns = Path.GetFileName(dir);
            if (ns.StartsWith("zzzz__") || ns == "Internal" || ns.Contains("cordl_internals")) continue;
            ProcessNamespace(dir, ns);
        }

        foreach (var kv in _types)
        {
            var full = string.IsNullOrEmpty(kv.Key.ns) ? "global::" + kv.Key.typeName : "global::" + kv.Key.ns + "." + kv.Key.typeName;
            _fullNames[full] = kv.Value;
        }

        foreach (var data in _types.Values)
        {
            if (data.Fields != null) { foreach (var f in data.Fields) MapCppTypeToCs(f.Type, data.Namespace); }
            if (data.Methods != null)
            {
                foreach (var m in data.Methods)
                {
                    MapCppTypeToCs(m.ReturnType, data.Namespace);
                    if (m.Parameters != null) { foreach (var p in m.Parameters) MapCppTypeToCs(p.Type, data.Namespace); }
                }
            }
            if (data.BaseType != null) MapCppTypeToCs(data.BaseType, data.Namespace);
        }

        Console.WriteLine($"Found {_types.Count} types");
        Directory.CreateDirectory(outputFolder);

        var outputFile = Path.Combine(outputFolder, "GeneratedTypes.cs");

        using var writer = new StreamWriter(outputFile, false, new UTF8Encoding(false), 1 << 20);

        WriteHeader(writer);

        var byNs = _types.Keys
            .GroupBy(k => k.ns)
            .OrderBy(g => g.Key);

        foreach (var group in byNs)
        {
            var ns = group.Key;
            var typeNames = group.Select(k => k.typeName).OrderBy(x => x).ToList();

            writer.WriteLine($"namespace {ns}");
            writer.WriteLine("{");

            foreach (var typeName in typeNames)
            {
                try
                {
                    if (_types.TryGetValue((ns, typeName), out var data))
                        GenerateTypeStub(writer, typeName, ns, data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error generating {ns}.{typeName}: {ex.Message}");
                }
            }

            writer.WriteLine("}");
            writer.WriteLine();
        }

        writer.Flush();
        Console.WriteLine($"Generated {outputFile}");
    }

    private void ProcessNamespace(string dir, string ns)
    {
        foreach (var file in Directory.GetFiles(dir, "zzzz__*_def.hpp"))
            ProcessTypeFile(file, ns);

        foreach (var subdir in Directory.GetDirectories(dir))
        {
            var subNs = Path.GetFileName(subdir);
            if (subNs.StartsWith("zzzz__") || subNs == "Internal" || subNs.Contains("cordl_internals")) continue;
            ProcessNamespace(subdir, $"{ns}.{subNs}");
        }
    }

    private void ProcessTypeFile(string filePath, string ns)
    {
        var content = File.ReadAllText(filePath);
        var matches = Regex.Matches(content, @"// CS Name: ([^\r\n]+)\s+(?:template\s*<[^>]*>\s*)?(?://[^\r\n]*\r?\n\s*)*(struct|class|enum)\s+CORDL_TYPE\s+(\w+)");
        
        foreach (Match m in matches)
        {
            var csName = m.Groups[1].Value.Trim();
            var keyword = m.Groups[2].Value;
            var cppName = m.Groups[3].Value;
            
            int start = m.Index;
            var next = matches.Cast<Match>().FirstOrDefault(nm => nm.Index > start);
            int end = next?.Index ?? content.Length;
            var typeContent = content.Substring(start, end - start);
            
            var key = (ns, cppName);
            if (_types.ContainsKey(key)) continue;

            var data = new TypeData { Namespace = ns, TypeName = cppName };
            _types[key] = data;
            
            data.IsValueType = keyword == "struct" || keyword == "enum" || typeContent.Contains("__IL2CPP_IS_VALUE_TYPE = true");
            data.BaseType = ExtractBaseType(typeContent);
            data.Fields = ExtractFields(typeContent);
            data.Methods = ExtractMethods(typeContent);
        }
    }

    private List<FieldInfo> ExtractFields(string content)
    {
        var fields = new List<FieldInfo>();
        var seenNames = new HashSet<string>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("/// @brief Field")) continue;

            var mInfo = Regex.Match(line, @"Field\s+(\w+),\s+offset\s+(0x[0-9a-fA-F]+)");
            if (!mInfo.Success) continue;
            var fName = mInfo.Groups[1].Value;
            var offset = mInfo.Groups[2].Value;
            bool isStatic = offset.Equals("0xffffffff", StringComparison.OrdinalIgnoreCase);

            if (fName.StartsWith("__") || fName.Contains("BackingField")) continue;
            if (!seenNames.Add(fName)) continue;

            var nextLine = lines[i + 1].Trim();
            if (nextLine.Contains("constexpr") || nextLine.Contains("const ")) continue;

            string fType = null;
            var propMatch = Regex.Match(nextLine, @"__declspec\(property\([^)]+\)\)\s+(.+?)\s+" + Regex.Escape(fName));
            if (propMatch.Success)
                fType = propMatch.Groups[1].Value.Trim();
            else
            {
                var fieldMatch = Regex.Match(nextLine, @"^(?:static\s+)?(.+?)\s+" + Regex.Escape(fName) + @"\s*;");
                if (fieldMatch.Success) fType = fieldMatch.Groups[1].Value.Trim();
            }

            if (fType != null)
                fields.Add(new FieldInfo { Type = fType, Name = fName, IsStatic = isStatic });
        }
        return fields;
    }

    private List<MethodInfo> ExtractMethods(string content)
    {
        var methods = new List<MethodInfo>();
        var seenSigs = new HashSet<string>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            var nextLine = lines[i + 1].Trim();

            if (!line.StartsWith("/// @brief Method")) continue;
            if (nextLine.Contains("template <")) continue;

            var m = Regex.Match(nextLine, @"(?:inline\s+|static\s+|virtual\s+)+(.+?)\s+(\w+)\s*\(([^)]*)\)");
            if (!m.Success) continue;

            var methodName = m.Groups[2].Value;
            if (methodName.StartsWith("_") || methodName.Contains("ctor") || methodName.Contains("Finalize")) continue;
            if (methodName == "MoveNext" || methodName == "SetStateMachine") continue;
            if (methodName.StartsWith("get_") || methodName.StartsWith("set_") || methodName.StartsWith("add_") || methodName.StartsWith("remove_") || methodName.StartsWith("getStaticF_") || methodName.StartsWith("setStaticF_")) continue;

            var returnType = m.Groups[1].Value.Trim();
            var parameters = m.Groups[3].Value;

            var sig = methodName + "(" + NormalizeParamSig(parameters) + ")";
            if (!seenSigs.Add(sig)) continue;

            methods.Add(new MethodInfo
            {
                ReturnType = returnType,
                Name = methodName,
                Parameters = ParseParameters(parameters),
                IsStatic = nextLine.Contains("static ")
            });
        }

        return methods;
    }

    private static string NormalizeParamSig(string parameters)
    {
        return Regex.Replace(parameters, @"\s+", "").ToLowerInvariant();
    }

    private List<ParameterInfo> ParseParameters(string parameters)
    {
        var result = new List<ParameterInfo>();
        if (string.IsNullOrWhiteSpace(parameters)) return result;

        int paramIndex = 0;
        foreach (var part in SplitByCommaRespectingBrackets(parameters))
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace <= 0) continue;

            var type = trimmed.Substring(0, lastSpace).Trim();
            var name = trimmed.Substring(lastSpace + 1).Trim();

            if (!Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                name = $"param{paramIndex}";

            paramIndex++;
            result.Add(new ParameterInfo { Type = type, Name = name });
        }

        return result;
    }

    private List<string> SplitByCommaRespectingBrackets(string input)
    {
        var result = new List<string>();
        int depth = 0, sqDepth = 0, start = 0;

        for (int i = 0; i < input.Length; i++)
        {
            switch (input[i])
            {
                case '<': depth++; break;
                case '>': if (depth > 0) depth--; break;
                case '[': sqDepth++; break;
                case ']': if (sqDepth > 0) sqDepth--; break;
                case ',' when depth == 0 && sqDepth == 0:
                    result.Add(input.Substring(start, i - start));
                    start = i + 1;
                    break;
            }
        }

        if (start < input.Length) result.Add(input.Substring(start));
        return result;
    }

    private string ExtractBaseType(string content)
    {
        var match = Regex.Match(content, @"class\s+CORDL_TYPE\s+\w+\s*:\s*public\s+(.+?)\s*\{");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        return null;
    }

    private void GenerateTypeStub(StreamWriter writer, string typeName, string ns, TypeData data)
    {
        var hierarchy = SplitNestingValidated(typeName, ns);
        if (hierarchy.Count == 0) return;

        for (int i = 0; i < hierarchy.Count - 1; i++)
        {
            var (name, arity) = hierarchy[i];
            var (gn, gp) = ConvertToGenericNameWithArity(name, arity);
            var parentFullCpp = string.Join("_", hierarchy.Take(i + 1).Select(p => p.arity > 0 ? $"{p.name}_{p.arity}" : p.name));
            var isStruct = _types.TryGetValue((ns, parentFullCpp), out var pData) && pData.IsValueType;
            writer.WriteLine($"{new string(' ', (i + 1) * 4)}public partial {(isStruct ? "struct" : "class")} {Sanitize(gn)}{gp}");
            writer.WriteLine($"{new string(' ', (i + 1) * 4)}{{");
        }

        var currentIndent = new string(' ', hierarchy.Count * 4);
        var last = hierarchy.Last();
        var (typeNameFinal, tGp) = ConvertToGenericNameWithArity(last.name, last.arity);

        var isInterface = typeNameFinal.Length >= 2
            && typeNameFinal.StartsWith("I")
            && char.IsUpper(typeNameFinal[1])
            && !typeNameFinal.StartsWith("IVR")
            && !typeNameFinal.StartsWith("IL2")
            && !data.IsValueType;

        string baseTypeCs = null;
        if (data.BaseType != null && !data.IsValueType && !isInterface)
        {
            var mapped = MapCppTypeToCs(data.BaseType, ns);
            if (mapped != "object" && mapped != "string" && !mapped.StartsWith("global::System."))
            {
                var currentFull = string.IsNullOrEmpty(ns) ? "global::" + Sanitize(typeNameFinal) : "global::" + ns + "." + Sanitize(typeNameFinal);
                if (mapped != currentFull) baseTypeCs = mapped;
            }
        }

        var keyword = data.IsValueType ? "struct" : (isInterface ? "interface" : "class");
        writer.WriteLine($"{currentIndent}// namespace: {ns}");
        var safeTypeName = Sanitize(typeNameFinal);
        if (baseTypeCs != null)
            writer.WriteLine($"{currentIndent}public partial {keyword} {safeTypeName}{tGp} : {baseTypeCs}");
        else
            writer.WriteLine($"{currentIndent}public partial {keyword} {safeTypeName}{tGp}");

        writer.WriteLine($"{currentIndent}{{");

        var typeSigKey = $"{ns}.{string.Join(".", hierarchy.Select(h => h.name))}";
        var currentFullType = (string.IsNullOrEmpty(ns) ? "global::" : "global::" + ns + ".") + BuildCsPath("", hierarchy);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        seenNames.Add(safeTypeName);

        foreach (var field in data.Fields)
        {
            if (isInterface) continue;
            var csType = MapCppTypeToCs(field.Type, ns);
            var fName = Sanitize(field.Name);
            while (!seenNames.Add(fName)) fName += "_";
            
            if (!_generatedMembers.Add($"{typeSigKey}::F:{fName}:{field.IsStatic}")) continue;

            if (data.IsValueType && !field.IsStatic && (csType == currentFullType || csType.Split('<')[0] == currentFullType.Split('<')[0]))
                csType = "object";

            var staticMod = field.IsStatic ? "static " : "";
            writer.WriteLine($"{currentIndent}    public {staticMod}{csType} {fName};");
        }

        var seenSigs = new HashSet<string>();
        foreach (var method in data.Methods)
        {
            var mName = Sanitize(method.Name);
            var csReturn = MapCppTypeToCs(method.ReturnType, ns);
            var paramTypes = method.Parameters.Select(p => MapCppTypeToCs(p.Type, ns)).ToList();
            var sigKey = $"{mName}|{string.Join(",", paramTypes)}";
            if (!seenSigs.Add(sigKey)) continue;
            if (!_generatedMembers.Add($"{typeSigKey}::M:{sigKey}:{method.IsStatic}")) continue;

            var args = string.Join(", ", method.Parameters.Select(p => $"{MapCppTypeToCs(p.Type, ns)} {EscapeKeyword(Sanitize(p.Name))}"));
            var returnType = mName == ".ctor" ? "" : (csReturn + " ");
            var methodNameActual = mName == ".ctor" ? Sanitize(typeNameFinal) : mName;
            var staticMod = method.IsStatic ? "static " : "";
            var body = (mName == ".ctor" || csReturn == "void") ? "{ }" : "=> default;";
            
            if (isInterface)
            {
                if (method.IsStatic) continue;
                writer.WriteLine($"{currentIndent}    {returnType}{methodNameActual}({args});");
            }
            else
            {
                writer.WriteLine($"{currentIndent}    public {staticMod}{returnType}{methodNameActual}({args}) {body}");
            }
        }

        GenerateNestedStubs(writer, ns, typeName, currentIndent, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        writer.WriteLine($"{currentIndent}}}");

        for (int i = hierarchy.Count - 2; i >= 0; i--)
            writer.WriteLine($"{new string(' ', (i + 1) * 4)}}}");
    }

    private void GenerateNestedStubs(StreamWriter writer, string ns, string parentCppName, string indent, HashSet<string> seenNames)
    {
        var nested = _nestedTypes.Where(t => t.ns == ns && t.parentType == parentCppName).OrderBy(t => t.nestedType).ToList();
        foreach (var stub in nested)
        {
            var h = SplitNesting(stub.nestedType);
            if (h.Count == 0) continue;
            var (gn, gp) = ConvertToGenericNameWithArity(h[0].name, stub.arity);
            var safeGn = Sanitize(gn);
            if (seenNames.Add(gn))
            {
                var fullCpp = parentCppName + "_" + stub.nestedType;
                var isStruct = _types.TryGetValue((ns, fullCpp), out var pData) && pData.IsValueType;
                writer.WriteLine($"{indent}    public partial {(isStruct ? "struct" : "class")} {safeGn}{gp}");
                writer.WriteLine($"{indent}    {{");
                GenerateNestedStubs(writer, ns, fullCpp, indent + "    ", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                writer.WriteLine($"{indent}    }}");
            }
        }
    }

    private string MapCppTypeToCs(string cppType, string currentNs)
    {
        if (string.IsNullOrEmpty(cppType)) return "System.Object";

        cppType = cppType.Trim();
        cppType = Regex.Replace(cppType, @"^(inline|static|const|constexpr)\s+", "");
        cppType = cppType.Trim();
        
        bool isAbsolute = cppType.StartsWith("::");
        cppType = cppType.Replace("::", ".");
        cppType = Regex.Replace(cppType, @"(^|[<,]\s*)\.+", "$1");

        while (cppType.EndsWith("*"))
            cppType = cppType.Substring(0, cppType.Length - 1).Trim();

        if (cppType.StartsWith("UnityW<"))
        {
            var inner = cppType.Substring(7, cppType.Length - 8);
            return MapCppTypeToCs(inner, currentNs);
        }

        var prim = cppType switch
        {
            "void" => "void", "bool" => "bool", "int8_t" or "int8" => "sbyte", "uint8_t" or "uint8" => "byte",
            "int16_t" or "int16" => "short", "uint16_t" or "uint16" => "ushort", "int32_t" or "int32" => "int",
            "uint32_t" or "uint32" => "uint", "int64_t" or "int64" => "long", "uint64_t" or "uint64" => "ulong",
            "float" or "float_t" => "float", "double" or "double_t" => "double", "char" or "char16_t" => "char",
            "Il2CppString" or "StringW" => "System.String", "Il2CppObject" => "System.Object", "IntPtr" => "nint",
            "UIntPtr" => "nuint", "size_t" => "nuint", "ArrayW" => "byte[]", "Array" => "global::System.Array",
            "ByRef" => "ref", _ => null
        };
        if (prim != null) return prim;

        if (cppType.StartsWith("cordl_internals.") || cppType.StartsWith("MS.") || cppType.Contains(".HEU") || cppType.Contains(".HAPI") || cppType.Contains(".Test"))
            return "System.Object";

        if (Regex.IsMatch(cppType, @"^(T|T[A-Z][a-z0-9]*|T[0-9]+)$"))
            return "System.Object";

        if (cppType == "T" || cppType == "T1" || cppType == "T2" || cppType == "TKey" || cppType == "TValue" || cppType == "TResult")
            return "System.Object";

        if (cppType.Contains("<"))
        {
            var lt = cppType.IndexOf('<');
            var gt = cppType.LastIndexOf('>');
            if (lt > 0 && gt > lt)
            {
                var baseTypeStr = cppType.Substring(0, lt);
                var argsPart = cppType.Substring(lt + 1, gt - lt - 1);
                var lastDotBase = baseTypeStr.LastIndexOf('.');
                var baseTypeLast = lastDotBase >= 0 ? baseTypeStr.Substring(lastDotBase + 1) : baseTypeStr;
                
                var h = SplitNesting(baseTypeLast);
                var argsArr = SplitGenericArgs(argsPart);
                var processedArgs = argsArr.Select(a =>
                {
                    while (a.EndsWith("*")) a = a.Substring(0, a.Length - 1).Trim();
                    var m = MapCppTypeToCs(a, currentNs);
                    return m == "void" ? "System.Object" : m;
                }).ToList();

                var hBase = SplitNestingValidated(baseTypeStr, currentNs);
                if (hBase.Count > 1)
                {
                    string cns = currentNs;
                    string cp = "";
                    for (int i = 0; i < hBase.Count; i++)
                    {
                        var segment = hBase[i];
                        int arity = (i == hBase.Count - 1) ? processedArgs.Count : segment.arity;
                        if (i > 0)
                        {
                            _nestedTypes.Add((cns, cp, segment.name, arity));
                            cp = cp + "_" + (arity > 0 ? $"{segment.name}_{arity}" : segment.name);
                        }
                        else
                        {
                            cp = (arity > 0 ? $"{segment.name}_{arity}" : segment.name);
                        }
                    }
                }

                var segments = new List<string>();
                int argIdx = 0;
                foreach (var seg in h)
                {
                    if (seg.arity > 0 && argIdx < processedArgs.Count)
                    {
                        var take = Math.Min(seg.arity, processedArgs.Count - argIdx);
                        var slice = processedArgs.Skip(argIdx).Take(take).ToList();
                        argIdx += take;
                        var (gn, _) = ConvertToGenericNameWithArity(seg.name, take);
                        segments.Add(Sanitize(gn) + "<" + string.Join(", ", slice) + ">");
                    }
                    else
                    {
                        segments.Add(Sanitize(seg.name));
                    }
                }

                if (argIdx < processedArgs.Count && segments.Count > 0)
                {
                    var lastSeg = segments[segments.Count - 1];
                    if (!lastSeg.Contains("<"))
                        segments[segments.Count - 1] = lastSeg + "<" + string.Join(", ", processedArgs.Skip(argIdx)) + ">";
                }

                if (segments.Count > 0)
                {
                    if ((segments[0] == "ByRef" || segments[0] == "ByRefConst") && processedArgs.Count > 0) return "ref " + processedArgs[0];
                    if (segments[0] == "Ptr" && processedArgs.Count > 0 && (processedArgs[0] == "void" || processedArgs[0] == "nint" || processedArgs[0] == "object")) return "nint";
                    if (segments[0] == "ArrayW" && processedArgs.Count > 0) return processedArgs[0] + "[]";
                }

                string prefix = "";
                if (lastDotBase >= 0)
                {
                    var nsPart = baseTypeStr.Substring(0, lastDotBase);
                    prefix = MapCppTypeToCs(nsPart, currentNs);
                    if (!prefix.EndsWith(".")) prefix += ".";
                }

                var res = prefix + string.Join(".", segments);
                if (res.StartsWith("global::") || res.StartsWith("ref ") || res.EndsWith("[]")) return res;
                
                var partsRes = res.Split('.').Select(p => p.StartsWith("global::") ? p : SanitizePathSegment(p));
                var finalRes = string.Join(".", partsRes);
                if (finalRes.StartsWith("global::") || finalRes.StartsWith("ref ") || finalRes.EndsWith("[]")) return finalRes;

                if (prefix == "" && !isAbsolute) 
                    return (string.IsNullOrEmpty(currentNs) ? "global::" : "global::" + currentNs + ".") + finalRes;
                
                return "global::" + finalRes.Replace("global::", "");
            }
            return "System.Object";
        }

        if (cppType.Contains("."))
        {
            if (cppType.StartsWith("global::")) return cppType;
            var lastDot = cppType.LastIndexOf('.');
            if (lastDot > 0 && lastDot < cppType.Length - 1)
            {
                var nsPart = cppType.Substring(0, lastDot);
                var typePart = cppType.Substring(lastDot + 1);
                var res = ResolveUnderscoreNested(typePart, nsPart);
                if (res != null) return "global::" + res;
            }
            var parts = cppType.Split('.').Where(p => !string.IsNullOrEmpty(p)).Select(p => SanitizePathSegment(p));
            var dottedRes = string.Join(".", parts);
            if (isAbsolute || dottedRes.StartsWith("global::")) return "global::" + dottedRes.Replace("global::", "");
            return (string.IsNullOrEmpty(currentNs) ? "global::" : "global::" + currentNs + ".") + dottedRes;
        }

        var resolved = ResolveUnderscoreNested(cppType, currentNs);
        if (resolved != null) return "global::" + resolved;

        if (SystemTypes.Contains(cppType)) return "global::System." + Sanitize(cppType);

        if (isAbsolute) return "global::" + SanitizePathSegment(cppType);
        return (string.IsNullOrEmpty(currentNs) ? "global::" : "global::" + currentNs + ".") + SanitizePathSegment(cppType);
    }

    private static string SanitizePathSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment)) return "";
        if (segment.Contains("<"))
        {
            var lt = segment.IndexOf('<');
            var name = segment.Substring(0, lt);
            var args = segment.Substring(lt);
            return Sanitize(name) + args;
        }
        return Sanitize(segment);
    }

    private string ResolveUnderscoreNested(string typeName, string currentNs)
    {
        var h = SplitNesting(typeName);
        if (h.Count < 2) return null;

        for (int i = h.Count - 1; i >= 1; i--)
        {
            var pParts = h.Take(i).ToList();
            var pCppName = string.Join("_", pParts.Select(x => x.arity > 0 ? $"{x.name}_{x.arity}" : x.name));
            var pNs = FindTypeNamespace(pCppName, currentNs);
            if (pNs == null)
            {
                var m = Regex.Match(pCppName, @"^(.+)_(\d+)$");
                if (m.Success) pNs = FindTypeNamespace(m.Groups[1].Value, currentNs);
            }

            if (pNs != null)
            {
                string cp = pCppName;
                string cns = pNs;
                var rest = h.Skip(i).ToList();
                foreach (var s in rest)
                {
                    var segCpp = s.arity > 0 ? $"{s.name}_{s.arity}" : s.name;
                    if (string.IsNullOrEmpty(segCpp)) continue;
                    _nestedTypes.Add((cns, cp, segCpp, s.arity));
                    cp = cp + "_" + segCpp;
                }
                return BuildCsPath(pNs, h);
            }
        }

        if (FindTypeNamespace(typeName, currentNs) != null)
            return null;

        if (typeName.Contains("_") && !string.IsNullOrEmpty(currentNs) && (currentNs.StartsWith("System.") || currentNs == "GlobalNamespace" || currentNs.StartsWith("UnityEngine.") || currentNs.StartsWith("OVR")))
        {
            var res = BuildCsPath("", h);
            if (string.IsNullOrEmpty(res)) return null;
            return currentNs + "." + res;
        }
        return null;
    }

    private string BuildCsPath(string ns, List<(string name, int arity)> h)
    {
        var parts = h
            .Where(x => !string.IsNullOrEmpty(x.name))
            .Select(x => {
                var (gn, gp) = ConvertToGenericNameWithArity(x.name, x.arity);
                return Sanitize(gn) + gp;
            });
        var path = string.Join(".", parts);
        return string.IsNullOrEmpty(ns) ? path : ns + "." + path;
    }

    private static List<(string name, int arity)> SplitNesting(string typeName)
    {
        var res = new List<(string name, int arity)>();
        if (string.IsNullOrEmpty(typeName)) return res;

        var rawParts = typeName.Split('_');
        var parts = new List<string>();
        string leading = "";
        int idx = 0;
        while (idx < rawParts.Length && string.IsNullOrEmpty(rawParts[idx]))
        {
            leading += "_";
            idx++;
        }
        if (idx < rawParts.Length)
        {
            parts.Add(leading + rawParts[idx]);
            idx++;
        }
        for (; idx < rawParts.Length; idx++)
        {
            if (!string.IsNullOrEmpty(rawParts[idx]))
                parts.Add(rawParts[idx]);
        }

        for (int i = 0; i < parts.Count; i++)
        {
            string p = parts[i];
            int arity = 0;
            if (i < parts.Count - 1 && Regex.IsMatch(parts[i + 1], @"^\d+$"))
            {
                arity = int.Parse(parts[i + 1]);
                i++;
            }
            res.Add((p, arity));
        }
        return res;
    }

    private List<(string name, int arity)> SplitNestingValidated(string typeName, string ns)
    {
        if (string.IsNullOrEmpty(typeName)) return new List<(string name, int arity)>();

        var h = SplitNesting(typeName);
        if (h.Count < 2) return h;

        for (int i = h.Count - 1; i >= 1; i--)
        {
            var pParts = h.Take(i).ToList();
            var pCppName = string.Join("_", pParts.Select(x => x.arity > 0 ? $"{x.name}_{x.arity}" : x.name));
            var pNs = FindTypeNamespace(pCppName, ns);
            if (pNs == null)
            {
                var m = Regex.Match(pCppName, @"^(.+)_(\d+)$");
                if (m.Success) pNs = FindTypeNamespace(m.Groups[1].Value, ns);
            }

            if (pNs != null)
            {
                var validated = new List<(string name, int arity)>();
                validated.AddRange(h.Take(i));
                validated.AddRange(h.Skip(i));
                return validated;
            }
        }

        var fullName = string.Join("_", h.Select(x => x.arity > 0 ? $"{x.name}_{x.arity}" : x.name));
        var fullArityMatch = Regex.Match(fullName, @"^(.+?)_(\d+)$");
        if (fullArityMatch.Success)
            return new List<(string name, int arity)> { (fullArityMatch.Groups[1].Value, int.Parse(fullArityMatch.Groups[2].Value)) };
        return new List<(string name, int arity)> { (typeName, 0) };
    }

    private static string StripAritySuffix(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return typeName;
        var lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            var prefix = typeName.Substring(0, lastDot);
            var lastPart = typeName.Substring(lastDot + 1);
            var m = Regex.Match(lastPart, @"^(.+?)_\d+$");
            if (m.Success) return prefix + "." + m.Groups[1].Value;
            return typeName;
        }
        else
        {
            var m = Regex.Match(typeName, @"^(.+?)_\d+$");
            if (m.Success) return m.Groups[1].Value;
            return typeName;
        }
    }

    private static (string name, string genericParams) ConvertToGenericNameWithArity(string name, int arity)
    {
        if (arity == 0) return (name, "");
        var gp = arity switch { 1 => "<T>", 2 => "<TKey, TValue>", _ => $"<{string.Join(", ", Enumerable.Range(1, arity).Select(i => $"T{i}"))}>" };
        return (name, gp);
    }

    private string FindTypeNamespace(string typeName, string currentNs = null)
    {
        var matches = _types.Keys
            .Where(k => k.typeName == typeName || (k.typeName.StartsWith(typeName + "_") && k.typeName.Length > typeName.Length + 1 && char.IsDigit(k.typeName[typeName.Length + 1])))
            .Select(k => k.ns).Distinct().ToList();
        if (matches.Count == 0) return null;
        if (matches.Count == 1) return matches[0];
        if (currentNs != null && matches.Contains(currentNs)) return currentNs;
        return null;
    }

    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "UnknownType";
        if (char.IsDigit(name[0])) name = "_" + name;
        return EscapeKeyword(name);
    }

    private static string EscapeKeyword(string name)
    {
        var keywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while", "add", "alias", "ascending", "async", "await",
            "by", "descending", "dynamic", "equals", "from", "get", "global", "group", "into",
            "join", "let", "nameof", "on", "orderby", "partial", "remove", "select", "set",
            "unmanaged", "value", "var", "when", "where", "yield", "nint", "nuint"
        };
        return keywords.Contains(name) ? "@" + name : name;
    }

    private static void WriteHeader(StreamWriter w)
    {
        w.WriteLine("#nullable disable\n#pragma warning disable\nusing global::System;\nusing global::System.Collections.Generic;\n");
    }

    private static List<string> SplitGenericArgs(string args)
    {
        var result = new List<string>();
        var depth = 0;
        var current = new StringBuilder();

        foreach (var c in args)
        {
            if (c == '<') { depth++; current.Append(c); }
            else if (c == '>') { depth--; current.Append(c); }
            else if (c == ',' && depth == 0)
            {
                if (current.Length > 0) result.Add(current.ToString().Trim());
                current.Clear();
            }
            else current.Append(c);
        }

        if (current.Length > 0) result.Add(current.ToString().Trim());
        return result;
    }
}

class TypeData { public string Namespace { get; set; } = ""; public string TypeName { get; set; } = ""; public List<FieldInfo> Fields { get; set; } = new(); public List<MethodInfo> Methods { get; set; } = new(); public bool IsValueType { get; set; } public bool IsStatic { get; set; } public bool IsAbstract { get; set; } public string BaseType { get; set; } }
class FieldInfo { public string Type { get; set; } = ""; public string Name { get; set; } = ""; public bool IsStatic { get; set; } }
class MethodInfo { public string ReturnType { get; set; } = ""; public string Name { get; set; } = ""; public bool IsStatic { get; set; } public List<ParameterInfo> Parameters { get; set; } = new(); }
class ParameterInfo { public string Type { get; set; } = ""; public string Name { get; set; } = ""; }