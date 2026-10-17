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
!include x64.nsh
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Pixelery"
!define MUI_LANGDLL_ALLLANGUAGES

!insertmacro MUI_PAGE_WELCOME
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

LangString SetupBusy ${LANG_ENGLISH} "Another Pixelery installation or removal is running. Wait for it to finish and try again."
LangString SetupBusy ${LANG_GERMAN} "Eine andere Installation oder Deinstallation von Pixelery läuft. Warte, bis sie beendet ist, und versuche es erneut."
LangString SetupBusy ${LANG_FRENCH} "Une autre installation ou désinstallation de Pixelery est en cours. Attendez sa fin et réessayez."
LangString SetupBusy ${LANG_SPANISH} "Hay otra instalación o desinstalación de Pixelery en curso. Espera a que termine e inténtalo de nuevo."
LangString SetupBusy ${LANG_ITALIAN} "È in corso un'altra installazione o disinstallazione di Pixelery. Attendi il completamento e riprova."
LangString SetupBusy ${LANG_JAPANESE} "別の Pixelery のインストールまたはアンインストールが実行中です。完了してから再試行してください。"
LangString SetupBusy ${LANG_KOREAN} "다른 Pixelery 설치 또는 제거가 진행 중입니다. 완료될 때까지 기다린 후 다시 시도하세요."
LangString SetupBusy ${LANG_SIMPCHINESE} "另一个 Pixelery 安装或卸载正在进行中。请等待完成后重试。"
LangString SetupBusy ${LANG_ARABIC} "تجري عملية أخرى لتثبيت Pixelery أو إزالته. انتظر حتى تنتهي ثم حاول مرة أخرى."
LangString SetupBusy ${LANG_TURKISH} "Başka bir Pixelery yükleme veya kaldırma işlemi sürüyor. Bitmesini bekleyip yeniden deneyin."
LangString SetupBusy ${LANG_UKRAINIAN} "Триває інше встановлення або видалення Pixelery. Дочекайтеся завершення та спробуйте ще раз."
LangString SetupBusy ${LANG_HEBREW} "התקנה או הסרה אחרת של Pixelery מתבצעת כעת. יש להמתין לסיומה ולנסות שוב."

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
Var Transaction
Var Stage
Var Backup
Var Journal
Var TransactionFailed
Var RollbackFailed
Var SetupMutex

; Installation and removal share registration and shortcuts even across target
; directories. Keep one owner until process exit, including rollback and relaunch.
; Global also covers another Windows session using the same installation.
!macro AcquireSetupLock
    System::Call 'kernel32::CreateMutexW(p 0, i 0, w "Global\Pixelery.Setup") p.r0'
    StrCpy $SetupMutex $0
    StrCpy $1 -1
    ${If} $SetupMutex != 0
        System::Call 'kernel32::WaitForSingleObject(p r0, i 0) i.r1'
    ${EndIf}
    ${If} $1 != 0
    ${AndIf} $1 != 128 ; WAIT_ABANDONED grants ownership after a terminated setup.
        ${If} $Relaunch == "1"
            SetSilent normal
        ${EndIf}
        MessageBox MB_ICONSTOP "$(SetupBusy)" /SD IDOK
        SetErrorLevel 1618
        Quit
    ${EndIf}
!macroend

; A relative manifest path can still escape through a junction in a parent.
; The selected installation root is the boundary; never follow links below it.
!macro RejectLinkedParents FailureLabel
    StrCpy $Parent "$Entry"
    ${Do}
        ${GetParent} "$Parent" $Parent
        ${If} $Parent == ""
            ${ExitDo}
        ${EndIf}
        System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR\$Parent") i.r0'
        ${If} $0 != -1
            IntOp $0 $0 & 0x400 ; FILE_ATTRIBUTE_REPARSE_POINT
            ${If} $0 != 0
                Goto ${FailureLabel}
            ${EndIf}
        ${EndIf}
    ${Loop}
!macroend

