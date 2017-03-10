@echo off
::務必先設定好GTA5.exe執行檔的位置，否則防火牆不會有作用
SET GTAPath="D:\SteamLibrary\steamapps\common\Grand Theft Auto V\gta5.exe"

::########################################## 注意底下請勿修改 ##########################################
SET RuleName="GTA 5 單人公開戰局用"
SET File=".check"

netsh advfirewall firewall show rule name=%RuleName% | findstr /C:%RuleName% > %File%

FOR /F "usebackq" %%A IN ('%File%') DO set size=%%~zA

if %size% EQU 0 (
    ::如果找不到該規則，則要新增
    netsh advfirewall firewall add rule name=%RuleName% dir=in action=block program=%GTAPath% enable=yes
    netsh advfirewall firewall add rule name=%RuleName% dir=out action=block program=%GTAPath% enable=yes
    IF %ERRORLEVEL% EQU 0 echo GTA5禁止連線成功(A)
) ELSE (
    ::如果找到則更新規則
    netsh advfirewall firewall set rule name=%RuleName% new program=%GTAPath% action=block enable=yes
    IF %ERRORLEVEL% EQU 0 echo GTA5禁止連線成功(E)
)
del /F /Q %File%
::pause
@echo on