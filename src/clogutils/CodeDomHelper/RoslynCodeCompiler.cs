
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Roslyn.CodeDom
{
    public class RoslynCodeCompiler : ICodeCompiler
    {
        public TargetFramework TargetFramework { get; }

        public RoslynCodeCompiler(TargetFramework targetFramework)
        {
            TargetFramework = targetFramework;
        }

        public CompilerResults CompileAssemblyFromSourceBatch(CompilerParameters options, string[] sources)
        {
            var compilation = CSharpCompilation
                .Create(
                    Path.GetFileName(options.OutputAssembly),
                    syntaxTrees: sources.Select(x => CSharpSyntaxTree.ParseText(x)))
                .WithFrameworkReferences(TargetFramework);

            var compilerResults = new CompilerResults(new TempFileCollection());
            AppendDiagnostics(compilation.GetDiagnostics());

            using var fileStream = new FileStream(options.OutputAssembly, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            var emitResult = compilation.Emit(fileStream);
            fileStream.Close();

            if (emitResult.Success)
            {
                compilerResults.NativeCompilerReturnValue = 0;
                if (options.GenerateInMemory)
                {
                    var bytes = File.ReadAllBytes(options.OutputAssembly);
                    compilerResults.CompiledAssembly = Assembly.Load(bytes);
                }
            }
            else
            {
                compilerResults.NativeCompilerReturnValue = 0;
                AppendDiagnostics(emitResult.Diagnostics);
            }

            return compilerResults;

            void AppendDiagnostics(IEnumerable<Diagnostic> diagnostics)
            {
                foreach (var diagnostic in diagnostics)
                {
                    var error = new CompilerError(
                        diagnostic.Location.SourceTree?.FilePath,
                        line: diagnostic.Location.GetLineSpan().StartLinePosition.Line,
                        column: diagnostic.Location.GetLineSpan().StartLinePosition.Character,
                        errorNumber: diagnostic.Id,
                        errorText: diagnostic.GetMessage());
                    compilerResults.Errors.Add(error);
                }
            }
        }

        #region ICodeCompiler

        CompilerResults ICodeCompiler.CompileAssemblyFromDom(CompilerParameters options, CodeCompileUnit compilationUnit)
        {
            throw new System.NotImplementedException();
        }

        CompilerResults ICodeCompiler.CompileAssemblyFromDomBatch(CompilerParameters options, CodeCompileUnit[] compilationUnits)
        {
            throw new System.NotImplementedException();
        }

        CompilerResults ICodeCompiler.CompileAssemblyFromFile(CompilerParameters options, string fileName)
        {
            throw new System.NotImplementedException();
        }

        CompilerResults ICodeCompiler.CompileAssemblyFromFileBatch(CompilerParameters options, string[] fileNames)
        {
            throw new System.NotImplementedException();
        }

        CompilerResults ICodeCompiler.CompileAssemblyFromSource(CompilerParameters options, string source) =>
            CompileAssemblyFromSourceBatch(options, new[] { source });

        CompilerResults ICodeCompiler.CompileAssemblyFromSourceBatch(CompilerParameters options, string[] sources) =>
            CompileAssemblyFromSourceBatch(options, sources);

        #endregion
    }
}
