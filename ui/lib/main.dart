import 'dart:async';
import 'dart:convert';

import 'package:material_ui/material_ui.dart';

import 'app/app.dart';
import 'bridge/core_library.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  final CoreLibrary? core = CoreLibrary.tryOpen();
  runApp(FgmApp(core: core));
  if (core == null) {
    return;
  }
  WidgetsBinding.instance.addPostFrameCallback((_) => _restoreWindow(core));
}

/// Retries until the Flutter runner has shown its window, then persists the
/// size periodically so the next launch reopens with the same bounds.
Future<void> _restoreWindow(CoreLibrary core) async {
  for (var attempt = 0; attempt < 15; attempt++) {
    if (_applied(core.windowRestoreJson())) {
      break;
    }
    await Future<void>.delayed(const Duration(milliseconds: 100));
  }
  Timer.periodic(const Duration(seconds: 1), (_) => core.windowSaveJson());
  Timer.periodic(
    const Duration(seconds: 30),
    (_) => core.dlss5ProcessHotkeyQueueJson(),
  );
}

bool _applied(String? reply) {
  if (reply == null) {
    return false;
  }
  try {
    final Object? decoded = jsonDecode(reply);
    return decoded is Map && decoded['ok'] == true && decoded['applied'] == true;
  } on FormatException {
    return false;
  }
}
