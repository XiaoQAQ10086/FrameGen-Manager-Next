/// Process-wide cache of resolved cover paths.
///
/// Every navigation rebuilds the page from scratch, so without this each switch
/// re-asks the core for every cover and paints the letter placeholder again
/// until the answers arrive.
///
/// Only real paths are cached: "no cover yet" is deliberately not remembered,
/// so a cover that the core generates later still shows up on the next visit.
abstract final class CoverCache {
  static final Map<String, String> _paths = <String, String>{};

  static bool knows(String gameId) => _paths.containsKey(gameId);

  static void put(String gameId, String? path) {
    if (path == null || path.isEmpty) {
      _paths.remove(gameId);
      return;
    }
    _paths[gameId] = path;
  }

  static Map<String, String?> snapshot() => Map<String, String?>.of(_paths);
}
