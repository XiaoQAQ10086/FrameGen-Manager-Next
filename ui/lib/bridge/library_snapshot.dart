import 'library_game.dart';

/// Result of a library list/scan call. `errorCode` is set when the native call
/// failed; games stay empty in that case.
final class LibrarySnapshot {
  const LibrarySnapshot({required this.games, this.errorCode, this.errorDetail});

  const LibrarySnapshot.failure(String this.errorCode, String this.errorDetail)
    : games = const <LibraryGame>[];

  final List<LibraryGame> games;
  final String? errorCode;
  final String? errorDetail;

  bool get hasError => errorCode != null;
}
