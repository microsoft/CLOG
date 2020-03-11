cmake -s . -B build

cmake --build build


cmake -G "Unix Makefiles" ..
cmake -G "Visual Studio 16 2019" -A x64 ..