Section "Pixelery" SecApp
    SectionIn RO
    ; Stage on the destination volume before changing any existing payload.
    CreateDirectory "$INSTDIR"
    ClearErrors
    GetTempFileName $Transaction "$INSTDIR"
    IfErrors install_failed
    Delete "$Transaction"
    CreateDirectory "$Transaction"
    IfErrors install_failed
    StrCpy $Stage "$Transaction\new"
    StrCpy $Backup "$Transaction\old"
    StrCpy $TransactionFailed 0
    ClearErrors
    SetOverwrite on
    !include "${BUILD_DIR}\payload.nsh"
    SetOutPath "$Stage"
    File /oname=files.lst "${BUILD_DIR}\payload.lst"
    WriteUninstaller "$Stage\Uninstall.exe"
    IfErrors install_failed
    FileOpen $Journal "$Transaction\entries.lst" w
    IfErrors install_failed
    FileWriteUTF16LE /BOM $Journal ""
    StrCpy $Manifest "$INSTDIR\files-app.lst"
    StrCpy $PayloadPrefix "app\"
    Call AppendManifest
    StrCpy $Manifest "$INSTDIR\files-cli.lst"
    StrCpy $PayloadPrefix "cli\"
    Call AppendManifest
    StrCpy $Manifest "$INSTDIR\files.lst"
    StrCpy $PayloadPrefix ""
    Call AppendManifest
    StrCpy $Manifest "$Stage\files.lst"
    Call AppendManifest
    FileWriteUTF16LE $Journal "files-app.lst$\r$\nfiles-cli.lst$\r$\nfiles.lst$\r$\nUninstall.exe$\r$\n"
    IfErrors install_failed
    FileClose $Journal
    StrCpy $Journal ""
    StrCmp $TransactionFailed 0 0 install_failed
    Call BackupFiles
    StrCmp $TransactionFailed 0 0 install_failed
    FileOpen $Journal "$Transaction\published.lst" w
    IfErrors install_failed
    FileWriteUTF16LE /BOM $Journal ""
    StrCpy $Manifest "$Stage\files.lst"
    Call PublishFiles
    StrCpy $Entry "files.lst"
    Call PublishEntry
    StrCpy $Entry "Uninstall.exe"
    Call PublishEntry
    FileClose $Journal
    StrCpy $Journal ""
    StrCmp $TransactionFailed 0 0 install_failed
    SetOutPath "$INSTDIR"
    CreateDirectory "$SMPROGRAMS\Pixelery"
    CreateShortcut "$SMPROGRAMS\Pixelery\Pixelery.lnk" "$INSTDIR\Pixelery.exe"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "Pixelery"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "tobitege"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\Pixelery.exe"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
    DeleteRegValue HKCU "${UNINSTALL_KEY}" "CliInstalled"
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "Language" $LANGUAGE
    Call InitializeUserLanguage
    Call CleanTransaction
    Goto app_done
install_failed:
    StrCmp $Journal "" +2
    FileClose $Journal
    SetOutPath "$INSTDIR"
    Call RollbackFiles
    Call ReportInstallFailure
    ${If} $RollbackFailed == 0
        Call RelaunchApplication
    ${EndIf}
    SetErrorLevel 1
    Quit
app_done:
SectionEnd

; Build the complete, validated target list before any rename. Existing manifests
; and the uninstaller are included in the same transaction as the payload.
Function AppendManifest
    IfFileExists "$Manifest" 0 append_done
    ClearErrors
    FileOpen $Handle "$Manifest" r
    IfErrors append_failed
    FileSeek $Handle 2 SET
append_next:
    ClearErrors
    FileReadUTF16LE $Handle $Entry
    IfErrors append_complete
    ${TrimNewLines} $Entry $Entry
    StrCmp $Entry "" append_next
    StrLen $Position $PayloadPrefix
    StrCpy $0 $Entry $Position
    StrCmp $0 $PayloadPrefix 0 append_invalid
    StrCpy $0 $Entry 1
    StrCmp $0 "\" append_invalid
    StrCpy $Previous ""
append_character:
    StrCpy $Character $Entry 1 $Position
    StrCmp $Character "" append_write
    StrCmp $Character ":" append_invalid
    StrCmp $Character "/" append_invalid
    StrCmp $Character "*" append_invalid
    StrCmp $Character "?" append_invalid
    ${If} $Character == "."
    ${AndIf} $Previous == "."
        Goto append_invalid
    ${EndIf}
    StrCpy $Previous $Character
    IntOp $Position $Position + 1
    Goto append_character
