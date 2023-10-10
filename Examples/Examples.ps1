$buildDirectory = ([System.IO.Path]::Combine((Get-Location), "bin\x64\Debug\net7.0")) # change x64 if using a different build target
$buildDirectory = ([System.IO.Path]::Combine((Get-Location), "bin\x64\Release\net7.0"))

Import-Module -Name ([System.IO.Path]::Combine($buildDirectory, "Examples.dll"))

# write and read back a .laz file
#Use-Laz -File ([System.IO.Path]::Combine((Get-Location), "..\TestResults\simpleLoopbackTest.laz"))

# basic performance: get read speed on a .las or .laz file of interest
# Typically this would be some file not included in the repo.
Get-ReadSpeed -File ([System.IO.Path]::Combine((Get-Location), "..\UnitTests\PSME LAS 1.4 point type 6.las")) -Verbose
