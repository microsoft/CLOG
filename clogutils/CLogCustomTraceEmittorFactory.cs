/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    A stream manages the send and receive queues for application data. This file
    contains the initialization and cleanup functionality for the stream.

--*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using clogutils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
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

        public void CompileSource(string sourceHash, string sourceCode)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            string assemblyName = Path.GetRandomFileName();
            var refPaths = new[] {typeof(object).GetTypeInfo().Assembly.Location, typeof(Console).GetTypeInfo().Assembly.Location, Path.Combine(Path.GetDirectoryName(typeof(GCSettings).GetTypeInfo().Assembly.Location), "System.Runtime.dll")};
            MetadataReference[] references = refPaths.Select(r => MetadataReference.CreateFromFile(r)).ToArray();

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                new[] {syntaxTree},
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            _compiledCode = new MemoryStream();

            EmitResult result = compilation.Emit(_compiledCode);

            if (!result.Success)
            {
                foreach (var d in result.Diagnostics)
                {
                    TraceLine(TraceType.Err, "Compiling customer trace renderer failed");
                    TraceLine(TraceType.Err, d.ToString());
                    TraceLine(TraceType.Err, "--------------------------------------------");
                    TraceLine(TraceType.Err, sourceCode);
                }

                throw new Exception("Unable to compile type converters");
            }

            CustomTypeDecoder = sourceCode;
            _codeAssembly = Assembly.Load(_compiledCode.GetBuffer());
        }

        internal void ConstructFromAssembly(byte[] assembly)
        {
            _codeAssembly = Assembly.Load(assembly);
        }

        public string Decode(CLogEncodingCLogTypeSearch type, IClogEvent value)
        {
            object[] args = new object[1];

            switch (type.EncodingType)
            {
                case CLogEncodingType.UInt32:
                    args[0] = value.AsUInt32;
                    break;

                case CLogEncodingType.Int32:
                    args[0] = value.AsInt32;
                    break;

                case CLogEncodingType.ByteArray:
                    args[0] = value.AsBinary;
                    break;

                default:
                    throw new NotImplementedException("UndefinedType:" + type);
            }

            string customDecoder = type.CustomDecoder;
            string[] bits = customDecoder.Split('.');
            string member = bits[bits.Length - 1];
            customDecoder = customDecoder.Substring(0, customDecoder.Length - member.Length - 1);

            foreach (var v in _codeAssembly.GetTypes())
            {
                Console.WriteLine(v.FullName);
            }

            var newType = _codeAssembly.GetType(customDecoder);
            var instance = _typesInterface = _codeAssembly.CreateInstance(customDecoder);


            if (!_compiledConverterFunctions.ContainsKey(type.CustomDecoder))
            {
                var meth = newType.GetMember(member).FirstOrDefault() as MethodInfo;
                _compiledConverterFunctions[type.CustomDecoder] = meth;
            }

            MethodInfo method = _compiledConverterFunctions[type.CustomDecoder];
            string ret = (string)method.Invoke(_typesInterface, args);
            return ret;
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


        [StructLayout(LayoutKind.Explicit)]
        public struct SocketAddress
        {
            [FieldOffset(0)] public ushort si_family;

            [FieldOffset(2)] public ushort sin_port;

            // IPv4
            [FieldOffset(4)] public ulong S_addr;


            // IPv6
            [FieldOffset(4)] public ulong sin6_flowinfo;

            [FieldOffset(8)] public ulong S_v6Addr1;

            [FieldOffset(16)] public ulong S_v6Addr2;
        }
    }
}
