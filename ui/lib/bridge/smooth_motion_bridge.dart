import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'library_game.dart';
import 'smooth_motion_status.dart';

/// Smooth Motion driver/component operations.
final class SmoothMotionBridge {
  const SmoothMotionBridge._();

  static Future<Map<String, SmoothMotionStatus>> inspectMany(
    List<LibraryGame> games,
  ) async {
    final List<LibraryGame> withExe = games
        .where((LibraryGame game) => game.renderExe != null)
        .toList(growable: false);
    if (withExe.isEmpty) {
      return <String, SmoothMotionStatus>{};
    }

    final String request = jsonEncode(<String, Object?>{
      'exePaths': withExe
          .map((LibraryGame game) => game.renderExe)
          .toList(growable: false),
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.smInspectManyJson(request);
    });
    return _parseMany(reply, withExe);
  }

  static Future<SmoothMotionStatus> enable(LibraryGame game) =>
      _change(game, 'fgm_sm_enable_json');

  static Future<SmoothMotionStatus> disable(LibraryGame game) =>
      _change(game, 'fgm_sm_disable_json');

  static Future<SmoothMotionStatus> restore(LibraryGame game) =>
      _change(game, 'fgm_sm_restore_json');

  static Future<SmoothMotionStatus> _change(
    LibraryGame game,
    String command,
  ) async {
    final String? exe = game.renderExe;
    if (exe == null) {
      return const SmoothMotionStatus(code: 'NoRenderExe');
    }
    final String request = jsonEncode(<String, Object?>{
      'game': game.name,
      'exePath': exe,
      'targetDir': game.targetDirectory,
    });
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      if (core == null) {
        return null;
      }
      return switch (command) {
        'fgm_sm_disable_json' => core.smDisableJson(request),
        'fgm_sm_restore_json' => core.smRestoreJson(request),
        _ => core.smEnableJson(request),
      };
    });
    return _parseOne(reply);
  }

  static Map<String, SmoothMotionStatus> _parseMany(
    String? reply,
    List<LibraryGame> games,
  ) {
    if (reply == null) {
      return <String, SmoothMotionStatus>{};
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return <String, SmoothMotionStatus>{};
      }
      final List<Object?> states =
          decoded['states'] as List<Object?>? ?? <Object?>[];
      final Map<String, SmoothMotionStatus> result =
          <String, SmoothMotionStatus>{};
      for (var index = 0; index < states.length && index < games.length; index++) {
        final Object? state = states[index];
        if (state is Map) {
          result[games[index].id] = SmoothMotionStatus.fromJson(
            Map<String, Object?>.from(state),
          );
        }
      }
      return result;
    } on FormatException {
      return <String, SmoothMotionStatus>{};
    }
  }

  static SmoothMotionStatus _parseOne(String? reply) {
    if (reply == null) {
      return const SmoothMotionStatus(code: 'CoreUnavailable');
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map) {
        return const SmoothMotionStatus(code: 'InvalidReply');
      }
      if (decoded['ok'] != true) {
        return SmoothMotionStatus(
          code: decoded['code'] as String? ?? 'Internal',
        );
      }
      return SmoothMotionStatus.fromJson(Map<String, Object?>.from(decoded));
    } on FormatException {
      return const SmoothMotionStatus(code: 'InvalidReply');
    }
  }
}
