import 'dart:convert';
import 'dart:io';

/// Cache of resolved cover paths, kept for the life of the process and written
/// to `<dataRoot>/cache/covers/index.json` so a relaunch starts with covers.
///
/// Every navigation rebuilds a page from scratch, and the core re-resolves a
/// cover (including the store search and icon extraction fallbacks) whenever it
/// is asked. Remembering the answer removes the letter placeholder that used to
/// flash on each launch and each page switch.
///
/// Only real paths are cached: "no cover yet" is deliberately not remembered,
/// so a cover the core generates later still shows up on the next visit.
abstract final class CoverCache {
  static final Map<String, String> _paths = <String, String>{};
  static File? _index;

  /// Loads the index saved by the previous run. An empty root (tests, missing
  /// core) turns the cache into memory-only.
  static void load(String dataRoot) {
    if (dataRoot.isEmpty) {
      return;
    }
    final File file = File('$dataRoot/cache/covers/index.json');
    _index = file;
    try {
      if (!file.existsSync()) {
        return;
      }
      final Object? decoded = jsonDecode(file.readAsStringSync());
      if (decoded is! Map) {
        return;
      }
      for (final MapEntry<Object?, Object?> entry in decoded.entries) {
        final Object? key = entry.key;
        final Object? value = entry.value;
        if (key is String && value is String && value.isNotEmpty) {
          _paths[key] = value;
        }
      }
    } on FileSystemException {
      // A missing or unreadable index just costs one re-resolve.
    } on FormatException {
      // A corrupt index is rebuilt from the core.
    }
  }

  static bool knows(String gameId) => _paths.containsKey(gameId);

  static void put(String gameId, String? path) {
    if (path == null || path.isEmpty) {
      _paths.remove(gameId);
      return;
    }
    _paths[gameId] = path;
  }

  static Map<String, String?> snapshot() => Map<String, String?>.of(_paths);

  /// Best effort: a failed write only costs one re-resolve next launch.
  static void save() {
    final File? file = _index;
    if (file == null) {
      return;
    }
    try {
      file.parent.createSync(recursive: true);
      file.writeAsStringSync(jsonEncode(_paths));
    } on FileSystemException {
      // Ignored on purpose.
    }
  }
}
