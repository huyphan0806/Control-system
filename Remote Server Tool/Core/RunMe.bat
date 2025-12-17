@echo off
cd /d "%~dp0"
title He Thong Server Tu Dong

:: --- CẤU HÌNH ---
set PORT=5656
set MY_TOKEN=36j0ncx60N5CNmq46CuW5li38ux_5wrfkt6VbXGxHPoXZ7ErL

:: --- BẮT ĐẦU CHẠY ---
echo [1/3] Dang thiet lap moi truong...

:: [QUAN TRỌNG] Lệnh này bảo máy tính đi vào thư mục System để tìm file
cd System

echo [2/3] Dang kich hoat Ngrok...
:: Nạp token
ngrok config add-authtoken %MY_TOKEN%

:: Chạy Ngrok với tên miền cố định của bạn
start "Ngrok Tunnel" ngrok.exe http --domain=unexhilarative-anjanette-nonadverbially.ngrok-free.dev %PORT%

echo.
echo [3/3] Dang khoi dong Server C#...
timeout /t 3 >nul

:: Chạy Server
start "My Server" server.exe

echo.
echo === HOAN TAT ===
echo Server da duoc kich hoat tu trong thu muc System.
:: Quay ra ngoài để không bị kẹt
cd ..
pause