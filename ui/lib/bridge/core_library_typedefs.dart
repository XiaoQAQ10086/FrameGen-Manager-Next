part of 'core_library.dart';

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
