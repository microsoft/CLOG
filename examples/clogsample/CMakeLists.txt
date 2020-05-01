# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

#
#
# BUGBUG : these may not be required
#
#
add_custom_target(CLOG_GENERATED_FILES
    COMMENT "CREATED CMAKE Target for CLOG generated files"
)

# Include Files that need CLOG processing
include("${CLOG_INCLUDE_DIRECTORY}/CLog.make")
include_directories(${CMAKE_CLOG_OUTPUT_DIRECTORY})

set(SOURCES
    simple.cpp
)

set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} ${QUIC_C_FLAGS}")

#
# Setup CLOG by calling a helper function which sets up dependencies.  Then add the .c file
#     ...it'd be preferred to have the helper function set this requirement
#
CLOG_ADD_SOURCEFILE(CLOGSAMPLE_LIB ${SOURCES})

add_executable (clogsample ${SOURCES})
add_dependencies(clogsample CLOGSAMPLE_LIB)
target_link_libraries(clogsample CLOGSAMPLE_LIB)