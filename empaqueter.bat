@echo off
rem Cree le fichier ZIP a envoyer sur Thunderstore (lance compiler.bat avant).
set "RACINE=%~dp0"
set "DLL=%RACINE%SeulementLesPieces\bin\Release\SeulementLesPieces.dll"
if not exist "%DLL%" goto pasdedll
set "PAQUET=%RACINE%Thunderstore\SeulementLesPieces"
copy /y "%DLL%" "%PAQUET%\SeulementLesPieces.dll" >nul
powershell -NoProfile -Command "Compress-Archive -Force -Path \"%PAQUET%\*\" -DestinationPath \"%RACINE%SeulementLesPieces.zip\""
if errorlevel 1 goto echec
echo OK : le fichier SeulementLesPieces.zip est pret, a cote de ce fichier.
pause
exit /b 0

:pasdedll
echo ECHEC : SeulementLesPieces.dll introuvable. Lance d abord compiler.bat.
pause
exit /b 1

:echec
echo ECHEC : la creation du ZIP a rate, lis le message ci-dessus.
pause
exit /b 1
