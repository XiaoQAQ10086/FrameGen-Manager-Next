/// One game returned by the Rust library scanner.
final class LibraryGame {
  const LibraryGame({
    required this.id,
    required this.name,
    required this.source,
    required this.installDirectory,
    required this.targetDirectory,
    required this.graphicsApi,
    this.renderExe,
  });

  factory LibraryGame.fromJson(Map<String, Object?> json) {
    return LibraryGame(
      id: json['id'] as String? ?? '',
      name: json['name'] as String? ?? '',
      source: json['source'] as String? ?? 'unknown',
      installDirectory: json['installDirectory'] as String? ?? '',
      targetDirectory: json['targetDirectory'] as String? ?? '',
      graphicsApi: json['graphicsApi'] as String? ?? 'unknown',
      renderExe: json['renderExe'] as String?,
    );
  }

  final String id;
  final String name;
  final String source;
  final String installDirectory;
  final String targetDirectory;
  final String graphicsApi;
  final String? renderExe;
}
