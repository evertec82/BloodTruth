param([string]$GamePath, [ValidateSet(0,30,60)][int]$MirrorFps=30, [ValidateSet('Original','High','Ultra8x')][string]$Resolution='High', [switch]$NoGui, [switch]$ValidateOnly, [switch]$OriginalMoveOrder)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if (-not $GamePath) { $GamePath=Join-Path $root 'games/CUSA11108/files/uroot/eboot.bin' }
if (-not $NoGui -and -not $ValidateOnly) {
 Add-Type -AssemblyName System.Windows.Forms
 Add-Type -AssemblyName System.Drawing
 [Windows.Forms.Application]::EnableVisualStyles()
 $form=New-Object Windows.Forms.Form
 $form.Text='Blood & Truth PC VR - v0.1.5 prototype'
 $form.ClientSize=New-Object Drawing.Size(580,260)
 $form.StartPosition='CenterScreen'
 $label=New-Object Windows.Forms.Label
 $label.Text='CUSA11108 1.00. Two tracked controllers. Higher resolution is experimental; missing geometry remains under investigation.'
 $label.SetBounds(15,15,550,45); $form.Controls.Add($label)
 $pathBox=New-Object Windows.Forms.TextBox
 $pathBox.Text=$GamePath; $pathBox.SetBounds(15,65,550,24); $form.Controls.Add($pathBox)
 $mirror=New-Object Windows.Forms.ComboBox
 $mirror.DropDownStyle='DropDownList'; $mirror.SetBounds(15,105,300,25)
 [void]$mirror.Items.Add('Desktop mirror: 30 FPS'); [void]$mirror.Items.Add('Desktop mirror: 60 FPS'); [void]$mirror.Items.Add('Desktop mirror: uncapped')
 $mirror.SelectedIndex=[array]::IndexOf(@(30,60,0),$MirrorFps); $form.Controls.Add($mirror)
 $resolutionBox=New-Object Windows.Forms.ComboBox
 $resolutionBox.DropDownStyle='DropDownList'; $resolutionBox.SetBounds(15,145,550,25)
 [void]$resolutionBox.Items.Add('Original - game-controlled resolution')
 [void]$resolutionBox.Items.Add('High - 1440 x 1632 per eye (1.5x each axis)')
 [void]$resolutionBox.Items.Add('Ultra - 8x base pixels (2.83x each axis)')
 $resolutionBox.SelectedIndex=[array]::IndexOf(@('Original','High','Ultra8x'),$Resolution); $form.Controls.Add($resolutionBox)
 $play=New-Object Windows.Forms.Button
 $play.Text='Start Blood & Truth'; $play.SetBounds(380,205,185,32); $play.DialogResult='OK'; $form.Controls.Add($play); $form.AcceptButton=$play
 if ($form.ShowDialog() -ne 'OK') { $form.Dispose(); return }
 $GamePath=$pathBox.Text; $MirrorFps=@(30,60,0)[$mirror.SelectedIndex]; $Resolution=@('Original','High','Ultra8x')[$resolutionBox.SelectedIndex]; $form.Dispose()
}
. (Join-Path $PSScriptRoot 'expand-package.ps1')
# If the game is still a .pkg (given as the path, or found in the games folder when no unpacked
# game exists), unpack it first. A validation run never unpacks anything.
if (-not $ValidateOnly) {
 $gamesFolder=Join-Path $root 'games'
 $package=$null
 if ([System.IO.Path]::GetExtension($GamePath) -ieq '.pkg') { $package=$GamePath }
 elseif (-not (Test-Path -LiteralPath $GamePath)) { $found=Find-GamePackage $gamesFolder; if ($found) { $package=$found.FullName } }
 if ($package) {
  if (-not $NoGui) {
   $answer=[Windows.Forms.MessageBox]::Show("The game is a package:`n`n$package`n`nIt has to be unpacked once before it can be played. Unpack it now?",'Blood & Truth PC VR','YesNo','Question')
   if ($answer -ne 'Yes') { return }
  }
  $GamePath=Expand-GamePackage -PackagePath $package -GamesFolder $gamesFolder -LauncherFolder $PSScriptRoot -Root $root
 }
}
if (-not (Test-Path -LiteralPath $GamePath)) { throw 'The complete extracted CUSA11108 game is required at games/CUSA11108/files/uroot.' }
if ((Get-FileHash -LiteralPath $GamePath).Hash -ne '9312B61DAF0AC023F66203F59BC3563497C26BF6E97199833B9B60CEF14B65F7') { throw 'This prototype requires Blood & Truth CUSA11108 version 1.00.' }
if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $GamePath -Parent) 'sce_sys/param.sfo'))) { throw 'The game metadata is missing.' }
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'user/log') -Force | Out-Null
foreach ($name in @('config','users')) {
 $destination=Join-Path $PSScriptRoot ('user/'+$name+'.json')
 if (-not (Test-Path -LiteralPath $destination)) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot ($name+'-default.json')) -Destination $destination }
}
$exe=Join-Path $PSScriptRoot 'shadps4.exe'
if (-not (Test-Path $exe)) { throw 'Emulator executable is missing.' }
$resolutionHelper=Join-Path $PSScriptRoot 'set-native-resolution.ps1'
if ($Resolution -ne 'Original' -and -not (Test-Path -LiteralPath $resolutionHelper)) { throw 'Native resolution helper is missing.' }
if ($ValidateOnly) { [pscustomobject]@{GamePath=$GamePath;Executable=$exe;MirrorFps=$MirrorFps;Resolution=$Resolution}; return }
if (Get-Process shadps4 -ErrorAction SilentlyContinue) { throw 'Close the running emulator first.' }
$env:SHADPS4_OPENXR='1'; $env:SHADPS4_VR='1'

if (-not $env:SHADPS4_BLOODTRUTH_DMEM_ALIAS) { $env:SHADPS4_BLOODTRUTH_DMEM_ALIAS='1' }
$env:SHADPS4_XR_WAIT='0'; $env:SHADPS4_XR_PAUSE='0'
$env:SHADPS4_XR_GAMEPAD_LAYOUT='1'
$env:SHADPS4_MOVE_SWAP_HANDS=if ($OriginalMoveOrder) { '0' } else { '1' }
$env:SHADPS4_VR_WINDOW_FPS=[string]$MirrorFps
$env:SHADPS4_TITLE_RESOLUTION='title'; $env:SHADPS4_TITLE_EYE_WIDTH=''
$env:SHADPS4_VR_FPS_CAP=''; $env:SHADPS4_VR_PACE='1'
$env:SHADPS4_INPUT_SCRIPT=''; $env:SHADPS4_LIVE_INPUT=''
$env:SHADPS4_XR_TEST_PRESS=''; $env:SHADPS4_XR_TEST_TURN=''
$process=Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -ArgumentList @('-g',('"'+$GamePath+'"')) -WindowStyle Hidden -PassThru
if ($Resolution -ne 'Original') {
 $shell=Join-Path $PSHOME 'powershell.exe'
 if (-not (Test-Path -LiteralPath $shell)) { $shell=Join-Path $PSHOME 'pwsh.exe' }
 $resolutionProcess=Start-Process -FilePath $shell -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+$resolutionHelper+'"'),'-GameProcessId',$process.Id,'-Resolution',$Resolution,'-InstallPath',('"'+$PSScriptRoot+'"')) -WindowStyle Hidden -PassThru
}
Write-Host "Blood & Truth started. Log: $PSScriptRoot\user\log\shad_log.txt"
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Blood & Truth ended with code $($process.ExitCode). See user\log\shad_log.txt." }