append_write:
    !insertmacro RejectLinkedParents append_invalid
    FileWriteUTF16LE $Journal "$Entry$\r$\n"
    IfErrors append_invalid
    Goto append_next
append_complete:
    FileClose $Handle
    Goto append_done
append_invalid:
    FileClose $Handle
append_failed:
    StrCpy $TransactionFailed 1
append_done:
FunctionEnd

Function BackupFiles
    FileOpen $Handle "$Transaction\entries.lst" r
    IfErrors backup_failed
    FileSeek $Handle 2 SET
backup_next:
    ClearErrors
    FileReadUTF16LE $Handle $Entry
    IfErrors backup_complete
    ${TrimNewLines} $Entry $Entry
    StrCmp $Entry "" backup_next
    IfFileExists "$Backup\$Entry" backup_next
    System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR\$Entry") i.r0'
    IntCmp $0 -1 backup_next
    IntOp $0 $0 & 0x410 ; Reject directories and reparse points.
    IntCmp $0 0 0 backup_invalid backup_invalid
    ${GetParent} "$Backup\$Entry" $Parent
    CreateDirectory "$Parent"
    ClearErrors
    Rename "$INSTDIR\$Entry" "$Backup\$Entry"
    IfErrors backup_invalid
    Goto backup_next
backup_complete:
    FileClose $Handle
    Return
backup_invalid:
    FileClose $Handle
backup_failed:
    StrCpy $TransactionFailed 1
FunctionEnd

Function PublishFiles
    FileOpen $Handle "$Manifest" r
    IfErrors publish_failed
    FileSeek $Handle 2 SET
publish_next:
    ClearErrors
    FileReadUTF16LE $Handle $Entry
    IfErrors publish_complete
    ${TrimNewLines} $Entry $Entry
    StrCmp $Entry "" publish_next
    Call PublishEntry
    StrCmp $TransactionFailed 0 publish_next
publish_complete:
    FileClose $Handle
    Return
publish_failed:
    StrCpy $TransactionFailed 1
FunctionEnd

Function PublishEntry
    StrCmp $TransactionFailed 0 0 publish_entry_done
    ${GetParent} "$INSTDIR\$Entry" $Parent
    CreateDirectory "$Parent"
    ClearErrors
    ; Journal first: rollback only moves entries whose staged file is absent.
    FileWriteUTF16LE $Journal "$Entry$\r$\n"
    IfErrors publish_entry_failed
    Rename "$Stage\$Entry" "$INSTDIR\$Entry"
    IfErrors publish_entry_failed
    Return
publish_entry_failed:
    StrCpy $TransactionFailed 1
publish_entry_done:
FunctionEnd

Function RollbackFiles
    StrCpy $RollbackFailed 0
    StrCmp $Transaction "" rollback_return
    IfFileExists "$Transaction\published.lst" 0 rollback_originals
    ClearErrors
    FileOpen $Handle "$Transaction\published.lst" r
    IfErrors rollback_unreadable
    FileSeek $Handle 2 SET
rollback_new:
    ClearErrors
    FileReadUTF16LE $Handle $Entry
    IfErrors rollback_new_done
    ${TrimNewLines} $Entry $Entry
    StrCmp $Entry "" rollback_new
    IfFileExists "$Stage\$Entry" rollback_new
    ${GetParent} "$Stage\$Entry" $Parent
    CreateDirectory "$Parent"
    Rename "$INSTDIR\$Entry" "$Stage\$Entry"
    ${If} ${Errors}
        StrCpy $RollbackFailed 1
    ${EndIf}
    Goto rollback_new
rollback_new_done:
    FileClose $Handle
rollback_originals:
    IfFileExists "$Transaction\entries.lst" 0 rollback_done
    ClearErrors
    FileOpen $Handle "$Transaction\entries.lst" r
    IfErrors rollback_unreadable
    FileSeek $Handle 2 SET
rollback_old:
    ClearErrors
    FileReadUTF16LE $Handle $Entry
    IfErrors rollback_old_done
    ${TrimNewLines} $Entry $Entry
    StrCmp $Entry "" rollback_old
    IfFileExists "$Backup\$Entry" 0 rollback_old
    ${GetParent} "$INSTDIR\$Entry" $Parent
    CreateDirectory "$Parent"
    ClearErrors
    Rename "$Backup\$Entry" "$INSTDIR\$Entry"
    ; Preserve backup evidence if restoration was blocked by another process.
    ${If} ${Errors}
        StrCpy $RollbackFailed 1
    ${EndIf}
    Goto rollback_old
