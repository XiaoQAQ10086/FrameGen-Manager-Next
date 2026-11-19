import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'game_modes.dart';
import 'library_add_result.dart';
import 'library_game.dart';
import 'library_snapshot.dart';

/// Parses the JSON replies from `fgm_library_*_json`.
final class LibraryBridge {
  const LibraryBridge._();

  static LibrarySnapshot list(CoreLibrary? core) {
    if (core == null) {
      return const LibrarySnapshot.failure('CoreUnavailable', 'fgm_ffi.dll');
    }
    return _parse(core.listLibraryJson());
  }

  static Future<LibrarySnapshot> scan(CoreLibrary? core) async {
    if (core == null) {
      return const LibrarySnapshot.failure('CoreUnavailable', 'fgm_ffi.dll');
    }
    final String? reply = await Isolate.run(() {
      final CoreLibrary? local = CoreLibrary.tryOpen();
      return local?.scanLibraryJson();
    });
    return _parse(reply);
  }

  /// Reads manifest-only deployment badges for all games.
  static Future<Map<String, GameModes>> modes() async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.gameModesJson('{}');
    });
    return GameModes.parse(reply);
  }

  /// Opens the native executable picker and stores the selection.
  static Future<LibraryAddResult> addManual() async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.libraryAddManualJson('{}');
    });
    return LibraryAddResult.parse(reply);
  }

  /// Starts the game through Steam/Epic or its render executable.
  static Future<bool> launch(LibraryGame game) async {
    final String request = jsonEncode(<String, Object?>{'gameId': game.id});
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.launchGameJson(request);
    });
    return _ok(reply);
  }

  /// Returns the local cover path, downloading it on first use.
  static Future<String?> cover(LibraryGame game) async {
    if (game.source != 'steam') {
      return null;
    }
    final String request = jsonEncode(<String, Object?>{'gameId': game.id});
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.coverJson(request);
    });
    if (reply == null) {
      return null;
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return null;
      }
      return decoded['path'] as String?;
    } on FormatException {
      return null;
    }
  }

  static bool _ok(String? reply) {
    if (reply == null) {
      return false;
    }
    try {
      final Object? decoded = jsonDecode(reply);
      return decoded is Map && decoded['ok'] == true;
    } on FormatException {
      return false;
    }
  }

  static LibrarySnapshot _parse(String? reply) {
    if (reply == null) {
      return const LibrarySnapshot.failure('CoreUnavailable', 'empty reply');
    }

    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map) {
        return const LibrarySnapshot.failure('InvalidReply', 'not an object');
      }
      if (decoded['ok'] != true) {
        final String code = decoded['code'] as String? ?? 'Internal';
        return LibrarySnapshot.failure(
          code,
          _contextDetail(decoded['context']) ?? 'native error',
        );
      }
      final List<Object?> raw = decoded['games'] as List<Object?>? ?? <Object?>[];
      final List<LibraryGame> games = raw
          .whereType<Map<Object?, Object?>>()
          .map((Map<Object?, Object?> value) =>
              LibraryGame.fromJson(Map<String, Object?>.from(value)))
          .toList(growable: false);
      return LibrarySnapshot(games: games);
    } on FormatException catch (error) {
      return LibrarySnapshot.failure('InvalidReply', error.message);
    }
  }

  static String? _contextDetail(Object? context) {
    if (context is! List) {
      return null;
    }
    for (final Object? entry in context) {
      if (entry is List && entry.length >= 2 && entry[0] == 'reason') {
        return entry[1]?.toString();
      }
    }
    return null;
  }
}
