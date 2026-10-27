@echo off
chcp 65001 >nul
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. Please install .NET Framework 4.x.
  pause
  exit /b 1
)
"%CSC%" /nologo /target:winexe /codepage:65001 /out:"TCP调试助手.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "TCP调试助手.cs"
if errorlevel 1 (
  echo [FAILED] Build error, see messages above.
  pause
  exit /b 1
)
echo [OK] Built: TCP调试助手.exe
pause