rollback_old_done:
    FileClose $Handle
rollback_done:
    ; Do not discard backups after a failed rollback.
    ${If} $RollbackFailed == 0
        Call CleanTransaction
    ${EndIf}
rollback_return:
    Return
rollback_unreadable:
    ; Keep both journals and backups if their contents cannot be read safely.
    StrCpy $RollbackFailed 1
FunctionEnd

Function CleanTransaction
    StrCmp $Transaction "" clean_return
    IfFileExists "$Transaction\entries.lst" 0 clean_staging
    FileOpen $Handle "$Transaction\entries.lst" r
    FileSeek $Handle 2 SET
clean_next:
    ClearErrors
    FileReadUTF16LE $Handle $Entry
    IfErrors clean_done
    ${TrimNewLines} $Entry $Entry
    StrCmp $Entry "" clean_next
    ${GetParent} "$INSTDIR\$Entry" $Parent
clean_parent:
    StrCmp $Parent "$INSTDIR" clean_next
    RMDir "$Parent"
    ${GetParent} "$Parent" $Parent
    Goto clean_parent
clean_done:
    FileClose $Handle
clean_staging:
    ; Transaction contains only files staged or renamed by this installer.
    RMDir /r "$Transaction"
    ; Retire empty directories from the previous layout, preserving user files.
    RMDir "$INSTDIR\app"
    RMDir "$INSTDIR\cli"
clean_return:
FunctionEnd

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
    ${GetOptions} $0 "/RELAUNCH=" $R0
    StrCpy $Relaunch $R0
    !insertmacro AcquireSetupLock
    ${GetParameters} $0
    ClearErrors
    ${GetOptions} $0 "/UPDATEPID=" $R0
    ${IfNot} ${Errors}
        ; The app starts us before closing. Do not touch its payload until it exits.
        System::Call 'kernel32::OpenProcess(i 0x100000, i 0, i R0) p.r1'
        ${If} $1 != 0
            System::Call 'kernel32::WaitForSingleObject(p r1, i 30000) i.r2'
            System::Call 'kernel32::CloseHandle(p r1)'
            ${If} $2 != 0
                Call ReportInstallFailure
                SetErrorLevel 3
                Quit
            ${EndIf}
        ${Else}
            System::Call 'kernel32::GetLastError() i.r2'
            ${If} $2 != 87
                Call ReportInstallFailure
                SetErrorLevel 3
                Quit
            ${EndIf}
        ${EndIf}
    ${EndIf}
FunctionEnd

Function .onInstSuccess
    Call RelaunchApplication
FunctionEnd

Function ReportInstallFailure
    ; An automatic update has already closed its caller. Its error must remain
    ; visible even though successful updates run silently.
    ${If} $Relaunch == "1"
        SetSilent normal
    ${EndIf}
    MessageBox MB_ICONSTOP "$(FileError)" /SD IDOK
FunctionEnd

Function RelaunchApplication
    ${If} $Relaunch == "1"
    ${AndIf} ${FileExists} "$INSTDIR\Pixelery.exe"
        SetOutPath "$INSTDIR"
        Exec '"$INSTDIR\Pixelery.exe"'
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
    !insertmacro AcquireSetupLock
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
    StrLen $Position $PayloadPrefix
    StrCpy $0 $Entry $Position
    StrCmp $0 $PayloadPrefix 0 invalid
    StrCpy $0 $Entry 1
    StrCmp $0 "\" invalid
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
    !insertmacro RejectLinkedParents invalid
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
!insertmacro RemoveManifestFunction "un."

Section "Uninstall"
    StrCpy $Manifest "$INSTDIR\files.lst"
    StrCpy $PayloadPrefix ""
    Call un.RemoveManifest
    Delete "$SMPROGRAMS\Pixelery\Pixelery.lnk"
    RMDir "$SMPROGRAMS\Pixelery"
    DeleteRegKey HKCU "${UNINSTALL_KEY}"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir "$INSTDIR"
SectionEnd
