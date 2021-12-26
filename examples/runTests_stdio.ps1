#git clean -fdx .


if(!(Test-Path -Path ./build)) {
    mkdir build
}
cd build
cmake -DCMAKE_CLOG_CONFIG_PROFILE=printf ..
cmake --build .


if($IsWindows) {
    move .\clogsample\Debug\* .\clogsample -Force
}

./clogsample/clogsample > test.output.txt
if(!$?)
{
    Write-Host "Unable to run test"
    cd ..
    Exit
}

$diffs = Compare-Object (Get-Content ./test.output.txt) (Get-Content ../test.desired.output.basic.txt)

Write-Host "----------------------------------------------------"
Write-Host "Results: "

cat ./test.output.txt
if($diffs.Length -ne 0)
{
    Write-Host "----------------------------------------------------"
    Write-Host "FAIL : expected results"
    cat ../test.desired.output.basic.txt

    Write-Host "----------------------------------------------------"
    Write-Host "Diffs : "
    Compare-Object (Get-Content ./test.output.txt) (Get-Content ../test.desired.output.basic.txt)

    cd ..
    Exit -1
}
else
{
    Write-Host "SUCCESS : results look good"
}

cd ..
Exit 0
