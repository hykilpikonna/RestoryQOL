using Mono.Cecil;
using Mono.Cecil.Cil;

/// <summary>
/// Generates reference-only stub DLLs from game assemblies.
/// All method bodies are replaced with a minimal return-default body so the
/// project compiles in CI without the full game installation.
///
/// Usage:
///   StubGenerator &lt;input-dll&gt; &lt;output-dir&gt;
///     OR
///   StubGenerator --batch &lt;input-dir&gt; &lt;output-dir&gt; [glob-pattern]
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  StubGenerator <input.dll> <output-dir>");
            Console.Error.WriteLine("  StubGenerator --batch <input-dir> <output-dir> [pattern]");
            return 1;
        }

        bool batch = args[0] == "--batch";

        if (batch)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("--batch requires <input-dir> <output-dir>");
                return 1;
            }

            string inputDir = args[1];
            string outputDir = args[2];
            string pattern = args.Length > 3 ? args[3] : "*.dll";

            Directory.CreateDirectory(outputDir);

            var dlls = Directory.GetFiles(inputDir, pattern, SearchOption.TopDirectoryOnly);
            if (dlls.Length == 0)
            {
                Console.Error.WriteLine($"No files matching '{pattern}' found in '{inputDir}'");
                return 1;
            }

            int errors = 0;
            foreach (var dll in dlls)
            {
                string outPath = Path.Combine(outputDir, Path.GetFileName(dll));
                if (!StubAssembly(dll, outPath))
                    errors++;
            }

            return errors == 0 ? 0 : 2;
        }
        else
        {
            string inputDll = args[0];
            string outputDir = args[1];
            Directory.CreateDirectory(outputDir);
            string outPath = Path.Combine(outputDir, Path.GetFileName(inputDll));
            return StubAssembly(inputDll, outPath) ? 0 : 1;
        }
    }

    static bool StubAssembly(string inputPath, string outputPath)
    {
        try
        {
            Console.WriteLine($"Stubbing: {Path.GetFileName(inputPath)} -> {outputPath}");

            // Allow Cecil to resolve cross-assembly references by searching the
            // input file's directory (e.g. Restory.Assembly.dll → UnityEngine.CoreModule.dll)
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(inputPath)!);

            var readerParams = new ReaderParameters
            {
                ReadSymbols = false,
                ReadWrite = false,
                InMemory = true,
                AssemblyResolver = resolver,
            };

            using var asm = AssemblyDefinition.ReadAssembly(inputPath, readerParams);

            foreach (var module in asm.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    foreach (var method in type.Methods)
                    {
                        if (!method.HasBody)
                            continue;

                        // Replace body with minimal IL: just return default
                        var body = method.Body;
                        body.Instructions.Clear();
                        body.Variables.Clear();
                        body.ExceptionHandlers.Clear();

                        var il = body.GetILProcessor();

                        var returnType = method.ReturnType;

                        if (returnType.FullName == "System.Void")
                        {
                            il.Append(il.Create(OpCodes.Ret));
                        }
                        else if (IsValueType(returnType))
                        {
                            // For value types: push a zeroed local and return it
                            var local = new VariableDefinition(returnType);
                            body.Variables.Add(local);
                            body.InitLocals = true;

                            il.Append(il.Create(OpCodes.Ldloca_S, local));
                            il.Append(il.Create(OpCodes.Initobj, returnType));
                            il.Append(il.Create(OpCodes.Ldloc_0));
                            il.Append(il.Create(OpCodes.Ret));
                        }
                        else
                        {
                            // Reference type: return null
                            il.Append(il.Create(OpCodes.Ldnull));
                            il.Append(il.Create(OpCodes.Ret));
                        }
                    }
                }
            }

            var writerParams = new WriterParameters
            {
                WriteSymbols = false,
            };

            asm.Write(outputPath, writerParams);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR stubbing {inputPath}: {ex.Message}");
            return false;
        }
    }

    static bool IsValueType(TypeReference type)
    {
        // Primitives and known value types
        return type.IsValueType || type.FullName switch
        {
            "System.Boolean" or "System.Byte" or "System.SByte" or
            "System.Int16" or "System.UInt16" or "System.Int32" or
            "System.UInt32" or "System.Int64" or "System.UInt64" or
            "System.Single" or "System.Double" or "System.Char" => true,
            _ => false,
        };
    }
}
