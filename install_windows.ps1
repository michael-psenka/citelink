# global variables
$exePath = "C:\Program Files\CiteLinkChanger"
$commandName = "citelink"
$commandPath = "$exePath\cite-link-changer.exe"
# create command
$commandScript = "Start-Process -FilePath $commandPath"
$commandScriptBlock = [ScriptBlock]::Create($commandScript)

# copy module files
New-Item -ItemType Directory -Path $exePath -Force

Copy-Item -Path ".\cite_link_changer_dotnet\*" -Destination "$exePath" -Force

# created command files
New-Item -ItemType File -Path "$env:USERPROFILE\Documents\WindowsPowerShell\Modules\$commandName\$commandName.psm1" -Force
Set-Content -Path "$env:USERPROFILE\Documents\WindowsPowerShell\Modules\$commandName\$commandName.psm1" -Value $commandScriptBlock

# add command to profile
Add-Content $Profile "`nfunction citelink { & '$commandPath' }"