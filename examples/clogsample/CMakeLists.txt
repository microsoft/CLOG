# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Add clog subdirectory inc folder to create CLOG target
add_subdirectory(../../inc)

# Include Files that need CLOG processing
include("${CLOG_INCLUDE_DIRECTORY}/CLog.make")

set(SOURCES
    simple.cpp
)

#
# Create a clog target for all the source files. This target is then used
# as a normal cmake target dependency
#
CLOG_GENERATE_TARGET(CLOGSAMPLE_LIB ${SOURCES})

add_executable (clogsample ${SOURCES})
target_link_libraries(clogsample CLOGSAMPLE_LIB)
