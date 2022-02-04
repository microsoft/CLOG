# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

cmake_minimum_required(VERSION 3.10)

project(CLOG_EXE)

set(SOURCES
    clog.cs
    CommandLineArguments.cs)

DOT_NET_BUILD(CLOG_EXE clog.exe ${CMAKE_CURRENT_SOURCE_DIR}/clog.csproj net6.0 ${SOURCES})
