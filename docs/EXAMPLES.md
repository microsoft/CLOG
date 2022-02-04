cmake -s . -B build

cmake --build build


cmake -G "Unix Makefiles" ..

cmake -G "Visual Studio 16 2019" -A x64 ..

dotnet publish ./src/clog/clog.csproj --self-contained -o /home/chgray/.dotnet/tools -f net6.0 -r win-x64
dotnet publish ./src/clog/clog.csproj --self-contained -o /home/chgray/.dotnet/tools -f net6.0 -r linux-x64 -p:PublishReadyToRun=true -p:PublishReadyToRunShowWarnings=true

https://github.com/chgray/msquic/blob/53198319bd1460a04823eb6859b776b964f145f6/docs/BUILD.md
