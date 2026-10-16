@echo off
rem AI voices for every story line (needs Python and internet). See the top of make_voices.py.
echo.
echo  Madras Gethu - AI voices for the story
echo  ---------------------------------------
echo   1. Gemini     (Google, free API key, acts out each line - recommended)
echo   2. ElevenLabs (most natural, free 10,000 characters a month)
echo   3. Edge       (no key, unlimited, but flat computer voice)
echo   4. IndicF5    (OFFLINE: copies voices you record in tools\voice_refs - most lifelike)
echo   5. Parler     (OFFLINE: voices described in words, no recordings needed)
echo   (for 4 and 5 run setup_offline_voices.bat once first)
echo.
set /p CHOICE="Choose 1-5: "
set ENGINE=gemini
if "%CHOICE%"=="2" set ENGINE=elevenlabs
if "%CHOICE%"=="3" set ENGINE=edge
if "%CHOICE%"=="4" set ENGINE=indicf5
if "%CHOICE%"=="5" set ENGINE=parler
set KEY=
set NEEDKEY=
if "%ENGINE%"=="gemini" set NEEDKEY=1
if "%ENGINE%"=="elevenlabs" set NEEDKEY=1
if defined NEEDKEY (
  if "%ENGINE%"=="gemini" echo Get a free key at https://aistudio.google.com/apikey
  if "%ENGINE%"=="elevenlabs" echo Get your key at elevenlabs.io - Profile - API Keys
  set /p KEY="Paste your API key and press Enter: "
)
set /p PROJECT="Drag your Unity project folder here (e.g. E:\UnityProjects\MadrasGethu) and press Enter: "
set /p REDO="Replace voices made before (y/n)? "
set FORCE=
if /i "%REDO%"=="y" set FORCE=--force
if "%ENGINE%"=="edge" pip install --quiet edge-tts
python "%~dp0make_voices.py" %PROJECT% --engine %ENGINE% --key "%KEY%" %FORCE%
pause
