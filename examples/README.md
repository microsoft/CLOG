# Prerequisites
Need dotnet core 3.1 or later. Instructions found at https://dotnet.microsoft.com/download

Install latest clog nuget tool. Download from here into nupkg folder https://github.com/microsoft/CLOG/releases

Then run

```
dotnet tool install --global --add-source nupkg Microsoft.Logging.CLOG
```

# Build (using pwsh)
```
mkdir build
clog --installDirectory build/clog
cd build
$env:CLOG_DEVELOPMENT_MODE = 1
cmake ..
cmake --build .
```
