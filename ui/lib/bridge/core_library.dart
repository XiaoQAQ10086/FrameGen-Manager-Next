import 'dart:convert';
import 'dart:ffi';

import 'core_candidates.dart';
import 'ffi_string.dart';

part 'core_library_requests.dart';

typedef _VersionNative = Pointer<Int8> Function();
typedef _VersionDart = Pointer<Int8> Function();
typedef _PhaseNameNative = Pointer<Int8> Function(Uint32);
typedef _PhaseNameDart = Pointer<Int8> Function(int);
typedef _JsonNative = Pointer<Int8> Function();
typedef _JsonDart = Pointer<Int8> Function();
typedef _StringFreeNative = Void Function(Pointer<Int8>);
typedef _StringFreeDart = void Function(Pointer<Int8>);
typedef _AllocNative = Pointer<Uint8> Function(IntPtr);
typedef _AllocDart = Pointer<Uint8> Function(int);
typedef _FreeBytesNative = Void Function(Pointer<Uint8>, IntPtr);
typedef _FreeBytesDart = void Function(Pointer<Uint8>, int);
typedef _RequestNative = Pointer<Int8> Function(Pointer<Uint8>, IntPtr);
typedef _RequestDart = Pointer<Int8> Function(Pointer<Uint8>, int);

/// Thin loader around the `fgm-ffi` cdylib.
///
/// The UI never reaches into Rust internals: it only reads the version, phase
/// keys and JSON DTOs exposed by the C ABI.
final class CoreLibrary {
  CoreLibrary._(
    this._version,
    this._phaseName,
    this._libraryScan,
    this._libraryList,
    this._libraryAddManual,
    this._launchGame,
    this._cover,
    this._gameModes,
    this._diagnosticsExport,
    this._pickExecutable,
    this._gpuSpoofInspect,
    this._gpuSpoofApply,
    this._gpuSpoofRestore,
    this._operationProgress,
    this._operationCancel,
    this._deploy,
    this._restore,
    this._inspect,
    this._smInspectMany,
    this._smEnable,
    this._smDisable,
    this._smRestore,
    this._settingsGet,
    this._settingsSet,
    this._dlss5Install,
    this._dlss5Inspect,
    this._dlss5SetHotkey,
    this._dlss5CleanLegacy,
    this._dlss5ProcessHotkeyQueue,
    this._dlss5Restore,
    this._dlss5Uninstall,
    this._logsTail,
    this._anrCurrent,
    this._anrImport,
    this._anrDownload,
    this._anrDeploy,
    this._anrRestore,
    this._updateCheck,
    this._updateDownload,
    this._updateInstall,
    this._updateOpen,
    this._gpu,
    this._windowRestore,
    this._windowSave,
    this._stringFree,
    this._alloc,
    this._freeBytes,
  );

  final _VersionDart _version;
  final _PhaseNameDart _phaseName;
  final _JsonDart _libraryScan;
  final _JsonDart _libraryList;
  final _RequestDart _libraryAddManual;
  final _RequestDart _launchGame;
  final _RequestDart _cover;
  final _RequestDart _gameModes;
  final _RequestDart _diagnosticsExport;
  final _JsonDart _pickExecutable;
  final _JsonDart _gpuSpoofInspect;
  final _RequestDart _gpuSpoofApply;
  final _RequestDart _gpuSpoofRestore;
  final _JsonDart _operationProgress;
  final _JsonDart _operationCancel;
  final _RequestDart _deploy;
  final _RequestDart _restore;
  final _RequestDart _inspect;
  final _RequestDart _smInspectMany;
  final _RequestDart _smEnable;
  final _RequestDart _smDisable;
  final _RequestDart _smRestore;
  final _JsonDart _settingsGet;
  final _RequestDart _settingsSet;
  final _RequestDart _dlss5Install;
  final _RequestDart _dlss5Inspect;
  final _RequestDart _dlss5SetHotkey;
  final _RequestDart _dlss5CleanLegacy;
  final _JsonDart _dlss5ProcessHotkeyQueue;
  final _RequestDart _dlss5Restore;
  final _RequestDart _dlss5Uninstall;
  final _RequestDart _logsTail;
  final _JsonDart _anrCurrent;
  final _RequestDart _anrImport;
  final _RequestDart _anrDownload;
  final _RequestDart _anrDeploy;
  final _RequestDart _anrRestore;
  final _JsonDart _updateCheck;
  final _JsonDart _updateDownload;
  final _RequestDart _updateInstall;
  final _RequestDart _updateOpen;
  final _JsonDart _gpu;
  final _JsonDart _windowRestore;
  final _JsonDart _windowSave;
  final _StringFreeDart _stringFree;
  final _AllocDart _alloc;
  final _FreeBytesDart _freeBytes;

  static CoreLibrary? _shared;

  /// Cached handle for cheap polling from the UI isolate.
  static CoreLibrary? shared() => _shared ??= tryOpen();

