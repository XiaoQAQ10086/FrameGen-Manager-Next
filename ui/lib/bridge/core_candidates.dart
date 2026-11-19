import 'dart:io';

/// Candidate `fgm_ffi.dll` locations, most specific first.
Iterable<String> coreCandidates() sync* {
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
