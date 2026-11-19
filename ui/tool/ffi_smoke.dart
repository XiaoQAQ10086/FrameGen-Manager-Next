import 'dart:io';

import 'package:fgm_ui/bridge/core_library.dart';

/// Cross-language smoke test used by the Rust + Flutter gate.
void main() {
  final CoreLibrary? core = CoreLibrary.tryOpen();
  if (core == null) {
    stderr.writeln('fgm_ffi.dll not found; set FGM_FFI_DLL or build the crate.');
    exitCode = 1;
    return;
  }

  stdout.writeln('fgm core ${core.version}');
  stdout.writeln('phase 0 = ${core.phaseName(0)}');
  stdout.writeln('phase 4 = ${core.phaseName(4)}');
  stdout.writeln('phase 99 = ${core.phaseName(99)}');

  if (core.phaseName(0) != 'planned' ||
      core.phaseName(4) != 'installed' ||
      core.phaseName(99) != null) {
    stderr.writeln('unexpected phase mapping');
    exitCode = 1;
  }
}
