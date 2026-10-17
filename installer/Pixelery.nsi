Unicode true
ManifestDPIAware true
RequestExecutionLevel user
SetCompressor /SOLID lzma
Name "Pixelery"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\Pixelery"
InstallDirRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Pixelery" "InstallLocation"
VIProductVersion "${PRODUCT_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "Pixelery"
VIAddVersionKey /LANG=1033 "ProductVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=1033 "FileVersion" "${PRODUCT_VERSION}"
VIAddVersionKey /LANG=1033 "FileDescription" "Pixelery Setup"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright 2026 tobitege"

!include MUI2.nsh
!include LogicLib.nsh
!include FileFunc.nsh
!include TextFunc.nsh
!include Sections.nsh
!include x64.nsh
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Pixelery"
!define MUI_LANGDLL_ALLLANGUAGES

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "German"
!insertmacro MUI_LANGUAGE "French"
!insertmacro MUI_LANGUAGE "Spanish"
!insertmacro MUI_LANGUAGE "Italian"
!insertmacro MUI_LANGUAGE "Japanese"
!insertmacro MUI_LANGUAGE "Korean"
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "Arabic"
!insertmacro MUI_LANGUAGE "Turkish"
!insertmacro MUI_LANGUAGE "Ukrainian"
!insertmacro MUI_LANGUAGE "Hebrew"

LangString CliName ${LANG_ENGLISH} "Command line tool (optional)"
LangString CliName ${LANG_GERMAN} "Kommandozeilenprogramm (optional)"
LangString CliName ${LANG_FRENCH} "Outil en ligne de commande (facultatif)"
LangString CliName ${LANG_SPANISH} "Herramienta de línea de comandos (opcional)"
LangString CliName ${LANG_ITALIAN} "Strumento da riga di comando (facoltativo)"
LangString CliName ${LANG_JAPANESE} "コマンドラインツール（任意）"
LangString CliName ${LANG_KOREAN} "명령줄 도구(선택 사항)"
LangString CliName ${LANG_SIMPCHINESE} "命令行工具（可选）"
LangString CliName ${LANG_ARABIC} "أداة سطر الأوامر (اختيارية)"
LangString CliName ${LANG_TURKISH} "Komut satırı aracı (isteğe bağlı)"
LangString CliName ${LANG_UKRAINIAN} "Засіб командного рядка (необов’язково)"
LangString CliName ${LANG_HEBREW} "כלי שורת פקודה (אופציונלי)"
LangString FileError ${LANG_ENGLISH} "Cannot update or remove the installed files. Close Pixelery and try again."
LangString FileError ${LANG_GERMAN} "Die installierten Dateien können nicht aktualisiert oder entfernt werden. Schließe Pixelery und versuche es erneut."
LangString FileError ${LANG_FRENCH} "Impossible de mettre à jour ou de supprimer les fichiers installés. Fermez Pixelery et réessayez."
LangString FileError ${LANG_SPANISH} "No se pueden actualizar ni eliminar los archivos instalados. Cierra Pixelery e inténtalo de nuevo."
LangString FileError ${LANG_ITALIAN} "Impossibile aggiornare o rimuovere i file installati. Chiudi Pixelery e riprova."
LangString FileError ${LANG_JAPANESE} "インストール済みファイルを更新または削除できません。Pixelery を閉じて再試行してください。"
LangString FileError ${LANG_KOREAN} "설치된 파일을 업데이트하거나 제거할 수 없습니다. Pixelery를 닫고 다시 시도하세요."
LangString FileError ${LANG_SIMPCHINESE} "无法更新或删除已安装的文件。请关闭 Pixelery 后重试。"
LangString FileError ${LANG_ARABIC} "تعذر تحديث الملفات المثبتة أو إزالتها. أغلق Pixelery وحاول مرة أخرى."
LangString FileError ${LANG_TURKISH} "Yüklü dosyalar güncellenemiyor veya kaldırılamıyor. Pixelery’yi kapatıp yeniden deneyin."
LangString FileError ${LANG_UKRAINIAN} "Не вдалося оновити або видалити встановлені файли. Закрийте Pixelery та спробуйте ще раз."
LangString FileError ${LANG_HEBREW} "לא ניתן לעדכן או להסיר את הקבצים המותקנים. יש לסגור את Pixelery ולנסות שוב."

Var Manifest
Var PayloadPrefix
Var Handle
Var Entry
Var Parent
Var Position
Var Previous
Var Character
Var InitialCulture
Var StateDirectory
Var Relaunch

