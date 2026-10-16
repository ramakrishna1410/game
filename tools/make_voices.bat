@echo off
rem Makes placeholder Tamil voices for every story line (needs Python and internet).
set /p PROJECT="Drag your Unity project folder here (e.g. E:\UnityProjects\MadrasGethu) and press Enter: "
pip install --quiet edge-tts
python "%~dp0make_voices.py" %PROJECT%
pause
