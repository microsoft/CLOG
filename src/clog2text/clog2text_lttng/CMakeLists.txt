# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

cmake_minimum_required(VERSION 3.10)

project(CLOG2TEXT_LTTNG)

set(SOURCES
    CommandLineArguments.cs
    Program.cs)


DOT_NET_BUILD(CLOG2TEXT_LTTNG clog2text_lttng.exe ${CMAKE_CURRENT_SOURCE_DIR}/clog2text_lttng.csproj net6.0 ${SOURCES})