Section "Pixelery" SecApp
    SectionIn RO
    StrCpy $Manifest "$INSTDIR\files-app.lst"
    StrCpy $PayloadPrefix "app\"
    Call RemoveManifest
    StrCpy $Manifest "$INSTDIR\files-cli.lst"
    StrCpy $PayloadPrefix "cli\"
    Call RemoveManifest
    SetOverwrite on
    !include "${BUILD_DIR}\app.nsh"
    SetOutPath "$INSTDIR"
    File /oname=files-app.lst "${BUILD_DIR}\app.lst"
    IfErrors install_failed
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    IfErrors install_failed
    CreateDirectory "$SMPROGRAMS\Pixelery"
    CreateShortcut "$SMPROGRAMS\Pixelery\Pixelery.lnk" "$INSTDIR\app\Pixelery.exe"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "Pixelery"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "tobitege"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\app\Pixelery.exe"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "CliInstalled" 0
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "Language" $LANGUAGE
    Call InitializeUserLanguage
    Goto app_done
install_failed:
    MessageBox MB_ICONSTOP "$(FileError)" /SD IDOK
    SetErrorLevel 1
    Quit
app_done:
SectionEnd

Section /o "$(CliName)" SecCli
    !include "${BUILD_DIR}\cli.nsh"
    SetOutPath "$INSTDIR"
    File /oname=files-cli.lst "${BUILD_DIR}\cli.lst"
    IfErrors install_failed
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "CliInstalled" 1
    Goto cli_done
install_failed:
    MessageBox MB_ICONSTOP "$(FileError)" /SD IDOK
    SetErrorLevel 1
    Quit
cli_done:
SectionEnd

Function .onInit
    SetShellVarContext current
    SetRegView 64
    ${IfNot} ${RunningX64}
        SetErrorLevel 2
        Quit
    ${EndIf}
    ClearErrors
    ReadRegDWORD $2 HKCU "${UNINSTALL_KEY}" "Language"
    ${IfNot} ${Errors}
        StrCpy $LANGUAGE $2
    ${EndIf}
    ${GetParameters} $0
    ClearErrors
    ${GetOptions} $0 "/LANGUAGE=" $3
    ${IfNot} ${Errors}
        StrCpy $LANGUAGE $3
    ${EndIf}
    !insertmacro MUI_LANGDLL_DISPLAY
    Call MapLanguage
    ${GetParameters} $0
    ClearErrors
    ${GetOptions} $0 "/CLI=" $1
    ${If} ${Errors}
        ReadRegDWORD $1 HKCU "${UNINSTALL_KEY}" "CliInstalled"
    ${EndIf}
    ${If} $1 == "1"
        !insertmacro SelectSection ${SecCli}
    ${EndIf}
    ${GetOptions} $0 "/RELAUNCH=" $R0
    StrCpy $Relaunch $R0
    ClearErrors
    ${GetOptions} $0 "/UPDATEPID=" $R0
    ${IfNot} ${Errors}
        ; The app starts us before closing. Do not touch its payload until it exits.
        System::Call 'kernel32::OpenProcess(i 0x100000, i 0, i R0) p.r1'
        ${If} $1 != 0
            System::Call 'kernel32::WaitForSingleObject(p r1, i 30000) i.r2'
            System::Call 'kernel32::CloseHandle(p r1)'
            ${If} $2 != 0
                SetErrorLevel 3
                Quit
            ${EndIf}
        ${Else}
            System::Call 'kernel32::GetLastError() i.r2'
            ${If} $2 != 87
                SetErrorLevel 3
                Quit
            ${EndIf}
        ${EndIf}
    ${EndIf}
FunctionEnd

Function .onInstSuccess
    ${If} $Relaunch == "1"
        SetOutPath "$INSTDIR\app"
        Exec '"$INSTDIR\app\Pixelery.exe"'
    ${EndIf}
FunctionEnd

