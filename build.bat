@echo off
setlocal
echo ===================================================
echo               Compilando NotionPad
echo ===================================================

set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC_PATH%" (
    set "CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC_PATH%" (
    echo [ERRO] O compilador C# csc.exe nao foi encontrado no Windows.
    pause
    exit /b 1
)

echo Usando: %CSC_PATH%
echo Compilando src\NotionPad.cs para NotionPad.exe...

"%CSC_PATH%" /nologo /t:winexe /optimize+ /lib:"%CSC_PATH%\..\WPF" /win32icon:"src\notionpad.ico" /out:"NotionPad.exe" /r:PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll,System.Xaml.dll,System.dll,System.Core.dll "src\NotionPad.cs"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCESSO] NotionPad.exe compilado com sucesso!
    echo ===================================================
) else (
    echo.
    echo [FALHA] Ocorreu um erro durante a compilacao.
    pause
    exit /b 1
)
