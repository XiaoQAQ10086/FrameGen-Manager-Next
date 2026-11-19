/// Imported ANR package metadata.
final class AnrPackage {
  const AnrPackage({required this.kind, required this.version});

  factory AnrPackage.fromJson(Map<String, Object?> json) {
    return AnrPackage(
      kind: json['kind'] as String? ?? 'full',
      version: json['version'] as String? ?? 'unknown',
    );
  }

  final String kind;
  final String version;
}
