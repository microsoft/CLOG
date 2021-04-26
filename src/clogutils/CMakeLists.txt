# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

cmake_minimum_required(VERSION 3.10)

project(clogUtils)

set(SOURCES
    CLogConsoleTrace.cs
    CLogCustomTraceEmittorFactory.cs
    CLogDecodedTraceLine.cs
    CLogEncodingCLogTypeSearch.cs
    CLogEncodingType.cs
    CLogEnterReadOnlyModeException.cs
    CLogErrors.cs
    CLogFileProcessor.cs
    CLogFullyDecodedMacroEmitter.cs
    CLogHandledException.cs
    CLogLineMatch.cs
    CLogSidecar.cs
    CLogTypeNotFoundException.cs
    CLogTypeSearchNode.cs
    ICLogOutputModule.cs)

DOT_NET_LIBRARY_BUILD(CLOGUTILS_DLL clogutils.dll ${CMAKE_CURRENT_SOURCE_DIR}/clogutils.csproj ${SOURCES})
