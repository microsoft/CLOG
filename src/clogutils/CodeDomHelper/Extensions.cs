using System;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis.CSharp;

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
