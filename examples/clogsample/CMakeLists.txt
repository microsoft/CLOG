# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.


add_custom_target(CLOG_GENERATED_FILES
    COMMENT "CREATED CMAKE Target for CLOG generated files"
)


# Include Files that need CLOG processing
include("../../inc/CLog.make")
include_directories(${CMAKE_CLOG_OUTPUT_DIRECTORY})

set(SOURCES
    simple.cpp
)

#
# Setup CLOG by calling a helper function which sets up dependencies.  Then add the .c file
#     ...it'd be preferred to have the helper function set this requirement
#
CLOG_ADD_SOURCEFILE(CLOGSAMPLE_LIB ${SOURCES})

add_executable (clogsample simple.cpp)

# add_dependencies(clogsample CLOGSAMPLE_LIB)