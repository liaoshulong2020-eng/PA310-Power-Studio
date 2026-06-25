cd %~dp0 

@echo off
set a='0'
Wmic OS Get Caption|Find /i "Windows 10">nul&&(set a='1')
if %a% NEQ '0' (goto GOTOWIN10) else (goto GOTOOTHER)
:GOTOWIN10
echo GOTOWIN10
if %processor_architecture%==x86 (devcon.exe remove "USB\VID_04CC&PID_121B") else (devcon_x64.exe remove "USB\VID_04CC&PID_121B")

if %processor_architecture%==x86 (devcon.exe rescan) else (devcon_x64.exe rescan)

if %processor_architecture%==x86 (devcon.exe -r update .\PA300WIN10\PA300.inf "USB\VID_04CC&PID_121B") else (devcon_x64.exe -r update .\PA300WIN10\PA300.inf "USB\VID_04CC&PID_121B")

if %processor_architecture%==x86 (devcon.exe rescan) else (devcon_x64.exe rescan)
exit /B

:GOTOOTHER
echo GOTOWIN7
if %processor_architecture%==x86 (devcon.exe remove "USB\VID_04CC&PID_121B") else (devcon_x64.exe remove "USB\VID_04CC&PID_121B")

if %processor_architecture%==x86 (devcon.exe rescan) else (devcon_x64.exe rescan)

if %processor_architecture%==x86 (devcon.exe -r update .\PA300\PA300.inf "USB\VID_04CC&PID_121B") else (devcon_x64.exe -r update .\PA300\PA300.inf "USB\VID_04CC&PID_121B")

if %processor_architecture%==x86 (devcon.exe rescan) else (devcon_x64.exe rescan)

pause