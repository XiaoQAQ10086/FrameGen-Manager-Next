import 'dart:convert';
import 'dart:ffi';
import 'dart:io';

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
    this._deploy,
    this._restore,
    this._inspect,
    this._stringFree,
    this._alloc,
    this._freeBytes,
  );

  final _VersionDart _version;
  final _PhaseNameDart _phaseName;
  final _JsonDart _libraryScan;
  final _JsonDart _libraryList;
  final _RequestDart _deploy;
  final _RequestDart _restore;
  final _RequestDart _inspect;
  final _StringFreeDart _stringFree;
  final _AllocDart _alloc;
  final _FreeBytesDart _freeBytes;

  /// Loads the native library, or returns null when it has not been built yet.
  static CoreLibrary? tryOpen() {
    for (final String candidate in _candidates()) {
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
            'fgm_deploy_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_restore_json',
          ),
          library.lookupFunction<_RequestNative, _RequestDart>(
            'fgm_inspect_json',
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

  String get version => _readCString(_version());

  /// Returns the stable key for a deployment phase, or null when unknown.
  String? phaseName(int phase) {
    final Pointer<Int8> pointer = _phaseName(phase);
    if (pointer.address == 0) {
      return null;
    }
    return _readCString(pointer);
  }

  /// Scans Steam/Epic/loose folders and returns the JSON reply.
  String? scanLibraryJson() => _json(_libraryScan);

  /// Reads the cached library and returns the JSON reply.
  String? listLibraryJson() => _json(_libraryList);

  String? deployJson(String request) => _request(_deploy, request);

  String? restoreJson(String request) => _request(_restore, request);

  String? inspectJson(String request) => _request(_inspect, request);

  String? _request(_RequestDart function, String request) {
    final List<int> bytes = utf8.encode(request);
    final Pointer<Uint8> buffer = _alloc(bytes.length);
    if (buffer.address == 0) {
      return null;
    }
    try {
      buffer.asTypedList(bytes.length).setAll(0, bytes);
      final Pointer<Int8> pointer = function(buffer, bytes.length);
      if (pointer.address == 0) {
        return null;
      }
      try {
        return _readCString(pointer);
      } finally {
        _stringFree(pointer);
      }
    } finally {
      _freeBytes(buffer, bytes.length);
    }
  }

  String? _json(_JsonDart function) {
    final Pointer<Int8> pointer = function();
    if (pointer.address == 0) {
      return null;
    }
    try {
      return _readCString(pointer);
    } finally {
      _stringFree(pointer);
    }
  }

  static Iterable<String> _candidates() sync* {
    final String? override = Platform.environment['FGM_FFI_DLL'];
    if (override != null && override.isNotEmpty) {
      yield override;
    }

    yield 'fgm_ffi.dll';

    final String executableDir = File(Platform.resolvedExecutable).parent.path;
    yield '$executableDir${Platform.pathSeparator}fgm_ffi.dll';

    final String current = Directory.current.path;
    for (final String profile in const ['release', 'debug']) {
      yield '$current/../rust/target/$profile/fgm_ffi.dll';
      yield '$current/rust/target/$profile/fgm_ffi.dll';
    }
  }
}

String _readCString(Pointer<Int8> pointer) {
  final List<int> bytes = <int>[];
  var index = 0;
  while (true) {
    final int unit = pointer[index];
    if (unit == 0) {
      break;
    }
    bytes.add(unit & 0xFF);
    index++;
  }
  return utf8.decode(bytes, allowMalformed: true);
}
