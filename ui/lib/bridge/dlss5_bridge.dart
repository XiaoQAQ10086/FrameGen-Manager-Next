import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'dlss5_hotkey_result.dart';
import 'dlss5_result.dart';
import 'dlss5_state.dart';
import 'library_game.dart';

/// DLSS5 pack + ReShade install.
final class Dlss5Bridge {
  const Dlss5Bridge._();

  /// Reads deployment state, ReShade presence and legacy residue.
  static Future<Dlss5State> inspect(LibraryGame game) async {
    final String request = jsonEncode(<String, Object?>{
      'targetDir': game.targetDirectory,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.dlss5InspectJson(request);
    });
    return Dlss5State.parse(reply);
  }

  static Future<Dlss5Result> install(
    LibraryGame game, {
    String? api,
    String? proxy,
    int? hotkey,
  }) async {
    final String? exe = game.renderExe;
    if (exe == null) {
      return const Dlss5Result(ok: false, code: 'NoRenderExe');
    }
    final String request = jsonEncode(<String, Object?>{
      'exePath': exe,
      'targetDir': game.targetDirectory,
      'api': api ?? game.graphicsApi,
      'proxy': proxy,
      'hotkey': hotkey,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.dlss5InstallJson(request);
    });
    return Dlss5Result.parse(reply);
  }

  /// Deletes legacy RenoDX/DLSS5 files; returns the removed file count.
  static Future<int?> cleanLegacy(LibraryGame game) async {
    final String request = jsonEncode(<String, Object?>{
      'targetDir': game.targetDirectory,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.dlss5CleanLegacyJson(request);
    });
    if (reply == null) {
      return null;
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return null;
      }
      return decoded['removed'] as int? ?? 0;
    } on FormatException {
      return null;
    }
  }

  /// Writes `[INPUT] KeyOverlay` for an installed game.
  static Future<HotkeyOutcome> setHotkey(
    LibraryGame game,
    int virtualKey,
  ) async {
    final String request = jsonEncode(<String, Object?>{
      'targetDir': game.targetDirectory,
      'virtualKey': virtualKey,
      'exePath': game.renderExe,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.dlss5SetHotkeyJson(request);
    });
    if (reply == null) {
      return HotkeyOutcome.failed;
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return HotkeyOutcome.failed;
      }
      return decoded['queued'] == true
          ? HotkeyOutcome.queued
          : HotkeyOutcome.applied;
    } on FormatException {
      return HotkeyOutcome.failed;
    }
  }

  /// Removes the pack but keeps the ReShade proxy and its INI.
  static Future<Dlss5Result> restore(LibraryGame game) async {
    final String request = jsonEncode(<String, Object?>{
      'targetDir': game.targetDirectory,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.dlss5RestoreJson(request);
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
