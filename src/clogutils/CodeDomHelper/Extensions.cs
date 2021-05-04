using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace Roslyn.CodeDom
{
    public static class Extensions
    {
        public static CSharpCompilation WithFrameworkReferences(this CSharpCompilation compilation, TargetFramework targetFramework) =>
            compilation.WithReferenceAssemblies(targetFramework.ToReferenceAssemblyKind());

        public static ReferenceAssemblyKind ToReferenceAssemblyKind(this TargetFramework targetFramework) => targetFramework switch
        {
            TargetFramework.NetStandard20 => ReferenceAssemblyKind.NetStandard20,
            _ => throw new Exception($"Invalid target framework {targetFramework}")
        };

    }
}
