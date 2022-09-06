cmake_minimum_required (VERSION 3.5 FATAL_ERROR)
project (clogexamples)

add_subdirectory(.. buildclog)

LIST(APPEND CMAKE_PROGRAM_PATH  "${PROJECT_BINARY_DIR}/buildclog/artifacts")
set(CLOG_EXE  "${PROJECT_BINARY_DIR}/buildclog/artifacts/clog${CMAKE_EXECUTABLE_SUFFIX}")

set(CMAKE_CLOG_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/bld/clog)
set(CMAKE_CLOG_SIDECAR_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR})
set(CLOG_INCLUDE_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/../defaults)
set(CMAKE_CLOG_GENERATE_FILE ${CMAKE_CURRENT_SOURCE_DIR}/../defaults/CLog.cmake)
set(CMAKE_CLOG_CONFIG_FILE ${CMAKE_CURRENT_SOURCE_DIR}/clog_examples.clog_config)
include(${CMAKE_CLOG_GENERATE_FILE})

#
# Allow commandline overrides of the CLOG confguration profile;  but if unspecified
#    use a reasonable choice per OS
#
if ("${CMAKE_CLOG_CONFIG_PROFILE}" STREQUAL "")
    if (WIN32)
        set(CMAKE_CLOG_CONFIG_PROFILE windows)
    elseif (APPLE)
        set(CMAKE_CLOG_CONFIG_PROFILE macos)
    elseif (UNIX)
        set(CMAKE_CLOG_CONFIG_PROFILE linux)
    endif()
endif()

add_subdirectory (clogsample)
