@echo off
cd /d "%~dp0"
title He Thong Server Tu Dong

:: --- CẤU HÌNH NGƯỜI DÙNG ---
:: [HƯỚNG DẪN]: Dán Authtoken của bạn vào sau dấu bằng (=) ở dòng dưới
set MY_TOKEN=NHAP_TOKEN_CUA_BAN_TAI_DAY

:: [HƯỚNG DẪN]: Dán Domain của bạn vào sau dấu bằng (=) ở dòng dưới
:: Nếu không có domain riêng, hãy xóa nội dung sau dấu bằng để trống
set MY_DOMAIN=NHAP_DOMAIN_CUA_BAN_TAI_DAY

set PORT=5656

:: --- KIỂM TRA THÔNG TIN ---
if "%MY_TOKEN%"=="NHAP_TOKEN_CUA_BAN_TAI_DAY" (
    echo [!] LOI: Ban chua nhap Authtoken vao file .bat
    echo Vui long chuot phai vao file nay, chon Edit de dien thong tin.
    pause
    exit
)

:: --- BẮT ĐẦU CHẠY ---
echo [1/3] Dang thiet lap moi truong...

:: [QUAN TRỌNG] Di vao thu muc System de tim file thuc thi
cd System

echo [2/3] Dang kich hoat Ngrok...
:: Nạp token
ngrok config add-authtoken %MY_TOKEN%

:: Chạy Ngrok (Tự động kiểm tra Domain)
if "%MY_DOMAIN%"=="" (
    start "Ngrok Tunnel" ngrok.exe http %PORT%
) else (
    if "%MY_DOMAIN%"=="NHAP_DOMAIN_CUA_BAN_TAI_DAY" (
        start "Ngrok Tunnel" ngrok.exe http %PORT%
    ) else (
        start "Ngrok Tunnel" ngrok.exe http --domain=%MY_DOMAIN% %PORT%
    )
)

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
