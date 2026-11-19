import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'dlss5_result.dart';
import 'library_game.dart';

/// DLSS5 pack + ReShade install.
final class Dlss5Bridge {
  const Dlss5Bridge._();

  static const String appVersion = '2.3.4';

  static Future<Dlss5Result> install(LibraryGame game) async {
    final String? exe = game.renderExe;
    if (exe == null) {
      return const Dlss5Result(ok: false, code: 'NoRenderExe');
    }
    final String request = jsonEncode(<String, Object?>{
      'exePath': exe,
      'targetDir': game.targetDirectory,
      'api': game.graphicsApi,
      'appVersion': appVersion,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.dlss5InstallJson(request);
    });
    return Dlss5Result.parse(reply);
  }

  /// Removes everything the install wrote; user-modified files are reported
  /// as conflicts and left in place.
  static Future<Dlss5Result> uninstall(LibraryGame game) async {
    final String request = jsonEncode(<String, Object?>{
      'targetDir': game.targetDirectory,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.dlss5UninstallJson(request);
    });
    return Dlss5Result.parse(reply);
  }
}
