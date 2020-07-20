cmake_minimum_required (VERSION 3.5 FATAL_ERROR)
project (clogexamples)

set(CMAKE_CLOG_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/bld/clog)
set(CMAKE_CLOG_BINS_DIRECTORY ${CMAKE_SOURCE_DIR}/artifacts/tools/bin/clog)
set(CMAKE_CLOG_SIDECAR_DIRECTORY ${CMAKE_SOURCE_DIR}/examples/clogsample)
set(CMAKE_CLOG_CONFIG_FILE ${CMAKE_SOURCE_DIR}/clog_examples_linux.clog_config)
set(CLOG_SOURCE_DIRECTORY ${CMAKE_SOURCE_DIR}/..)
set(CLOG_INCLUDE_DIRECTORY ${CLOG_SOURCE_DIRECTORY}/inc)
set(CMAKE_CLOG_CONFIG_PROFILE linux)

include_directories(${CLOG_INCLUDE_DIRECTORY})

add_subdirectory (clogsample)
