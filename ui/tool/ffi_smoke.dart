import 'dart:convert';
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

  final String? libraryJson = core.listLibraryJson();
  if (libraryJson == null) {
    stderr.writeln('library list returned null');
    exitCode = 1;
    return;
  }
  final Object? decoded = jsonDecode(libraryJson);
  if (decoded is! Map || decoded['ok'] != true) {
    stderr.writeln('unexpected library reply: $libraryJson');
    exitCode = 1;
  } else {
    stdout.writeln('library reply ok, games=${(decoded['games'] as List).length}');
  }

  final String? inspectReply = core.inspectJson('{}');
  if (inspectReply == null || !inspectReply.contains('"ok":false')) {
    stderr.writeln('unexpected inspect reply: $inspectReply');
    exitCode = 1;
  } else {
    stdout.writeln('inspect error envelope ok');
  }
}
