@echo off
setlocal

REM Input and output file names
set input_file=lexicon.pls
set output_file=lexicon.pls

REM If %1 is set, then set the input file to %1
if not "%1" == "" set input_file=%1

REM If %2 is set, then set the output file to %2
if not "%2" == "" set output_file=%2

REM Check if the input file exists
if not exist "%input_file%" (
    echo Input file %input_file% not found!
    exit /b 1
)

REM Call the Python script to perform the sorting
python sort_lexemes.py "%input_file%" "%output_file%"

endlocal

REM Pause so that the user can see the output before the window closes
pause