@echo off
rem One-time setup for the OFFLINE voice engines (IndicF5 and Indic Parler-TTS by AI4Bharat).
rem Needs: Python 3.10 or 3.11 (python.org, tick "Add Python to PATH") and Git (git-scm.com).
echo.
echo  Offline AI voices - one-time setup (downloads about 3 GB of Python packages)
echo.
set /p GPU="Do you have an NVIDIA graphics card (y/n)? "
python -m pip install --upgrade pip
if /i "%GPU%"=="y" (
  pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu121
) else (
  pip install torch torchaudio
)
pip install soundfile numpy huggingface_hub
pip install git+https://github.com/huggingface/parler-tts.git
pip install git+https://github.com/ai4bharat/IndicF5.git
echo.
echo  The AI4Bharat models need a free Hugging Face account:
echo   1. Sign up at https://huggingface.co/join
echo   2. Open https://huggingface.co/ai4bharat/IndicF5 and https://huggingface.co/ai4bharat/indic-parler-tts
echo      and click "Agree and access repository" on each.
echo   3. Make a token at https://huggingface.co/settings/tokens (type: Read) and paste it below.
echo.
huggingface-cli login
echo.
echo  Done. Now run make_voices.bat and choose 4 (IndicF5) or 5 (Parler).
pause