Function MapLanguage
    StrCpy $InitialCulture ""
    ${Switch} $LANGUAGE
        ${Case} ${LANG_ENGLISH}
            StrCpy $InitialCulture "en"
            ${Break}
        ${Case} ${LANG_GERMAN}
            StrCpy $InitialCulture "de"
            ${Break}
        ${Case} ${LANG_FRENCH}
            StrCpy $InitialCulture "fr"
            ${Break}
        ${Case} ${LANG_SPANISH}
            StrCpy $InitialCulture "es"
            ${Break}
        ${Case} ${LANG_ITALIAN}
            StrCpy $InitialCulture "it"
            ${Break}
        ${Case} ${LANG_JAPANESE}
            StrCpy $InitialCulture "ja"
            ${Break}
        ${Case} ${LANG_KOREAN}
            StrCpy $InitialCulture "ko"
            ${Break}
        ${Case} ${LANG_SIMPCHINESE}
            StrCpy $InitialCulture "zh-CN"
            ${Break}
        ${Case} ${LANG_ARABIC}
            StrCpy $InitialCulture "ar"
            ${Break}
        ${Case} ${LANG_TURKISH}
            StrCpy $InitialCulture "tr"
            ${Break}
        ${Case} ${LANG_UKRAINIAN}
            StrCpy $InitialCulture "uk"
            ${Break}
        ${Case} ${LANG_HEBREW}
            StrCpy $InitialCulture "he"
            ${Break}
    ${EndSwitch}
    ${If} $InitialCulture == ""
        SetErrorLevel 2
        Quit
    ${EndIf}
FunctionEnd

Function InitializeUserLanguage
    ReadEnvStr $StateDirectory PIXELERY_STATE_ROOT
    ${GetRoot} "$StateDirectory" $0
    ${If} $0 == ""
        StrCpy $StateDirectory "$LOCALAPPDATA\Pixelery"
    ${EndIf}
    CreateDirectory "$StateDirectory"
    ; CREATE_NEW is atomic: never replace even an empty existing preference file.
    System::Call 'kernel32::CreateFileW(w "$StateDirectory\language.state", i 0x40000000, i 1, p 0, i 1, i 0x80, p 0) p.r1'
    StrCmp $1 -1 language_done
    FileWrite $1 "$InitialCulture$\r$\n"
    FileClose $1
language_done:
FunctionEnd

Function un.onInit
    SetShellVarContext current
    SetRegView 64
    ClearErrors
    ReadRegDWORD $2 HKCU "${UNINSTALL_KEY}" "Language"
    ${IfNot} ${Errors}
        StrCpy $LANGUAGE $2
    ${EndIf}
FunctionEnd

; Only remove recorded payload files. Never recursively remove an install or settings directory.
!macro RemoveManifestFunction Prefix
Function ${Prefix}RemoveManifest
    IfFileExists "$Manifest" 0 done
    ClearErrors
    FileOpen $Handle "$Manifest" r
    IfErrors failed
    FileSeek $Handle 2 SET ; UTF-16 BOM
next:
    ClearErrors
    FileReadUTF16LE $Handle $Entry
    IfErrors complete
    ${TrimNewLines} $Entry $Entry
    StrCmp $Entry "" next
    StrCpy $0 $Entry 4
    StrCmp $0 $PayloadPrefix 0 invalid
    StrCpy $Position 4
    StrCpy $Previous ""
check_character:
    StrCpy $Character $Entry 1 $Position
    StrCmp $Character "" remove
    StrCmp $Character ":" invalid
    StrCmp $Character "/" invalid
    StrCmp $Character "*" invalid
    StrCmp $Character "?" invalid
    ${If} $Character == "."
    ${AndIf} $Previous == "."
        Goto invalid
    ${EndIf}
    StrCpy $Previous $Character
    IntOp $Position $Position + 1
    Goto check_character
remove:
    ClearErrors
    Delete "$INSTDIR\$Entry"
    IfErrors invalid
    ${GetParent} "$INSTDIR\$Entry" $Parent
remove_directory:
    StrCmp $Parent "$INSTDIR" next
    RMDir "$Parent" ; Nonempty directories, including user files, are retained.
    ${GetParent} "$Parent" $Parent
    Goto remove_directory
complete:
    FileClose $Handle
    Delete "$Manifest"
    Goto done
invalid:
    FileClose $Handle
failed:
    MessageBox MB_ICONSTOP "$(FileError)" /SD IDOK
    SetErrorLevel 1
    Quit
done:
FunctionEnd
!macroend
!insertmacro RemoveManifestFunction ""
!insertmacro RemoveManifestFunction "un."

Section "Uninstall"
    StrCpy $Manifest "$INSTDIR\files-app.lst"
    StrCpy $PayloadPrefix "app\"
    Call un.RemoveManifest
    StrCpy $Manifest "$INSTDIR\files-cli.lst"
    StrCpy $PayloadPrefix "cli\"
    Call un.RemoveManifest
    Delete "$SMPROGRAMS\Pixelery\Pixelery.lnk"
    RMDir "$SMPROGRAMS\Pixelery"
    DeleteRegKey HKCU "${UNINSTALL_KEY}"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir "$INSTDIR"
SectionEnd
