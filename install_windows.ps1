# Windows installation script. Will save program files and add to path,
# so you can run citelink and citelink mypdf.pdf from either cmd or
# powershell.

# If you want a different name for the command, change $commandName.

# NOTE: if you run this script again (when $destination has already
# been added to PATH), it will remove $destination from PATH. Simply run the script
# again and $destination will be re-added to PATH.

$destination = "C:\Program Files\CiteLinkChanger"
$commandName = "citelink"
# make new directory for program files
if (!(Test-Path $destination)) {
    New-Item -ItemType Directory -Path $destination
}
# copy from repo to new directory
Copy-Item -Path ".\cite_link_changer_dotnet\*" -Destination $destination -Force
# if citelink is already installed, we only want to update files as we did above, and exit
if (Get-Command $commandName -ErrorAction SilentlyContinue) {
    Write-Host "$commandName command found, update complete!"
    exit
}
# add to PATH
$env:Path += ";$destination"
[Environment]::SetEnvironmentVariable("Path", $env:Path, [System.EnvironmentVariableTarget]::Machine)
# create command by creating batch file simply calling the executable
New-Item -ItemType File -Path "$env:ProgramFiles\CiteLinkChanger\$commandName.bat" -Value "@echo off`r`n`"$env:ProgramFiles\CiteLinkChanger\cite-link-changer.exe`" %*"