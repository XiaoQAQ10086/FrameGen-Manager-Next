import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
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
