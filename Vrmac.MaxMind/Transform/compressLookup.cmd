set SOURCE=%~dp0regionFromCountry.txt
set DEST=%~dp0regionFromCountry.gz
set EXE="C:\Program Files\7-Zip\7z.exe"
%EXE% a -tgzip -mx=9 -mmt=1 -mtm=off %DEST% %SOURCE%