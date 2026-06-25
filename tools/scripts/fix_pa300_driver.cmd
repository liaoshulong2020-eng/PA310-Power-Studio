@echo off
setlocal
set "LOG=%~dp0pa300_driver_fix.log"
echo [%date% %time%] PA300 driver repair started>"%LOG%"
echo Removing incompatible libwdi usbser package...>>"%LOG%"
pnputil /delete-driver oem179.inf /uninstall /force >>"%LOG%" 2>&1
echo Installing ZHIYUAN WinUSB package...>>"%LOG%"
pnputil /add-driver "%~dp0PA300-USB\Drivers\PA300WIN10\pa300.inf" /install >>"%LOG%" 2>&1
echo Forcing device binding...>>"%LOG%"
"%~dp0PA300-USB\Drivers\devcon_x64.exe" update "%~dp0PA300-USB\Drivers\PA300WIN10\pa300.inf" "USB\VID_04CC&PID_121B" >>"%LOG%" 2>&1
pnputil /scan-devices >>"%LOG%" 2>&1
echo [%date% %time%] PA300 driver repair finished>>"%LOG%"
endlocal