  /// Loads the native library, or returns null when it has not been built yet.
  static CoreLibrary? tryOpen() {
    for (final String candidate in coreCandidates()) {
      try {
        final DynamicLibrary library = DynamicLibrary.open(candidate);
        return CoreLibrary._(
          library.lookupFunction<_VersionNative, _VersionDart>('fgm_version'),
          library.lookupFunction<_PhaseNameNative, _PhaseNameDart>(
            'fgm_operation_phase_name',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_library_scan_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_library_list_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_library_add_manual_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_launch_game_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_cover_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_game_modes_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_diagnostics_export_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_pick_executable_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_gpu_spoof_inspect_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_gpu_spoof_apply_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_gpu_spoof_restore_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_operation_progress_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_operation_cancel_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_deploy_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_restore_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_inspect_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_sm_inspect_many_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_sm_enable_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_sm_disable_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_sm_restore_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_settings_get_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_settings_set_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_dlss5_install_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_dlss5_inspect_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_dlss5_set_hotkey_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_dlss5_clean_legacy_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_dlss5_process_hotkey_queue_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_dlss5_restore_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_dlss5_uninstall_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_logs_tail_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_anr_current_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_anr_import_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_anr_download_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_anr_deploy_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_anr_restore_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_update_check_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_update_download_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_update_install_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_update_open_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>('fgm_gpu_json'),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_window_restore_json',
          ),
          library.lookupFunction<_JsonNative, _JsonDart>(
            'fgm_window_save_json',
          ),
          library.lookupFunction<_StringFreeNative, _StringFreeDart>(
            'fgm_string_free',
          ),
          library.lookupFunction<_AllocNative, _AllocDart>('fgm_alloc'),
          library.lookupFunction<_FreeBytesNative, _FreeBytesDart>(
            'fgm_free_bytes',
          ),
        );
      } on Object {
        continue;
      }
    }
    return null;
  }

  String get version => readCString(_version());

  /// Returns the stable key for a deployment phase, or null when unknown.
  String? phaseName(int phase) {
    final Pointer<Int8> pointer = _phaseName(phase);
    if (pointer.address == 0) {
      return null;
    }
    return readCString(pointer);
  }

  /// Scans Steam/Epic/loose folders and returns the JSON reply.
  String? scanLibraryJson() => _json(_libraryScan);

  /// Reads the cached library and returns the JSON reply.
  String? listLibraryJson() => _json(_libraryList);

  String? libraryAddManualJson(String request) =>
      _request(_libraryAddManual, request);

  String? launchGameJson(String request) => _request(_launchGame, request);

  String? coverJson(String request) => _request(_cover, request);

  String? gameModesJson(String request) => _request(_gameModes, request);

  String? diagnosticsExportJson(String request) =>
      _request(_diagnosticsExport, request);

  String? pickExecutableJson() => _json(_pickExecutable);

  String? gpuSpoofInspectJson() => _json(_gpuSpoofInspect);

  String? gpuSpoofApplyJson(String request) =>
      _request(_gpuSpoofApply, request);

  String? gpuSpoofRestoreJson(String request) =>
      _request(_gpuSpoofRestore, request);

  String? operationProgressJson() => _json(_operationProgress);

  String? operationCancelJson() => _json(_operationCancel);

  String? deployJson(String request) => _request(_deploy, request);

  String? restoreJson(String request) => _request(_restore, request);

  String? inspectJson(String request) => _request(_inspect, request);

  String? smInspectManyJson(String request) =>
      _request(_smInspectMany, request);

  String? smEnableJson(String request) => _request(_smEnable, request);

  String? smDisableJson(String request) => _request(_smDisable, request);

  String? smRestoreJson(String request) => _request(_smRestore, request);

  String? settingsGetJson() => _json(_settingsGet);

  String? settingsSetJson(String request) =>
      _request(_settingsSet, request);

  String? dlss5InstallJson(String request) =>
      _request(_dlss5Install, request);

  String? dlss5InspectJson(String request) =>
      _request(_dlss5Inspect, request);

  String? dlss5SetHotkeyJson(String request) =>
      _request(_dlss5SetHotkey, request);

  String? dlss5CleanLegacyJson(String request) =>
      _request(_dlss5CleanLegacy, request);

  String? dlss5ProcessHotkeyQueueJson() => _json(_dlss5ProcessHotkeyQueue);

  String? dlss5RestoreJson(String request) =>
      _request(_dlss5Restore, request);

  String? dlss5UninstallJson(String request) =>
      _request(_dlss5Uninstall, request);

  String? logsTailJson(String request) => _request(_logsTail, request);

  String? anrCurrentJson() => _json(_anrCurrent);

  String? anrImportJson(String request) => _request(_anrImport, request);

  String? anrDownloadJson(String request) => _request(_anrDownload, request);

  String? anrDeployJson(String request) => _request(_anrDeploy, request);

  String? anrRestoreJson(String request) => _request(_anrRestore, request);

  String? updateCheckJson() => _json(_updateCheck);

  String? updateDownloadJson() => _json(_updateDownload);

  String? updateInstallJson(String request) => _request(_updateInstall, request);

  String? updateOpenJson(String request) => _request(_updateOpen, request);

  String? gpuJson() => _json(_gpu);

  /// Applies the saved window bounds; no-op when none were stored.
  String? windowRestoreJson() => _json(_windowRestore);

  /// Persists the current window bounds.
  String? windowSaveJson() => _json(_windowSave);

  String? _request(_RequestDart function, String request) =>
      _requestJson(function, request, _alloc, _stringFree, _freeBytes);


  String? _json(_JsonDart function) =>
      _jsonString(function, _stringFree);

}
