#git clean -fdx .


if(!(Test-Path -Path ./build)) {
    mkdir build
}
cd build
cmake ..
cmake --build .

if ($IsLinux) {
    rm -r -f ./log
    mkdir log
    mkdir log/lttng

    lttng create clogsample -o=log/lttng
    if(!$?)
    {
        Write-Host "LTTNG Create failed"
        Exit -1
    }

    lttng enable-event --userspace CLOG_*
    if(!$?)
    {
        Write-Host "LTTNG enable-event failed"
        Exit -2
    }

    lttng add-context --userspace --type=vpid --type=vtid
    if(!$?)
    {
        Write-Host "LTTNG add-context failed"
        Exit -3
    }
    lttng start
    if(!$?)
    {
        Write-Host "LTTNG start failed"
        Exit -4
    }
}

if($IsWindows) {
    move .\clogsample\Debug\* .\clogsample -Force

    cd .\clogsample
    dir

    wevtutil.exe um clog_examples.man
    wevtutil.exe im clog_examples.man

    if(!$?) {
        Write-Host "Manifest registration failed"
        Exit -5
    } else {
        Write-Host "ETW Manifest installed"
    }

    wpr -start clog_examples.wprp -filemode -instancename clog_example

    if(!$?)
    {
        Write-Host "WPR wouldnt start"
        Exit -6
    }

    cd ..
}

./clogsample/clogsample
if(!$?)
{
    Write-Host "Unable to run test"
    Exit -7
}

if ($IsLinux) {
    lttng list --userspace | grep CLOG
    lttng list --userspace | grep CLOG

    lttng stop clogsample
    lttng destroy clogsample
    babeltrace --names all ./log/lttng/* > ./log/clog.babel

    echo "---------------------------------------"
    ./buildclog/artifacts/clog2text_lttng -i ./log/clog.babel -s ../clog.sidecar > test.output.txt
}

if ($IsWindows)
{
    Write-Host "Stopping WPR in the same directory as the .exe / .man file - because our ETW manifest doesnt specify a direct path for the resource"
    cd ./clogsample
    wpr -stop clog_example.etl -instancename clog_example

    if(!$?)
    {
        Write-Host "WPR stop failed"
        Exit -8
    }

    cd ..

    # Intentionally run the program twice - data layer outputs some info the first time it's run, causing the test output to be wrong :/
    .\buildclog\artifacts\clog2text_windows.exe -i ./clogsample/clog_example.etl -s ../clog.sidecar
    .\buildclog\artifacts\clog2text_windows.exe -i ./clogsample/clog_example.etl -s ../clog.sidecar > test.output.txt
}

$diffs = Compare-Object (Get-Content ./test.output.txt) (Get-Content ../test.desired.output.txt)

Write-Host "----------------------------------------------------"
Write-Host "Results: "

cat ./test.output.txt
if($diffs.Length -ne 0)
{
    Write-Host "----------------------------------------------------"
    Write-Host "FAIL : expected results"
    cat ../test.desired.output.txt

    Write-Host "----------------------------------------------------"
    Write-Host "Diffs : "
    Compare-Object (Get-Content ./test.output.txt) (Get-Content ../test.desired.output.txt)

    cd ..
    Exit -9
}
else
{
    Write-Host "SUCCESS : results look good"
}

cd ..
Exit 0
