/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Describes a handled exception that comes as a result of a traceline (in your C/C++) that is malformed

--*/

using System;

namespace clogutils
{
    public class CLogHandledException : Exception
    {
        public enum ExceptionType
        {
            TypeException = 1,
            DuplicateMacro = 2,
            MultipleMacrosWithSameName = 3,
            CircularConfigFilesNotAllowed = 4,
            DuplicateId = 5,
            SourceMustIncludeCLOGHeader = 6,
            IncorrectStringAndNumericalEncoding = 7,
            TooFewArguments = 8,
            TooManyArguments = 9,
            InvalidInput = 10,
            UndefinedType = 11,
            UnableToOpenChainedConfigFile = 12,
            UnableToOpenCustomDecoder = 13,
            ByteArrayMustUseMacro = 14,
            TaceIDNotUnique = 15,
            EncoderIncompatibleWithType = 16,
            MustSpecifiyETWManifest = 17,
            MustSpecifyETWProvider = 18,
            ManifestedETWProviderNotFound = 19,
            ETWManifestNotFound = 20,
            ETWOutOfUniqueIDs = 21,
            ETWTypeMismatch = 22,
            CustomTypeDecoderNotFound = 23,
            InvalidUniqueId = 24,
            WontWriteInReadOnlyMode = 25,
            RequiredConfigParameterUnspecified = 26
        }

        public static string TranslateExceptionTypeToErrorMessage(ExceptionType e)
        {
            switch (e)
            {
                case ExceptionType.DuplicateMacro:
                    return "Macro defined twice in chained config files";
                case ExceptionType.MultipleMacrosWithSameName:
                    return "Macro specified mutiple times - you may only specify the macro one time within a config file";
                case ExceptionType.CircularConfigFilesNotAllowed:
                    return "Configs may not contain circular includes";
                case ExceptionType.DuplicateId:
                    return "Duplicate ID's not permitted";
                case ExceptionType.SourceMustIncludeCLOGHeader:
                    return "Source file must include CLOG header file";
                case ExceptionType.IncorrectStringAndNumericalEncoding:
                    return "Unique ID not in the string_num encoding format";
                case ExceptionType.TooFewArguments:
                    return "Too few arguments passed to CLOG macro";
                case ExceptionType.TooManyArguments:
                    return "Too many arguments passed to CLOG macro";
                case ExceptionType.InvalidInput:
                    return "Invalid input";
                case ExceptionType.UndefinedType:
                    return "Type not defined in configuration files";
                case ExceptionType.UnableToOpenChainedConfigFile:
                    return "Unable to opend chained config file";
                case ExceptionType.UnableToOpenCustomDecoder:
                    return "Unable to open custom C# decoder";
                case ExceptionType.ByteArrayMustUseMacro:
                    return "BYTEARRAY type must specify length(in bytes) along with the pointer into the CLOG_BYTEARRAY(len, pointer); macro";
                case ExceptionType.TaceIDNotUnique:
                    return "The encoding string, arg types, etc may not change once specified";
                case ExceptionType.EncoderIncompatibleWithType:
                    return "Encoder is not compatible with type.  Either do not use this type in your code, or consider helping the community by updating the CLOGs encoder to support it";
                case ExceptionType.MustSpecifiyETWManifest:
                    return "ETW Manifest not specified in config file";
                case ExceptionType.MustSpecifyETWProvider:
                    return "ETW Provider not specified in config file";
                case ExceptionType.ManifestedETWProviderNotFound:
                    return "Unable to locate ETW provider in manifest";
                case ExceptionType.ETWManifestNotFound:
                    return "ETW Manifest not found";
                case ExceptionType.ETWOutOfUniqueIDs:
                    return "ETW Manifest contains too many unique ID's;  this is a limitation of ETW that requires either a new provider, or the deletion of unused ID's";
                case ExceptionType.ETWTypeMismatch:
                    return "CLOG defined types mismatch with existing ETW manifest - you must fix the ETW manifest manually to align the types";
                case ExceptionType.InvalidUniqueId:
                    return "CLOG Unique IDs must be alphanumeric";
                case ExceptionType.WontWriteInReadOnlyMode:
                    return "Wont write while in readonly mode.  --readOnly was specified as a command line argument.  If you're in a development mode, you can set the environment CLOG_DEVELOPMENT_MODE such that manifests and sidecars will be automatically updated";
                case ExceptionType.RequiredConfigParameterUnspecified:
                    return "A required configuration parameter was not specified in the configuration file - this will be a user specified option required for a chosen event module";
            }

            return "Uknown Error";
        }

        public CLogHandledException(string msg, ExceptionType type, CLogLineMatch traceLine, Exception e = null) : base(msg)
        {
            Exception = e;
            TraceLine = traceLine;
            Type = type;
        }

        public ExceptionType Type { get; private set; }
        public CLogLineMatch TraceLine { get; set; }

        public string InfoString { get; set; }

        public Exception Exception { get; set; }

        public void PrintDiagnostics()
        {
            CLogErrors.PrintMatchDiagnostic(TraceLine);

            string fileLine = CLogConsoleTrace.GetFileLine(TraceLine);
            CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"{fileLine}: fatal error CLOG{(int)Type}: {TranslateExceptionTypeToErrorMessage(Type)}");

            if (!String.IsNullOrEmpty(InfoString))
                CLogConsoleTrace.TraceLine(CLogConsoleTrace.TraceType.Err, $"   Info : " + InfoString);
        }
    }
}
