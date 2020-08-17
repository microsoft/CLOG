/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    In some cases the human display of a CLOG event will require a type with custom formatting - 
    for example, perhaps a BYTEARRAY needs to be printed in a special way

    This class allows embedded C# to be include within a sidecar, and compiled on demand to do this formating

--*/

using clogutils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Roslyn.CodeDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using static clogutils.CLogConsoleTrace;

namespace clog2text_lttng
{
    public class CLogCustomTraceEmittorFactory
    {
        private readonly Dictionary<string, MethodInfo> _compiledConverterFunctions = new Dictionary<string, MethodInfo>();
        private readonly Dictionary<string, CLogEncodingCLogTypeSearch> _converterFunctions = new Dictionary<string, CLogEncodingCLogTypeSearch>();
        private Assembly _codeAssembly;
        private MemoryStream _compiledCode;

        private object _typesInterface;

        public string CustomTypeDecoder { get; private set; }

        public void SetSourceCode(string sourceCode)
        {
            CustomTypeDecoder = sourceCode;
        }

        public bool Inited()
        {
            return !String.IsNullOrEmpty(CustomTypeDecoder);
        }

        internal void PrepareAssemblyCompileIfNecessary()
        {
            if (null != _codeAssembly)
                return;

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(CustomTypeDecoder);

            string assemblyName = Path.GetRandomFileName();
            var refPaths = new[] { typeof(object).GetTypeInfo().Assembly.Location, typeof(Console).GetTypeInfo().Assembly.Location, Path.Combine(Path.GetDirectoryName(typeof(GCSettings).GetTypeInfo().Assembly.Location), "System.Runtime.dll") };
            MetadataReference[] references = refPaths.Select(r => MetadataReference.CreateFromFile(r)).ToArray();

            Compilation compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                null,
                new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

            compilation = compilation.WithFrameworkReferences(TargetFramework.NetStandard20);

            _compiledCode = new MemoryStream();

            EmitResult result = compilation.Emit(_compiledCode);

            if (!result.Success)
            {
                foreach (var d in result.Diagnostics)
                {
                    TraceLine(TraceType.Err, "Compiling customer trace renderer failed");
                    TraceLine(TraceType.Err, d.ToString());
                    TraceLine(TraceType.Err, "--------------------------------------------");
                    TraceLine(TraceType.Err, CustomTypeDecoder);
                }

                throw new Exception("Unable to compile type converters");
            }

            _codeAssembly = Assembly.Load(_compiledCode.GetBuffer());
        }

        internal void ConstructFromAssembly(byte[] assembly)
        {
            _codeAssembly = Assembly.Load(assembly);
        }

        public bool Decode(CLogEncodingCLogTypeSearch type, IClogEventArg value, CLogLineMatch traceLine, out string decodedValue)
        {
            //
            // Skip custom type decode if there is no decoder loaded
            //
            if (CustomTypeDecoder == null)
            {
                decodedValue = "";
                return true;
            }
            
            //
            // Compiling also caches the assembly
            //
            PrepareAssemblyCompileIfNecessary();

            object[] args = new object[1];

            switch (type.EncodingType)
            {
                case CLogEncodingType.UInt32:
                    args[0] = value.AsUInt32;
                    break;

                case CLogEncodingType.Int32:
                    args[0] = value.AsInt32;
                    break;

                case CLogEncodingType.UInt8:
                    args[0] = (byte)value.AsInt32;
                    break;

                case CLogEncodingType.ByteArray:
                    args[0] = value.AsBinary;
                    break;

                case CLogEncodingType.Pointer:
                    args[0] = value.AsPointer;
                    break;

                default:
                    throw new NotImplementedException("UndefinedType:" + type);
            }

            string customDecoder = type.CustomDecoder;
            string[] bits = customDecoder.Split('.');
            string member = bits[bits.Length - 1];
            customDecoder = customDecoder.Substring(0, customDecoder.Length - member.Length - 1);

            var newType = _codeAssembly.GetType(customDecoder);
            var instance = _typesInterface = _codeAssembly.CreateInstance(customDecoder);
            decodedValue = "ERROR:" + type.CustomDecoder;

            if (!_compiledConverterFunctions.ContainsKey(type.CustomDecoder))
            {
                if (null == newType)
                    return true;

                var meth = newType.GetMember(member).FirstOrDefault() as MethodInfo;
                _compiledConverterFunctions[type.CustomDecoder] = meth;
            }

            MethodInfo method = _compiledConverterFunctions[type.CustomDecoder];
            decodedValue = (string)method.Invoke(_typesInterface, args);
            return false;
        }

        private string MakeStringCSafe(string s)
        {
            s = s.Replace("!", "");
            return s;
        }

        public void AddConverter(CLogEncodingCLogTypeSearch type)
        {
            _converterFunctions[type.DefinationEncoding] = type;
        }
    }
}
