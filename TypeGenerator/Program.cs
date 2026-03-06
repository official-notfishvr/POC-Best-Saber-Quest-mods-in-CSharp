// to do make this work :sob
/*
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

    private static readonly HashSet<string> SkippedTypeNames = new()
    {
        "ValueTuple", "ValueTuple_1", "ValueTuple_2", "ValueTuple_3", "ValueTuple_4",
        "ValueTuple_5", "ValueTuple_6", "ValueTuple_7", "ValueTuple_8",
        "Tuple", "Tuple_1", "Tuple_2", "Tuple_3", "Tuple_4",
        "Tuple_5", "Tuple_6", "Tuple_7", "Tuple_8",
        "List_1", "Dictionary_2", "HashSet_1", "Queue_1", "Stack_1",
        "LinkedList_1", "LinkedListNode_1", "IEnumerable_1", "IEnumerator_1",
        "IList_1", "IReadOnlyList_1", "IReadOnlyDictionary_2", "IReadOnlyCollection_1",
        "ICollection_1", "IReadOnlySet_1",
        "Nullable_1", "Action", "Action_1", "Action_2", "Action_3", "Action_4",
        "Action_5", "Action_6", "Action_7", "Action_8",
        "Func", "Func_1", "Func_2", "Func_3", "Func_4", "Func_5",
        "Func_6", "Func_7", "Func_8", "Func_9", "Func_10",
        "EventHandler_1", "Task_1", "TaskAwaiter_1",
        "String", "Object", "Boolean", "Byte", "SByte", "Int16", "UInt16",
        "Int32", "UInt32", "Int64", "UInt64", "Single", "Double", "Char",
        "Decimal", "IntPtr", "UIntPtr", "Void", "Enum", "ValueType",
        "BigInteger", "Guid", "DateTime", "DateTimeOffset", "TimeSpan", "Type", "Attribute",
        "Color", "Color32", "Vector2", "Vector3", "Vector4", "Quaternion",
        "Rect", "Bounds", "Matrix4x4", "GameObject", "Transform",
        "Component", "MonoBehaviour", "ScriptableObject",
        "NativeArray_1", "Span_1", "ReadOnlySpan_1",
        "Tween_1", "Tween", "ITween",
        "BinaryFormatter", "SoapFormatter", "Formatter",
    };

    private static readonly HashSet<string> BurstNamespaces = new()
    {
        "Unity.Burst",
        "Unity.Collections",
        "Unity.Jobs",
        "Unity.Burst.Intrinsics",
        "Unity.Mathematics",
    };

    private static readonly HashSet<string> SkippedNamespaces = new()
    {
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Xml", "System.Xml.Linq", "System.Xml.Schema", "System.Xml.XPath", "System.Xml.Xsl", "System.Xml.Serialization",
        "System.IO", "System.IO.Compression",
        "System.Text", "System.Text.RegularExpressions",
        "System.Threading", "System.Threading.Tasks",
        "System.Reflection",
        "System.Runtime", "System.Runtime.CompilerServices", "System.Runtime.InteropServices", "System.Runtime.Serialization",
        "System.Security", "System.Security.Cryptography", "System.Security.Cryptography.X509Certificates",
        "System.Security.Principal", "System.Security.Permissions", "System.Security.AccessControl",
        "System.Diagnostics", "System.Diagnostics.CodeAnalysis", "System.Diagnostics.Tracing",
        "System.Globalization", "System.Resources", "System.ComponentModel", "System.ComponentModel.Design",
        "System.Linq", "System.Linq.Expressions",
        "System.Net", "System.Net.Http", "System.Net.Sockets", "System.Net.WebSockets",
        "System.Net.Mail", "System.Net.Security",
        "System.Numerics", "System.Data", "System.Data.Common", "System.Transactions",
        "System.Web", "System.Web.Services", "System.Configuration", "System.CodeDom",
        "System.Drawing", "System.Windows", "System.Windows.Forms",
        "System.Media", "System.ServiceProcess", "System.Timers",
        "Microsoft.Win32",
        "UnityEngine", "UnityEngine.AI", "UnityEngine.Animations", "UnityEngine.Audio",
        "UnityEngine.CoreModule", "UnityEngine.Director", "UnityEngine.GeometryUtility",
        "UnityEngine.InputLegacyModule", "UnityEngine.JSONSerializeModule",
        "UnityEngine.ParticleSystemModule", "UnityEngine.PhysicsModule", "UnityEngine.Physics2DModule",
        "UnityEngine.ScreenModule", "UnityEngine.SharedInternalsModule", "UnityEngine.SpriteMaskModule",
        "UnityEngine.SpriteShapeModule", "UnityEngine.StreamingModule", "UnityEngine.SubstanceModule",
        "UnityEngine.SubsystemsModule", "UnityEngine.TerrainModule", "UnityEngine.TerrainPhysicsModule",
        "UnityEngine.TextCoreModule", "UnityEngine.TextRenderingModule", "UnityEngine.TilemapModule",
        "UnityEngine.UI", "UnityEngine.UIModule", "UnityEngine.UmbraModule",
        "UnityEngine.UnityAnalyticsModule", "UnityEngine.UnityTestProtocolModule",
        "UnityEngine.UnityWebRequestModule", "UnityEngine.UnityWebRequestAssetBundleModule",
        "UnityEngine.UnityWebRequestAudioModule", "UnityEngine.UnityWebRequestTextureModule",
        "UnityEngine.UnityWebRequestWWWModule", "UnityEngine.VFXModule", "UnityEngine.VideoModule",
        "UnityEngine.VirtualTexturingModule", "UnityEngine.WindModule", "UnityEngine.XR",
    };

    private static readonly HashSet<string> InlineTypes = new()
    {
        "Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Color32",
        "Rect", "Bounds", "Matrix4x4", "RangeInt", "RectInt", "RectIntPosition",
        "Object", "GameObject", "Transform", "Component", "MonoBehaviour",
        "ScriptableObject", "Behaviour", "Material", "Texture", "Texture2D",
        "Shader", "Mesh", "AnimationClip", "Animator", "AudioSource", "Camera",
        "Light", "ParticleSystem", "Rigidbody", "Collider", "MeshRenderer",
        "SkinnedMeshRenderer", "RectTransform", "Mathf", "Time", "Debug",
        "MathfInternal", "Pose", "LayerMask", "AnimationCurve", "Gradient",
        "Terrain", "TerrainData", "TerrainLayer", "TreeInstance", "LODGroup",
        "MeshFilter", "RectOffset", "TextureFormat", "EventSystem", "BaseEventData",
        "PointerEventData", "InputControl", "InputAction", "AsyncOperation",
        "IResourceLocation", "ResourceLocationBase", "AssetBundleProvider",
        "BundledAssetProvider", "SceneProvider", "Addressables", "Scene",
        "LoadSceneMode", "Image", "Text", "Button", "Toggle", "Slider",
        "ScrollRect", "Canvas", "Graphic", "GraphicRaycaster",
        "AnimatorControllerParameter", "AudioMixer", "AudioMixerGroup",
        "Joint", "HingeJoint", "SpringJoint", "FixedJoint", "ConfigurableJoint",
        "ParticleSystemEmitter", "ParticleSystemVelocityOverLifetimeModule",
        "ParticleSystemLimitVelocityOverLifetimeModule", "ParticleSystemInheritVelocityModule",
        "ParticleSystemLifetimeByEmitterSpeedModule", "BuiltinRenderTextureType",
        "RenderBuffer", "PlayableGraph", "Playable", "PlayableDirector",
        "TimelineAsset", "TrackAsset", "TimelineClip", "VFXManager",
        "VideoPlayer", "VideoClip", "XRNode", "XRController", "XRSettings",
        "TrackedPoseDriver", "NetworkConnection", "NetworkIdentity", "NetworkBehaviour",
        "FormerlySerializedAsAttribute", "PreserveAttribute", "RequiredByNativeCodeAttribute",
        "ObjectPool", "TMP_FontAsset", "TMP_Text", "TMP_InputField", "TMP_Dropdown",
        "GUIStyle", "GUIContent", "GUILayoutOption", "SpriteMask",
        "SplatPrototype", "TreePrototype", "DetailPrototype", "WindZone",
        "VirtualTexturingSettings", "AnalyticsResult", "UnityWebRequest",
        "UnityWebRequestAsyncOperation", "DownloadHandler", "UploadHandler",
        "UnityWebRequestAssetBundle", "UnityWebRequestAudio", "DownloadHandlerAudioClip",
        "UnityWebRequestTexture", "DownloadHandlerTexture", "WWW",
        "NavMeshAgent", "NavMeshData", "NavMeshHit", "NavMeshQueryFilter",
        "Subsystem", "SubsystemDescriptor", "SpriteShapeController", "SpriteShapeRenderer",
        "StreamingController", "NativeArray", "NativeList", "NativeHashMap",
        "NativeQueue", "NativeReference", "ProfilerCategory",
    };

    private static bool IsValidCsType(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        if (type == "object" || type == "void" || type == "string" || type == "bool"
            || type == "int" || type == "uint" || type == "long" || type == "ulong"
            || type == "short" || type == "ushort" || type == "byte" || type == "sbyte"
            || type == "float" || type == "double" || type == "char" || type == "nint"
            || type == "nuint") return true;

        if (Regex.IsMatch(type, @"\bobject\s*<")) return false;
        if (type.Contains("::")) return false;
        if (type.Contains("__")) return false;

        int depth = 0;
        foreach (char c in type)
        {
            if (c == '<') depth++;
            else if (c == '>')
            {
                depth--;
                if (depth < 0) return false;
            }
        }
        if (depth != 0) return false;

        if (type.StartsWith("<") || type.StartsWith(">")) return false;

        if (Regex.IsMatch(type, @"\bobject\b\s*<")) return false;

        return true;
    }

    private static string SafeType(string type, string fallback = "object")
        => IsValidCsType(type) ? type : fallback;

    public void Generate(string includeFolder, string outputFolder)
    {
        Console.WriteLine($"Scanning {includeFolder}...");

        foreach (var dir in Directory.GetDirectories(includeFolder))
        {
            var ns = Path.GetFileName(dir);
            if (ns.StartsWith("zzzz__") || ns == "Internal") continue;
            ProcessNamespace(dir, ns);
        }

        Console.WriteLine($"Found {_types.Count} types");
        Directory.CreateDirectory(outputFolder);

        var outputFile = Path.Combine(outputFolder, "GeneratedTypes.cs");

        using var writer = new StreamWriter(outputFile, false, new UTF8Encoding(false), 1 << 20 /* 1 MB buffer */);

        WriteHeader(writer);
        WriteInlineNamespaces(writer);

        var byNs = _types.Keys
            .GroupBy(k => k.ns)
            .OrderBy(g => g.Key);

        foreach (var group in byNs)
        {
            var ns = group.Key;
            var typeNames = group.Select(k => k.typeName).OrderBy(x => x).ToList();

            writer.WriteLine($"    namespace {ns}");
            writer.WriteLine("    {");

            foreach (var typeName in typeNames)
            {
                if (_types.TryGetValue((ns, typeName), out var data))
                    GenerateTypeStub(writer, typeName, ns, data);
            }

            writer.WriteLine("    }");
            writer.WriteLine();
        }

        writer.WriteLine("}");
        writer.Flush();

        Console.WriteLine($"Generated {outputFile}");
    }

    private void ProcessNamespace(string dir, string ns)
    {
        if (BurstNamespaces.Any(bn => ns.StartsWith(bn) || ns.Contains(".Burst.") || ns.Contains(".Jobs.") || ns.Contains(".Collections.")))
            return;
        if (SkippedNamespaces.Any(sn => ns == sn || ns.StartsWith(sn + ".")))
            return;

        foreach (var file in Directory.GetFiles(dir, "*.hpp"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.EndsWith("_impl")) continue;
            if (fileName.StartsWith("_PrivateImplementationDetails") ||
                fileName.Contains("=") || fileName.Contains(" ") || fileName.Contains("-")) continue;
            if (fileName == ns) continue;
            if (KeywordHelper.IsKeyword(fileName)) continue;
            if (fileName.Contains("<") || fileName.Contains(">") || fileName.Contains("`")) continue;
            if (fileName.Contains("__")) continue;

            ProcessTypeFile(file, fileName, ns);
        }

        foreach (var subdir in Directory.GetDirectories(dir))
        {
            var subNs = Path.GetFileName(subdir);
            if (subNs.StartsWith("zzzz__")) continue;
            ProcessNamespace(subdir, $"{ns}.{subNs}");
        }
    }

    private void ProcessTypeFile(string filePath, string typeName, string ns)
    {
        if (SkippedTypeNames.Contains(typeName)) return;
        if (InlineTypes.Contains(typeName)) return;

        var key = (ns, typeName);
        if (_types.ContainsKey(key)) return;

        var data = new TypeData { Namespace = ns, TypeName = typeName };
        _types[key] = data;

        var defFile = Path.Combine(Path.GetDirectoryName(filePath)!, $"zzzz__{typeName}_def.hpp");
        if (!File.Exists(defFile)) return;

        var defContent = File.ReadAllText(defFile);
        data.Fields = ExtractFields(defContent);
        data.Methods = ExtractMethods(defContent);
    }


    private List<FieldInfo> ExtractFields(string content)
    {
        var fields = new List<FieldInfo>();
        var seenNames = new HashSet<string>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            var nextLine = lines[i + 1].Trim();

            if (!line.StartsWith("/// @brief Field")) continue;

            var m = Regex.Match(nextLine, @"^(?:static\s+)?(.+?)\s+(\w+)\s*;");
            if (!m.Success) continue;

            var fieldName = m.Groups[2].Value;
            if (fieldName.StartsWith("__") || fieldName.Contains("BackingField")) continue;
            if (!seenNames.Add(fieldName)) continue;

            fields.Add(new FieldInfo { Type = m.Groups[1].Value.Trim(), Name = fieldName });
        }

        return fields;
    }

    private List<MethodInfo> ExtractMethods(string content)
    {
        var methods = new List<MethodInfo>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            var nextLine = lines[i + 1].Trim();

            if (!line.StartsWith("/// @brief Method")) continue;
            if (nextLine.Contains("template <")) continue;

            var m = Regex.Match(nextLine, @"(?:inline\s+|static\s+)?(.+?)\s+(\w+)\s*\(([^)]*)\)");
            if (!m.Success) continue;

            var methodName = m.Groups[2].Value;
            if (methodName.StartsWith("_") || methodName.Contains("ctor") || methodName.Contains("Finalize")) continue;
            if (methodName == "MoveNext" || methodName == "SetStateMachine") continue;

            methods.Add(new MethodInfo
            {
                ReturnType = m.Groups[1].Value.Trim(),
                Name = methodName,
                Parameters = ParseParameters(m.Groups[3].Value)
            });
        }

        return methods;
    }

    private List<ParameterInfo> ParseParameters(string parameters)
    {
        var result = new List<ParameterInfo>();
        if (string.IsNullOrWhiteSpace(parameters)) return result;

        foreach (var part in SplitByCommaRespectingBrackets(parameters))
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace <= 0) continue;

            var type = trimmed.Substring(0, lastSpace).Trim();
            var name = trimmed.Substring(lastSpace + 1).Trim();

            if (type.EndsWith(">") && name.StartsWith("UnityW<"))
            { type = trimmed; name = "value"; }

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

    private void GenerateTypeStub(StreamWriter writer, string typeName, string ns, TypeData data)
    {
        var fields = data.Fields ?? new List<FieldInfo>();
        var methods = data.Methods ?? new List<MethodInfo>();

        var isInterface = typeName.Length is >= 2 and <= 40
            && typeName.StartsWith("I") && char.IsUpper(typeName[1])
            && !typeName.StartsWith("IVR") && !typeName.StartsWith("ICV")
            && !Regex.IsMatch(typeName, @"^I[A-Z]{2,}");

        var genericParams = "";
        var classTypeParams = new HashSet<string>();
        var genericMatch = Regex.Match(typeName, @"_(\d+)$");
        if (genericMatch.Success && int.TryParse(genericMatch.Groups[1].Value, out var count) && count is > 0 and <= 15)
        {
            var names = new[] { "T", "TKey", "TValue", "TResult", "TState", "TElement",
                                "T1","T2","T3","T4","T5","T6","T7","T8","T9","T10","T11","T12","T13","T14","T15" };
            var taken = names.Take(count).ToArray();
            genericParams = "<" + string.Join(", ", taken) + ">";
            foreach (var n in taken) classTypeParams.Add(n);
        }

        var emitted = new HashSet<string>();
        var addedProperties = new HashSet<string>();
        var addedEvents = new HashSet<string>();

        bool TypeHasLeakedParams(string t)
        {
            var tokens = Regex.Matches(t, @"\b([A-Z][A-Za-z0-9]*)\b");
            foreach (Match m2 in tokens)
            {
                var tok = m2.Value;
                if (tok is "System" or "Collections" or "Generic" or "global" or "List" or "Dictionary"
                    or "HashSet" or "Queue" or "Stack" or "LinkedList" or "LinkedListNode"
                    or "IEnumerator" or "IEnumerable" or "IReadOnlyList" or "IList" or "ICollection"
                    or "IReadOnlyCollection" or "IReadOnlyDictionary" or "IDictionary"
                    or "KeyValuePair" or "Nullable" or "Action" or "Func" or "EventHandler"
                    or "Task" or "TaskAwaiter" or "Span" or "ReadOnlySpan" or "ArraySegment"
                    or "NativeArray" or "NativeList" or "NativeHashMap" or "NativeQueue" or "NativeReference"
                    or "IntPtr" or "UIntPtr" or "Array" or "ArrayPool" or "Type" or "String" or "Object"
                    or "Int32" or "Int64" or "UInt32" or "UInt64" or "Boolean" or "Single" or "Double"
                    or "Byte" or "SByte" or "Char" or "Decimal" or "Void"
                    or "UnityEngine" or "Unity" or "GeneratedTypes")
                    continue;

                bool looksLikeParam = (tok.Length == 1 && char.IsUpper(tok[0]))
                    || Regex.IsMatch(tok, @"^T[A-Z]")
                    || Regex.IsMatch(tok, @"^T\d+$")
                    || tok == "TIn" || tok == "TOut" || tok == "TBase"
                    || Regex.IsMatch(tok, @"^[UV][A-Z]");
                if (!looksLikeParam) continue;
                if (classTypeParams.Contains(tok)) continue;
                return true;
            }
            return false;
        }

        bool TryMapTypeSafe(string cppType, out string result)
        {
            if (!TryMapType(cppType, ns, out result)) return false;
            if (TypeHasLeakedParams(result)) { result = null!; return false; }
            return true;
        }

        writer.WriteLine($"        public {(isInterface ? "interface" : "class")} {typeName}{genericParams}");
        writer.WriteLine("        {");

        if (!isInterface)
        {
            foreach (var field in fields)
            {
                var csType = TryMapTypeSafe(field.Type, out var mappedField) ? mappedField : null;

                var safeName = KeywordHelper.EscapeKeyword(field.Name);
                if (safeName == typeName) safeName = "_" + safeName;
                if (emitted.Contains($"prop:{safeName}")) continue;
                if (!emitted.Add($"field:{safeName}")) continue;

                if (csType != null)
                    writer.WriteLine($"            public {csType} {safeName};");
                else
                    writer.WriteLine($"            // public <unmappable type '{EscapeComment(field.Type)}'> {safeName};");
            }
        }

        foreach (var method in methods)
        {
            if (method.Name.StartsWith("get_"))
            {
                var propName = method.Name.Substring(4);
                if (!addedProperties.Add(propName)) continue;

                if (!TryMapTypeSafe(method.ReturnType, out var csType) || csType == "void")
                {
                    writer.WriteLine($"            // public <unmappable type '{EscapeComment(method.ReturnType)}'> {propName} {{ get; set; }}");
                    continue;
                }

                var safeName = KeywordHelper.EscapeKeyword(propName);
                if (safeName == typeName) safeName = "_" + safeName;
                if (emitted.Contains($"field:{safeName}")) continue;
                if (!emitted.Add($"prop:{safeName}")) continue;

                writer.WriteLine($"            public {csType} {safeName} {{ get; set; }}");
            }
            else if (method.Name.StartsWith("set_"))
            {
            }
            else if (method.Name.StartsWith("add_"))
            {
                var eventName = method.Name.Substring(4);
                if (!addedEvents.Add(eventName)) continue;

                if (method.Parameters.Count == 0) continue;

                if (!TryMapTypeSafe(method.Parameters[0].Type, out var evType))
                {
                    writer.WriteLine($"            // public event <unmappable type '{EscapeComment(method.Parameters[0].Type)}'> {eventName};");
                    continue;
                }

                evType = EnsureDelegateType(evType);
                if (!IsValidDelegateType(evType))
                {
                    writer.WriteLine($"            // public event <non-delegate '{EscapeComment(evType)}'> {eventName};");
                    continue;
                }

                var safeName = KeywordHelper.EscapeKeyword(eventName);
                if (safeName == typeName) safeName = "_" + safeName;
                if (!emitted.Add($"event:{safeName}")) continue;

                writer.WriteLine($"            public event {evType} {safeName};");
            }
            else if (method.Name.StartsWith("remove_"))
            {
            }
            else
            {
                if (emitted.Contains($"field:{method.Name}") || emitted.Contains($"prop:{method.Name}")) continue;

                if (!TryMapTypeSafe(method.ReturnType, out var csReturn))
                {
                    writer.WriteLine($"            // public <unmappable return '{EscapeComment(method.ReturnType)}'> {method.Name}(...) {{ }}");
                    continue;
                }

                var paramParts = new List<string>();
                bool paramsOk = true;
                var paramTypeKey = new StringBuilder();

                foreach (var p in method.Parameters)
                {
                    if (!TryMapTypeSafe(p.Type, out var pType))
                    {
                        writer.WriteLine($"            // public {csReturn} {method.Name}(...) {{ /* unmappable param type '{EscapeComment(p.Type)}' */ }}");
                        paramsOk = false;
                        break;
                    }
                    paramParts.Add($"{pType} {KeywordHelper.EscapeKeyword(p.Name)}");
                    paramTypeKey.Append(pType).Append(',');
                }

                if (!paramsOk) continue;

                var dedupeKey = $"method:{method.Name}({paramTypeKey})";
                if (!emitted.Add(dedupeKey)) continue;

                var paramStr = string.Join(", ", paramParts);

                if (csReturn == "void")
                    writer.WriteLine($"            public void {method.Name}({paramStr}) {{ }}");
                else
                    writer.WriteLine($"            public {csReturn} {method.Name}({paramStr}) => default;");
            }
        }

        writer.WriteLine("        }");
        writer.WriteLine();
    }

    private bool TryMapType(string cppType, string currentNs, out string result)
    {
        result = null!;
        try
        {
            var mapped = MapCppTypeToCs(cppType, currentNs);
            mapped = SanitizeType(mapped, currentNs);

            if (!IsValidCsType(mapped)) return false;
            result = mapped;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string SanitizeType(string csType, string currentNs)
    {
        if (string.IsNullOrEmpty(csType)) return "object";
        if (csType == "void" || csType == "object") return csType;
        if (csType.StartsWith("global::")) return csType;
        if (csType.StartsWith("UnityEngine.") || csType.StartsWith("Unity.")) return csType;

        if (Regex.IsMatch(csType, @"\bobject\s*<")) return "object";

        if (csType.Contains("<") && csType.Contains(">"))
        {
            var m = Regex.Match(csType, @"^([^<]+)<(.+)>(.*)$");
            if (m.Success)
            {
                var baseName = m.Groups[1].Value.Trim();
                var argsRaw = m.Groups[2].Value;
                var suffix = m.Groups[3].Value;

                if (baseName == "object") return "object";

                var sanitizedArgs = string.Join(", ",
                    SplitByCommaRespectingBrackets(argsRaw)
                        .Select(a => SanitizeType(a.Trim(), currentNs)));

                if (Regex.IsMatch(sanitizedArgs, @"\bobject\s*<")) return "object";

                return $"{baseName}<{sanitizedArgs}>{suffix}";
            }
        }

        if (csType.StartsWith("GeneratedTypes."))
        {
            var inner = csType.Substring("GeneratedTypes.".Length);
            var dot = inner.IndexOf('.');
            if (dot > 0)
            {
                var ns = inner.Substring(0, dot);
                var name = inner.Substring(dot + 1);
                if (!_types.ContainsKey((ns, name)) && !InlineTypes.Contains(name))
                    return "object";
            }
            return csType;
        }

        if (csType.Contains(".") && !csType.StartsWith("global::"))
        {
            var parts = csType.Split('.');
            var lastName = parts[^1];
            if (Regex.IsMatch(lastName, @"^[A-Z][A-Za-z0-9]*$"))
            {
                bool found = _types.Keys.Any(k => k.typeName == lastName)
                             || InlineTypes.Contains(lastName);
                if (!found) return "object";
            }
        }

        return csType;
    }

    private string MapCppTypeToCs(string cppType, string currentNs)
    {
        if (string.IsNullOrEmpty(cppType)) return "object";

        cppType = cppType.Trim();
        cppType = Regex.Replace(cppType, @"^(inline|static|const|constexpr)\s+", "");
        cppType = Regex.Replace(cppType, @"\s+const(\s|$)", " ");
        cppType = cppType.Trim();

        if (cppType.StartsWith("__declspec")) return "object";

        while (cppType.EndsWith("*"))
            cppType = cppType.Substring(0, cppType.Length - 1).Trim();

        cppType = cppType.Replace("::", ".");
        cppType = Regex.Replace(cppType, @"<\.", "<");
        cppType = Regex.Replace(cppType, @",\s*\.", ", ");
        while (cppType.StartsWith(".")) cppType = cppType.Substring(1);

        var arraySuffix = "";
        if (cppType.EndsWith("[]") && !cppType.Contains('<'))
        {
            arraySuffix = "[]";
            cppType = cppType.Substring(0, cppType.Length - 2);
        }

        var templateMatch = Regex.Match(cppType, @"^([\w\.]+)<");
        if (templateMatch.Success)
        {
            var baseName = templateMatch.Groups[1].Value;
            var startIdx = cppType.IndexOf('<');
            var endIdx = FindMatchingAngleBracket(cppType, startIdx);
            if (endIdx == -1) return "object";

            var typeArgs = cppType.Substring(startIdx + 1, endIdx - startIdx - 1);
            var argParts = SplitByCommaRespectingBrackets(typeArgs);

            string A(int i) => i < argParts.Count ? MapCppTypeToCs(argParts[i].Trim(), currentNs) : "object";
            string AllArgs() => string.Join(", ", argParts.Select(a => MapCppTypeToCs(a.Trim(), currentNs)));

            bool AnyArgBad() => argParts.Any(a =>
            {
                var m = MapCppTypeToCs(a.Trim(), currentNs);
                return Regex.IsMatch(m, @"\bobject\s*<");
            });

            if (baseName == "ArrayW" || baseName == "Array")
            {
                if (AnyArgBad()) return "object";
                var first = A(0);
                return first.EndsWith("[]") ? $"{first.Substring(0, first.Length - 2)}[][]{arraySuffix}" : $"{first}[]{arraySuffix}";
            }

            if (baseName is "Ptr" || baseName.EndsWith(".Ptr")) return "global::System.IntPtr";
            if (baseName is "ByRef" || baseName.EndsWith(".ByRef")) return "object";
            if (baseName is "ByRefConst" || baseName.EndsWith(".ByRefConst")) return "object";

            if (baseName == "UnityW")
            {
                if (AnyArgBad()) return "object";
                return A(0).TrimStart('.');
            }

            if (baseName == "ReadOnlySpan" || Regex.IsMatch(baseName, @"ReadOnlySpan_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.ReadOnlySpan<{A(0)}>{arraySuffix}";
            }
            if (baseName == "Span" || Regex.IsMatch(baseName, @"Span_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Span<{A(0)}>{arraySuffix}";
            }

            if (Regex.IsMatch(baseName, @"(^|\.)NativeArray_1$"))
            {
                if (AnyArgBad()) return "object";
                var inner = A(0);
                if (inner == "object") return "object[]";
                return $"Unity.Collections.NativeArray<{inner}>{arraySuffix}";
            }

            if (Regex.IsMatch(baseName, @"(^|\.)NativeText_1$")) return "object";

            if (baseName == "List" || Regex.IsMatch(baseName, @"(^|\.)(List_1|IEnumerable_1|IReadOnlyList_1|IList_1)$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.List<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)HashSet_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.HashSet<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)Queue_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.Queue<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)Stack_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.Stack<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)LinkedList_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.LinkedList<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)LinkedListNode_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.LinkedListNode<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)IEnumerator_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.IEnumerator<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)IReadOnlyCollection_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.IReadOnlyCollection<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)ICollection_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.ICollection<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)KeyValuePair_2$"))
            {
                if (argParts.Count < 2 || AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.KeyValuePair<{A(0)}, {A(1)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)ArraySegment_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.ArraySegment<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)Dictionary_2$"))
            {
                if (argParts.Count < 2 || AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.Dictionary<{A(0)}, {A(1)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)IReadOnlyDictionary_2$"))
            {
                if (argParts.Count < 2 || AnyArgBad()) return "object";
                return $"global::System.Collections.Generic.IReadOnlyDictionary<{A(0)}, {A(1)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)Nullable_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"{A(0)}?{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)Action(_\d+)?$"))
            {
                if (AnyArgBad()) return "global::System.Action";
                return argParts.Count == 0 ? "global::System.Action" : $"global::System.Action<{AllArgs()}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)Func(_\d+)?$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Func<{AllArgs()}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)EventHandler_1$"))
            {
                if (AnyArgBad()) return "global::System.EventHandler";
                return $"global::System.EventHandler<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)Task_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Threading.Tasks.Task<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)TaskAwaiter_1$"))
            {
                if (AnyArgBad()) return "object";
                return $"global::System.Runtime.CompilerServices.TaskAwaiter<{A(0)}>{arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)AsyncOperationHandle_1$")) return "object";
            if (Regex.IsMatch(baseName, @"(^|\.)ValueTuple_\d+$"))
            {
                if (AnyArgBad()) return "object";
                return argParts.Count == 1 ? A(0) : $"({AllArgs()}){arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)Tuple_\d+$"))
            {
                if (AnyArgBad()) return "object";
                return argParts.Count == 1 ? A(0) : $"({AllArgs()}){arraySuffix}";
            }
            if (Regex.IsMatch(baseName, @"(^|\.)ValuePair_\d+$"))
            {
                if (AnyArgBad()) return "object";
                return argParts.Count == 1 ? A(0) : $"({AllArgs()}){arraySuffix}";
            }

            if (Regex.IsMatch(baseName, @"(^|\.)Message_1$") ||
                Regex.IsMatch(baseName, @"(^|\.)RangeValuePair_\d+$") ||
                Regex.IsMatch(baseName, @"(^|\.)IntervalTreeNode_\d+$") ||
                Regex.IsMatch(baseName, @"(^|\.)PacketPool_\d+$") ||
                Regex.IsMatch(baseName, @"(^|\.)NativeArray_1_ReadOnly$"))
                return "object";

            if (Regex.IsMatch(baseName, @"_\d{1,2}$"))
                return "object";

            if (AnyArgBad()) return "object";

            var resolvedBase = ResolveNestedType(baseName, currentNs);
            if (resolvedBase.StartsWith("global::")) return $"{resolvedBase}{arraySuffix}";

            var safeArgs = argParts.Select(a => MapCppTypeToCs(a.Trim(), currentNs)).ToList();
            safeArgs = safeArgs.Select(a =>
                Regex.IsMatch(a, @"^[A-Z][A-Za-z0-9]{0,1}$") ? "object" : a).ToList();

            return $"{resolvedBase}<{string.Join(", ", safeArgs)}>{arraySuffix}";
        }

        if (cppType.StartsWith("System.") || cppType.StartsWith("Microsoft."))
            return $"global::{cppType}{arraySuffix}";
        if (cppType.StartsWith("UnityEngine.")) return $"{cppType}{arraySuffix}";
        if (cppType.StartsWith("Unity.") || cppType.StartsWith("Mono."))
            return $"global::{cppType}{arraySuffix}";

        var prim = cppType switch
        {
            "void" => "void",
            "bool" => "bool",
            "int8_t" or "int8" => "sbyte",
            "uint8_t" or "uint8" or "unsigned char" => "byte",
            "int16_t" or "int16" or "short" => "short",
            "uint16_t" or "uint16" or "unsigned short" => "ushort",
            "int32_t" or "int32" or "int" => "int",
            "uint32_t" or "uint32" or "unsigned int" => "uint",
            "int64_t" or "int64" or "long long" => "long",
            "uint64_t" or "uint64" or "unsigned long long" => "ulong",
            "float" or "float_t" => "float",
            "double" or "double_t" => "double",
            "char" or "char16_t" or "char32_t" or "wchar_t" => "char",
            "std::string" or "std::wstring" or "StringW" or "String" => "string",
            "std::vector" => "global::System.Collections.Generic.List<object>",
            "size_t" => "nuint",
            "intptr_t" => "nint",
            "uintptr_t" => "nuint",
            "" => "object",
            "Il2CppObject" => "object",
            "Il2CppClass" => "object",
            "Il2CppType" => "global::System.Type",
            "BigInteger" => "global::System.Numerics.BigInteger",
            "Color" => "UnityEngine.Color",
            "Color32" => "UnityEngine.Color32",
            "Vector2" => "UnityEngine.Vector2",
            "Vector3" => "UnityEngine.Vector3",
            "Vector4" => "UnityEngine.Vector4",
            "Quaternion" => "UnityEngine.Quaternion",
            "Rect" => "UnityEngine.Rect",
            "Bounds" => "UnityEngine.Bounds",
            "Matrix4x4" => "UnityEngine.Matrix4x4",
            "RangeInt" => "UnityEngine.RangeInt",
            "RectInt" => "UnityEngine.RectInt",
            "GameObject" => "UnityEngine.GameObject",
            "Object" => "UnityEngine.Object",
            "List" => "global::System.Collections.Generic.List<object>",
            "Array" => "global::System.Array",
            _ => null
        };

        if (prim != null) return prim + arraySuffix;

        return ResolveNestedType(cppType, currentNs) + arraySuffix;
    }

    private string ResolveNestedType(string typeName, string currentNs)
    {
        if (typeName.StartsWith("System.") || typeName.StartsWith("UnityEngine.") || typeName.StartsWith("Unity."))
            return $"global::{typeName}";

        if (typeName.Contains('.') && !typeName.StartsWith("global::"))
            return "object";

        var underIdx = typeName.IndexOf('_');
        if (underIdx > 0 && char.IsLetter(typeName[0]))
        {
            var parentName = typeName.Substring(0, underIdx);
            var nestedName = typeName.Substring(underIdx + 1);

            if (_types.Keys.Any(k => k.typeName == parentName) || InlineTypes.Contains(parentName))
                return $"{parentName}.{nestedName}";

            return "object";
        }

        return typeName;
    }

    private bool IsValidDelegateType(string type)
    {
        if (string.IsNullOrEmpty(type)) return false;
        if (type.StartsWith("global::System.Action") || type.StartsWith("global::System.Func") ||
            type.StartsWith("global::System.EventHandler")) return true;
        if (type.EndsWith("Handler") || type.EndsWith("Callback") || type.EndsWith("Delegate") ||
            type.EndsWith("Action") || type.EndsWith("Func")) return true;
        if (!type.Contains('.') && !type.Contains('<') && !type.EndsWith("Event")) return false;
        return true;
    }

    private string EnsureDelegateType(string type)
    {
        if (string.IsNullOrEmpty(type)) return "global::System.Action";
        if (type.StartsWith("global::System.Action") || type.StartsWith("global::System.Func") ||
            type.StartsWith("global::System.EventHandler"))
            return type;
        if (type.EndsWith("Handler") || type.EndsWith("Callback") || type.EndsWith("Delegate") ||
            type.EndsWith("Event") || type.EndsWith("Action") || type.EndsWith("Func"))
            return type;
        if (type.Contains('<') || type.Contains('.')) return type;
        return "global::System.Action";
    }

    private int FindMatchingAngleBracket(string s, int startIdx)
    {
        int depth = 0, sqDepth = 0;
        for (int i = startIdx; i < s.Length; i++)
        {
            if (s[i] == '[') sqDepth++;
            else if (s[i] == ']') { if (sqDepth > 0) sqDepth--; }
            else if (sqDepth == 0)
            {
                if (s[i] == '<') depth++;
                else if (s[i] == '>')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
        }
        return -1;
    }

    private static string EscapeComment(string s)
        => s.Replace("*/", "* /").Replace("--", "- -");


    private static void WriteHeader(StreamWriter w)
    {
        w.WriteLine("using System;");
        w.WriteLine("using System.Collections.Generic;");
        w.WriteLine();
        w.WriteLine("namespace GeneratedTypes");
        w.WriteLine("{");
    }

    private static void WriteInlineNamespaces(StreamWriter w)
    {
        w.WriteLine("    namespace UnityEngine");
        w.WriteLine("    {");
        w.WriteLine("        public struct Vector2 { public float x, y; }");
        w.WriteLine("        public struct Vector3 { public float x, y, z; }");
        w.WriteLine("        public struct Vector4 { public float x, y, z, w; }");
        w.WriteLine("        public struct Quaternion { public float x, y, z, w; }");
        w.WriteLine("        public struct Color { public float r, g, b, a; }");
        w.WriteLine("        public struct Color32 { public byte r, g, b, a; }");
        w.WriteLine("        public struct Rect { public float x, y, width, height; }");
        w.WriteLine("        public struct Bounds { public Vector3 center, size; }");
        w.WriteLine("        public struct Matrix4x4 { }");
        w.WriteLine("        public struct RangeInt { public int start, length; }");
        w.WriteLine("        public struct RectInt { public int x, y, width, height; }");
        w.WriteLine("        public struct RectIntPosition { public int x, y; }");
        w.WriteLine("        public class Object { }");
        w.WriteLine("        public class GameObject : Object { }");
        w.WriteLine("        public class Transform : Object { }");
        w.WriteLine("        public class Component : Object { }");
        w.WriteLine("        public class MonoBehaviour : Object { }");
        w.WriteLine("        public class ScriptableObject : Object { }");
        w.WriteLine("        public class Behaviour : Object { }");
        w.WriteLine("        public class Material : Object { }");
        w.WriteLine("        public class Texture : Object { }");
        w.WriteLine("        public class Texture2D : Texture { }");
        w.WriteLine("        public class Shader : Object { }");
        w.WriteLine("        public class Mesh : Object { }");
        w.WriteLine("        public class AnimationClip : Object { }");
        w.WriteLine("        public class Animator : Object { }");
        w.WriteLine("        public class AudioSource : Object { }");
        w.WriteLine("        public class Camera : Object { }");
        w.WriteLine("        public class Light : Object { }");
        w.WriteLine("        public class ParticleSystem : Object { }");
        w.WriteLine("        public class Rigidbody : Object { }");
        w.WriteLine("        public class Collider : Object { }");
        w.WriteLine("        public class MeshRenderer : Object { }");
        w.WriteLine("        public class SkinnedMeshRenderer : Object { }");
        w.WriteLine("        public class RectTransform : Transform { }");
        w.WriteLine("        public struct Mathf");
        w.WriteLine("        {");
        w.WriteLine("            public static float PI => default;");
        w.WriteLine("            public static float Epsilon => default;");
        w.WriteLine("            public static float Sin(float f) => default;");
        w.WriteLine("            public static float Cos(float f) => default;");
        w.WriteLine("            public static float Tan(float f) => default;");
        w.WriteLine("            public static float Abs(float f) => default;");
        w.WriteLine("            public static int Abs(int f) => default;");
        w.WriteLine("            public static float Min(float a, float b) => default;");
        w.WriteLine("            public static float Max(float a, float b) => default;");
        w.WriteLine("            public static int Min(int a, int b) => default;");
        w.WriteLine("            public static int Max(int a, int b) => default;");
        w.WriteLine("            public static float Clamp(float value, float min, float max) => default;");
        w.WriteLine("            public static int Clamp(int value, int min, int max) => default;");
        w.WriteLine("            public static float Clamp01(float value) => default;");
        w.WriteLine("            public static float Lerp(float a, float b, float t) => default;");
        w.WriteLine("            public static float LerpUnclamped(float a, float b, float t) => default;");
        w.WriteLine("            public static float InverseLerp(float a, float b, float value) => default;");
        w.WriteLine("            public static float Sqrt(float f) => default;");
        w.WriteLine("            public static float Pow(float f, float p) => default;");
        w.WriteLine("            public static int RoundToInt(float f) => default;");
        w.WriteLine("            public static int FloorToInt(float f) => default;");
        w.WriteLine("            public static int CeilToInt(float f) => default;");
        w.WriteLine("            public static float Round(float f) => default;");
        w.WriteLine("            public static float Floor(float f) => default;");
        w.WriteLine("            public static float Ceil(float f) => default;");
        w.WriteLine("        }");
        w.WriteLine("        public struct Time");
        w.WriteLine("        {");
        w.WriteLine("            public static float time => default;");
        w.WriteLine("            public static float deltaTime => default;");
        w.WriteLine("            public static float fixedDeltaTime => default;");
        w.WriteLine("            public static float fixedTime => default;");
        w.WriteLine("            public static float timeScale { get; set; }");
        w.WriteLine("            public static int frameCount => default;");
        w.WriteLine("        }");
        w.WriteLine("        public struct Debug");
        w.WriteLine("        {");
        w.WriteLine("            public static void Log(object message) { }");
        w.WriteLine("            public static void LogWarning(object message) { }");
        w.WriteLine("            public static void LogError(object message) { }");
        w.WriteLine("        }");
        w.WriteLine("        public struct MathfInternal");
        w.WriteLine("        {");
        w.WriteLine("            public static float FloatMakeNegative(float f) => default;");
        w.WriteLine("            public static float FloatMakePositive(float f) => default;");
        w.WriteLine("        }");
        w.WriteLine("        public struct Pose { public Vector3 position; public Quaternion rotation; }");
        w.WriteLine("        public struct LayerMask { public int value; }");
        w.WriteLine("        public struct AnimationCurve { }");
        w.WriteLine("        public struct Gradient { }");
        w.WriteLine("        public class Terrain : Object { }");
        w.WriteLine("        public class TerrainData : Object { }");
        w.WriteLine("        public class TerrainLayer : Object { }");
        w.WriteLine("        public struct TreeInstance { }");
        w.WriteLine("        public class LODGroup : Object { }");
        w.WriteLine("        public class MeshFilter : Object { }");
        w.WriteLine("        public class RectOffset { public int left, right, top, bottom; }");
        w.WriteLine("        public enum TextureFormat { }");
        w.WriteLine("        public class IntegratedSubsystem { }");
        w.WriteLine("        public class Sprite : Object { }");
        w.WriteLine("        public class RenderTexture : Texture { }");
        w.WriteLine("        public class Renderer : Component { }");
        w.WriteLine("        public class AudioClip : Object { }");
        w.WriteLine("        public class AudioListener : Component { }");
        w.WriteLine("        public class Coroutine { }");
        w.WriteLine("        public class Cubemap : Texture { }");
        w.WriteLine("        public class ComputeBuffer { }");
        w.WriteLine("        public class CanvasGroup : Component { }");
        w.WriteLine("        public class CanvasRenderer : Component { }");
        w.WriteLine("        public class CapsuleCollider : Collider { }");
        w.WriteLine("        public class Ping { }");
        w.WriteLine("        public class PropertyName { }");
        w.WriteLine("        public class MaterialPropertyBlock { }");
        w.WriteLine("        public struct Ray { }");
        w.WriteLine("        public struct Ray2D { }");
        w.WriteLine("        public struct RaycastHit { }");
        w.WriteLine("        public struct Plane { }");
        w.WriteLine("        public enum KeyCode { }");
        w.WriteLine("        public enum LogType { }");
        w.WriteLine("        public enum SystemLanguage { }");
        w.WriteLine("        public enum ThreadPriority { }");
        w.WriteLine("        public enum HumanBodyBones { }");
        w.WriteLine("        public enum FFTWindow { }");
        w.WriteLine("        public struct Camera_StereoscopicEye { }");
        w.WriteLine("        public struct Vector2Int { public int x, y; }");
        w.WriteLine("        public struct Vector3Int { public int x, y, z; }");
        w.WriteLine("        public class TextAsset : Object { }");
        w.WriteLine("        public class Cloth : Component { }");
        w.WriteLine("        public enum TextAnchor { }");
        w.WriteLine("        public enum TextAlignment { }");
        w.WriteLine("        public class AsyncOperation { }");
        w.WriteLine("        public enum RenderTextureFormat { }");
        w.WriteLine("        public enum RenderTextureReadWrite { }");
        w.WriteLine("        public enum RenderingPath { }");
        w.WriteLine("        public class Mesh_MeshData { }");
        w.WriteLine("        public enum CubemapFace { }");
        w.WriteLine("        public class WaitUntil { }");
        w.WriteLine("        public class AudioType { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.XR");
        w.WriteLine("    {");
        w.WriteLine("        public class XRNode { }");
        w.WriteLine("        public class XRSettings { }");
        w.WriteLine("        public class XRDevice { }");
        w.WriteLine("        public class XRDisplaySubsystem { }");
        w.WriteLine("        public class XRMeshSubsystem { }");
        w.WriteLine("        public class InputDevice { }");
        w.WriteLine("        public class InputDeviceCharacteristics { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.Android");
        w.WriteLine("    {");
        w.WriteLine("        public class Permission { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.EventSystems");
        w.WriteLine("    {");
        w.WriteLine("        public class EventSystem : UnityEngine.Object { }");
        w.WriteLine("        public class BaseEventData { }");
        w.WriteLine("        public class PointerEventData { }");
        w.WriteLine("        public class RaycastResult { }");
        w.WriteLine("        public enum PointerEventData_InputButton { }");
        w.WriteLine("        public class OVRPhysicsRaycaster { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.InputSystem");
        w.WriteLine("    {");
        w.WriteLine("        public struct InputControl { }");
        w.WriteLine("        public struct InputAction { }");
        w.WriteLine("        public class Controls { }");
        w.WriteLine("        public class XRInputSubsystem { }");
        w.WriteLine("        public enum Key { }");
        w.WriteLine("        public class InputDevice { }");
        w.WriteLine("        public enum InputDeviceChange { }");
        w.WriteLine("        public struct InputAction_CallbackContext { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.Timeline");
        w.WriteLine("    {");
        w.WriteLine("        public class ClipCaps { }");
        w.WriteLine("        public class TimelineAsset : UnityEngine.Object { }");
        w.WriteLine("        public class TrackAsset : UnityEngine.Object { }");
        w.WriteLine("        public class TimelineClip { }");
        w.WriteLine("        public class ActivationTrack : TrackAsset { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.Playables");
        w.WriteLine("    {");
        w.WriteLine("        public class PlayableGraph { }");
        w.WriteLine("        public struct Playable { }");
        w.WriteLine("        public class PlayableDirector : UnityEngine.Object { }");
        w.WriteLine("        public class FrameData { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.AddressableAssets");
        w.WriteLine("    {");
        w.WriteLine("        public class AssetReferenceGameObject : UnityEngine.Object { }");
        w.WriteLine("        public class Addressables { }");
        w.WriteLine("        public struct AsyncOperationHandle<T> { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.ResourceManagement");
        w.WriteLine("    {");
        w.WriteLine("        public class AsyncOperation { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.ResourceManagement.ResourceLocations");
        w.WriteLine("    {");
        w.WriteLine("        public interface IResourceLocation { }");
        w.WriteLine("        public class ResourceLocationBase { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.ResourceManagement.ResourceProviders");
        w.WriteLine("    {");
        w.WriteLine("        public class AssetBundleProvider { }");
        w.WriteLine("        public class BundledAssetProvider { }");
        w.WriteLine("        public class SceneProvider { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.SceneManagement");
        w.WriteLine("    {");
        w.WriteLine("        public class Scene { }");
        w.WriteLine("        public enum LoadSceneMode { Single, Additive }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.UI");
        w.WriteLine("    {");
        w.WriteLine("        public class Image : UnityEngine.Object { }");
        w.WriteLine("        public class Text : UnityEngine.Object { }");
        w.WriteLine("        public class Button : UnityEngine.Object { }");
        w.WriteLine("        public class Toggle : UnityEngine.Object { }");
        w.WriteLine("        public class Slider : UnityEngine.Object { }");
        w.WriteLine("        public class ScrollRect : UnityEngine.Object { }");
        w.WriteLine("        public class Canvas : UnityEngine.Object { }");
        w.WriteLine("        public class Graphic : UnityEngine.Object { }");
        w.WriteLine("        public class GraphicRaycaster : UnityEngine.Object { }");
        w.WriteLine("        public class Selectable : UnityEngine.Component { }");
        w.WriteLine("        public class MaskableGraphic : UnityEngine.Component { }");
        w.WriteLine("        public class InputField : UnityEngine.Component { }");
        w.WriteLine("        public class CanvasUpdate { }");
        w.WriteLine("        public interface ICanvasElement { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.Networking");
        w.WriteLine("    {");
        w.WriteLine("        public class NetworkConnection { }");
        w.WriteLine("        public class NetworkIdentity : UnityEngine.Object { }");
        w.WriteLine("        public class NetworkBehaviour : UnityEngine.Object { }");
        w.WriteLine("        public class UnityWebRequest { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.Rendering");
        w.WriteLine("    {");
        w.WriteLine("        public struct BuiltinRenderTextureType { }");
        w.WriteLine("        public struct RenderBuffer { }");
        w.WriteLine("        public enum CameraEvent { }");
        w.WriteLine("        public class CommandBuffer { }");
        w.WriteLine("        public enum ColorWriteMask { }");
        w.WriteLine("        public struct ScriptableRenderContext { }");
        w.WriteLine("        public enum TextureDimension { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.AddressableAssets");
        w.WriteLine("    {");
        w.WriteLine("        public class AssetReferenceGameObject : UnityEngine.Object { }");
        w.WriteLine("        public class Addressables { }");
        w.WriteLine("        public struct AsyncOperationHandle<T> { }");
        w.WriteLine("        public class AssetReference { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.TextCore");
        w.WriteLine("    {");
        w.WriteLine("        public struct TMP_FontAsset { }");
        w.WriteLine("        public class FaceInfo { }");
        w.WriteLine("        public class Glyph { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.TextCore.Text");
        w.WriteLine("    {");
        w.WriteLine("        public class TMP_Text { }");
        w.WriteLine("        public class TMP_InputField : UnityEngine.Object { }");
        w.WriteLine("        public class TMP_Dropdown : UnityEngine.Object { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.IMGUIModule");
        w.WriteLine("    {");
        w.WriteLine("        public struct GUIStyle { }");
        w.WriteLine("        public struct GUIContent { }");
        w.WriteLine("        public struct GUILayoutOption { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.SpriteMaskModule");
        w.WriteLine("    {");
        w.WriteLine("        public class SpriteMask : UnityEngine.Object { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.TerrainModule");
        w.WriteLine("    {");
        w.WriteLine("        public struct SplatPrototype { }");
        w.WriteLine("        public struct TreePrototype { }");
        w.WriteLine("        public struct DetailPrototype { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.WindModule");
        w.WriteLine("    {");
        w.WriteLine("        public class WindZone : UnityEngine.Object { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.VirtualTexturingModule");
        w.WriteLine("    {");
        w.WriteLine("        public struct VirtualTexturingSettings { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.UnityAnalyticsModule");
        w.WriteLine("    {");
        w.WriteLine("        public struct AnalyticsResult { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.UnityWebRequestModule");
        w.WriteLine("    {");
        w.WriteLine("        public class UnityWebRequest { }");
        w.WriteLine("        public class UnityWebRequestAsyncOperation { }");
        w.WriteLine("        public class DownloadHandler { }");
        w.WriteLine("        public class UploadHandler { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.UnityWebRequestAssetBundleModule");
        w.WriteLine("    {");
        w.WriteLine("        public class UnityWebRequestAssetBundle { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.UnityWebRequestAudioModule");
        w.WriteLine("    {");
        w.WriteLine("        public class UnityWebRequestAudio { }");
        w.WriteLine("        public class DownloadHandlerAudioClip { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.UnityWebRequestTextureModule");
        w.WriteLine("    {");
        w.WriteLine("        public class UnityWebRequestTexture { }");
        w.WriteLine("        public class DownloadHandlerTexture { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.UnityWebRequestWWWModule");
        w.WriteLine("    {");
        w.WriteLine("        public class WWW { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.AIModule");
        w.WriteLine("    {");
        w.WriteLine("        public class NavMeshAgent : UnityEngine.Object { }");
        w.WriteLine("        public class NavMeshData : UnityEngine.Object { }");
        w.WriteLine("        public struct NavMeshHit { }");
        w.WriteLine("        public struct NavMeshQueryFilter { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.SubsystemsModule");
        w.WriteLine("    {");
        w.WriteLine("        public class Subsystem { }");
        w.WriteLine("        public class SubsystemDescriptor { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.SpriteShapeModule");
        w.WriteLine("    {");
        w.WriteLine("        public class SpriteShapeController : UnityEngine.Object { }");
        w.WriteLine("        public class SpriteShapeRenderer : UnityEngine.Object { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace UnityEngine.StreamingModule");
        w.WriteLine("    {");
        w.WriteLine("        public struct StreamingController { }");
        w.WriteLine("    }");
        w.WriteLine("    namespace Unity.Collections");
        w.WriteLine("    {");
        w.WriteLine("        public struct NativeArray<T> where T : struct { public int Length => default; public T this[int index] { get => default; set { } } }");
        w.WriteLine("        public struct NativeList<T> where T : struct { public int Length => default; public T this[int index] { get => default; set { } } }");
        w.WriteLine("        public struct NativeHashMap<TKey, TValue> where TKey : struct, IEquatable<TKey> where TValue : struct { }");
        w.WriteLine("        public struct NativeQueue<T> where T : struct { }");
        w.WriteLine("        public struct NativeReference<T> where T : struct { }");
        w.WriteLine("    }");
        w.WriteLine();
    }
}


class TypeData
{
    public string Namespace { get; set; } = "";
    public string TypeName { get; set; } = "";
    public List<FieldInfo> Fields { get; set; } = new();
    public List<MethodInfo> Methods { get; set; } = new();
}

class FieldInfo
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
}

class MethodInfo
{
    public string ReturnType { get; set; } = "";
    public string Name { get; set; } = "";
    public List<ParameterInfo> Parameters { get; set; } = new();
}

class ParameterInfo
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
}

static class KeywordHelper
{
    private static readonly HashSet<string> Keywords = new()
    {
        "object", "string", "int", "bool", "float", "double", "long", "short", "byte", "char",
        "void", "null", "true", "false", "typeof", "sizeof", "nameof", "default", "new",
        "class", "struct", "interface", "enum", "delegate", "event", "readonly", "const",
        "static", "sealed", "abstract", "virtual", "override", "extern", "unsafe", "volatile",
        "public", "private", "protected", "internal", "async", "await", "lock", "using",
        "checked", "unchecked",
        "return", "break", "continue", "goto", "throw", "try", "catch", "finally",
        "if", "else", "switch", "case", "for", "foreach", "while", "do",
        "in", "is", "as", "out", "ref", "params", "this", "base", "operator", "implicit", "explicit",
        "fixed", "stackalloc", "where", "when", "from", "select", "group", "into",
        "orderby", "join", "let", "on", "equals", "by", "ascending", "descending",
        "add", "remove", "get", "set", "value", "global", "partial", "yield", "var",
        "dynamic", "record", "with", "init", "nint", "nuint", "not", "and", "or",
        "type", "assembly", "field", "method", "module", "param", "property", "typevar"
    };

    public static string EscapeKeyword(string name)
        => Keywords.Contains(name) ? "@" + name : name;

    public static bool IsKeyword(string name) => Keywords.Contains(name);
}
*/