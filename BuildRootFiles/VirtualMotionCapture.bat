@echo off

echo Running VirtualMotionCapture... / バーチャルモーションキャプチャーを起動中・・・

set pipeName=VMCpipe%RANDOM%%RANDOM%

start VirtualMotionCapture.exe /pipeName %pipeName%

echo Waiting...

timeout /t 5 > nul

echo Running ControlPanel... / コントロールパネルを起動中・・・

start ControlPanel\VirtualMotionCaptureControlPanel.exe /pipeName %pipeName%