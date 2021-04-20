cmake_minimum_required(VERSION 3.10)

project(CLOG_EXE)

set(SOURCES
    clog.cs
    CommandLineArguments.cs)


DOT_NET_BUILD(CLOG_EXE ${CMAKE_CURRENT_BINARY_DIR}/clog.exe ${CMAKE_CURRENT_SOURCE_DIR}/clog.csproj ${SOURCES})
