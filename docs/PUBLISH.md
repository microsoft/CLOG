#example dot net command to publish a self contained clog
dotnet publish ./clog.sln/ -o ~/clog_release -c Release --self-contained true -r linux-x64
