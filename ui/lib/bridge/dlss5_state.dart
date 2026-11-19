import 'dart:convert';

/// Deployment state of the DLSS5 pack for one game.
final class Dlss5State {
  const Dlss5State({
    required this.state,
    this.hasManifest = false,
    this.packVersion,
    this.proxyModule,
    this.reShadePresent = false,
    this.legacyFiles = const <String>[],
    this.errorCode,
  });

  factory Dlss5State.parse(String? reply) {
    if (reply == null) {
      return const Dlss5State(state: 'coreUnavailable');
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map) {
        return const Dlss5State(state: 'unknown');
      }
      if (decoded['ok'] != true) {
        return Dlss5State(
          state: 'unknown',
          errorCode: decoded['code'] as String? ?? 'Internal',
        );
      }
      return Dlss5State(
        state: decoded['state'] as String? ?? 'unknown',
        hasManifest: decoded['hasManifest'] == true,
        packVersion: decoded['packVersion'] as String?,
        proxyModule: decoded['proxyModule'] as String?,
        reShadePresent: decoded['reShadePresent'] == true,
        legacyFiles: _stringList(decoded['legacyFiles']),
      );
    } on FormatException {
      return const Dlss5State(state: 'unknown');
    }
  }

  final String state;
  final bool hasManifest;
  final String? packVersion;
  final String? proxyModule;
  final bool reShadePresent;
  final List<String> legacyFiles;
  final String? errorCode;

  /// True when the pack (or files matching it) is present on disk.
  bool get isDeployed =>
      state == 'installed' ||
      state == 'outdated' ||
      state == 'partial' ||
      state == 'conflict' ||
      state == 'installedWithoutManifest';

  /// True when a redeploy would add or repair files.
  bool get needsUpdate =>
      state == 'outdated' ||
      state == 'partial' ||
      state == 'conflict' ||
      state == 'installedWithoutManifest';

  bool get hasLegacyResidue => legacyFiles.isNotEmpty;

  static List<String> _stringList(Object? value) {
    if (value is! List) {
      return const <String>[];
    }
    return value.whereType<String>().toList(growable: false);
  }
}
