using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;

namespace Roslyn.CodeDom
{
    public sealed class RoslynCodeDomProvider : CodeDomProvider
    {
        public TargetFramework TargetFramework { get; }

        public RoslynCodeDomProvider(TargetFramework targetFramework)
        {
            TargetFramework = targetFramework;
        }

        [Obsolete("Callers should not use the ICodeCompiler interface and should instead use the methods directly on the CodeDomProvider class.")]
        public override ICodeCompiler CreateCompiler() => new RoslynCodeCompiler(TargetFramework);

        [Obsolete]
        public override ICodeGenerator CreateGenerator() => new CSharpCodeProvider().CreateGenerator();
    }
}
