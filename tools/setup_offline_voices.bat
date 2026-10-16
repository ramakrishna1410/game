@echo off
setlocal
rem One-time setup for the OFFLINE voice engines (AI4Bharat Indic Parler-TTS and IndicF5).
rem Each engine gets its own Python environment (tools\.venv_parler, tools\.venv_f5) because they
rem need different versions of the same libraries.
rem Needs: Python 3.10 or 3.11 (python.org, "Add Python to PATH") and Git (git-scm.com).
echo.
echo  Offline AI voices - one-time setup
echo   1. Parler  (no recordings needed)
echo   2. IndicF5 (copies voices you record)
echo   3. Both    (needs about 10 GB of disk)
echo.
set /p WHICH="Choose 1, 2 or 3: "
set /p GPU="Do you have an NVIDIA graphics card (y/n)? "
set TORCH=torch torchaudio
set TORCHURL=
if /i "%GPU%"=="y" set TORCHURL=--index-url https://download.pytorch.org/whl/cu121

if "%WHICH%"=="2" goto f5
echo.
echo  === Parler environment ===
python -m venv "%~dp0.venv_parler" || goto nopython
set PY="%~dp0.venv_parler\Scripts\python.exe"
%PY% -m pip install --upgrade pip
%PY% -m pip install %TORCH% %TORCHURL%
%PY% -m pip install git+https://github.com/huggingface/parler-tts.git soundfile
if "%WHICH%"=="1" goto login

:f5
echo.
echo  === IndicF5 environment ===
python -m venv "%~dp0.venv_f5" || goto nopython
set PY="%~dp0.venv_f5\Scripts\python.exe"
%PY% -m pip install --upgrade pip
%PY% -m pip install %TORCH% %TORCHURL%
%PY% -m pip install git+https://github.com/ai4bharat/IndicF5.git soundfile numpy

:login
echo.
echo  The AI4Bharat models need a free Hugging Face account:
echo   1. Sign up at https://huggingface.co/join
echo   2. Open https://huggingface.co/ai4bharat/indic-parler-tts and https://huggingface.co/ai4bharat/IndicF5
echo      and click "Agree and access repository" on each.
echo   3. Make a token at https://huggingface.co/settings/tokens (type: Read) and paste it below.
echo.
%PY% -c "from huggingface_hub import login; login()"
echo.
echo  Done. Now run make_voices.bat and choose 4 (IndicF5) or 5 (Parler).
pause
exit /b

:nopython
echo.
echo  Python wasn't found. Install Python 3.11 from python.org and tick "Add Python to PATH", then run this again.
pause
