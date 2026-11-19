import 'dart:convert';
import 'dart:isolate';

import 'anr_package.dart';
import 'anr_result.dart';
import 'core_library.dart';
import 'library_game.dart';

/// ANR package import and deployment.
final class AnrBridge {
  const AnrBridge._();

  static AnrPackage? current(CoreLibrary? core) {
    if (core == null) {
      return null;
    }
    final String? reply = core.anrCurrentJson();
    if (reply == null) {
      return null;
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return null;
      }
      final Object? package = decoded['package'];
      if (package is Map) {
        return AnrPackage.fromJson(Map<String, Object?>.from(package));
      }
    } on FormatException {
      return null;
    }
    return null;
  }

  static Future<AnrResult> importPackage(CoreLibrary? core) async {
    if (core == null) {
      return const AnrResult(ok: false, code: 'CoreUnavailable');
    }
    final String? reply = await Isolate.run(() {
      final CoreLibrary? local = CoreLibrary.tryOpen();
      return local?.anrImportJson('{}');
    });
    return AnrResult.parse(reply);
  }

  static Future<AnrResult> downloadLatest(CoreLibrary? core) async {
    if (core == null) {
      return const AnrResult(ok: false, code: 'CoreUnavailable');
    }
    final String? reply = await Isolate.run(() {
      final CoreLibrary? local = CoreLibrary.tryOpen();
      return local?.anrDownloadJson('{}');
    });
    return AnrResult.parse(reply);
  }

  static Future<AnrResult> deploy(LibraryGame game) async {
    final String? exe = game.renderExe;
    if (exe == null) {
      return const AnrResult(ok: false, code: 'NoRenderExe');
    }
    final String request = jsonEncode(<String, Object?>{
      'targetDir': game.targetDirectory,
      'exePath': exe,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.anrDeployJson(request);
    });
    return AnrResult.parse(reply);
  }

  static Future<AnrResult> restore(LibraryGame game) async {
    final String request = jsonEncode(<String, Object?>{
      'targetDir': game.targetDirectory,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.anrRestoreJson(request);
    });
    return AnrResult.parse(reply);
  }
}
