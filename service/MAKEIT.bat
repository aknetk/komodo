make
:CHECKFOLDER
@SLEEP 1
@IF NOT EXIST Z:\ GOTO CHECKFOLDER
@copy /Y cortex.nro Z:\switch\cortex.nro
pause

