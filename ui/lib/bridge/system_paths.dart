import 'dart:convert';

/// Asset/data directories and public links surfaced to the UI.
final class SystemPaths {
  const SystemPaths({
    this.dataRoot = '',
    this.packsDirectory = '',
    this.bundledPacksDirectory = '',
    this.licensesDirectory = '',
    this.logsDirectory = '',
    this.repository = '',
    this.netdiskUrl = '',
    this.anrBaiduUrl = '',
    this.anrQuarkUrl = '',
  });

  factory SystemPaths.parse(String? reply) {
    if (reply == null) {
      return const SystemPaths();
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return const SystemPaths();
      }
      return SystemPaths(
        dataRoot: decoded['dataRoot'] as String? ?? '',
        packsDirectory: decoded['packsDirectory'] as String? ?? '',
        bundledPacksDirectory:
            decoded['bundledPacksDirectory'] as String? ?? '',
        licensesDirectory: decoded['licensesDirectory'] as String? ?? '',
        logsDirectory: decoded['logsDirectory'] as String? ?? '',
        repository: decoded['repository'] as String? ?? '',
        netdiskUrl: decoded['netdiskUrl'] as String? ?? '',
        anrBaiduUrl: decoded['anrBaiduUrl'] as String? ?? '',
        anrQuarkUrl: decoded['anrQuarkUrl'] as String? ?? '',
      );
    } on FormatException {
      return const SystemPaths();
    }
  }

  final String dataRoot;
  final String packsDirectory;
  final String bundledPacksDirectory;
  final String licensesDirectory;
  final String logsDirectory;
  final String repository;
  final String netdiskUrl;
  final String anrBaiduUrl;
  final String anrQuarkUrl;
}
