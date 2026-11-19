import 'dart:convert';

import 'library_game.dart';

/// Result of adding a manual game through the native picker.
final class LibraryAddResult {
  const LibraryAddResult({
    this.ok = false,
    this.cancelled = false,
    this.game,
    this.code,
  });

  factory LibraryAddResult.parse(String? reply) {
    if (reply == null) {
      return const LibraryAddResult(code: 'CoreUnavailable');
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map) {
        return const LibraryAddResult(code: 'InvalidReply');
      }
      if (decoded['ok'] != true) {
        return LibraryAddResult(code: decoded['code'] as String? ?? 'Internal');
      }
      if (decoded['cancelled'] == true) {
        return const LibraryAddResult(ok: true, cancelled: true);
      }
      final Object? raw = decoded['game'];
      return LibraryAddResult(
        ok: true,
        game: raw is Map
            ? LibraryGame.fromJson(Map<String, Object?>.from(raw))
            : null,
      );
    } on FormatException {
      return const LibraryAddResult(code: 'InvalidReply');
    }
  }

  final bool ok;
  final bool cancelled;
  final LibraryGame? game;
  final String? code;
}
