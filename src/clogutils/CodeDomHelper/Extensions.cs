using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Roslyn.CodeDom.References;

namespace Roslyn.CodeDom
{
    public static class Extensions
    {
        public static Compilation WithFrameworkReferences(this Compilation compilation, TargetFramework targetFramework)
        {
            IEnumerable<MetadataReference> references;
            switch (targetFramework)
            {
                case TargetFramework.NetStandard20:
                    references = NetStandard20.All;
                    break;
                default:
                    throw new InvalidOperationException($"Invalid value: {targetFramework}");
            }

            return compilation.WithReferences(references);
        }
    }
}
