import 'dart:convert';

/// Manifest-only deployment state of one game across the feature modes.
final class GameModes {
  const GameModes({
    required this.gameId,
    this.framegen = 'notDeployed',
    this.dlss5 = 'notDeployed',
    this.anr = 'notDeployed',
  });

  factory GameModes.fromJson(Map<String, Object?> json) {
    return GameModes(
      gameId: json['gameId'] as String? ?? '',
      framegen: json['framegen'] as String? ?? 'notDeployed',
      dlss5: json['dlss5'] as String? ?? 'notDeployed',
      anr: json['anr'] as String? ?? 'notDeployed',
    );
  }

  final String gameId;
  final String framegen;
  final String dlss5;
  final String anr;

  bool get framegenDeployed => framegen != 'notDeployed';
  bool get dlss5Deployed => dlss5 != 'notDeployed';
  bool get anrDeployed => anr != 'notDeployed';
  bool get anyDeployed => framegenDeployed || dlss5Deployed || anrDeployed;

  static Map<String, GameModes> parse(String? reply) {
    if (reply == null) {
      return const <String, GameModes>{};
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return const <String, GameModes>{};
      }
      final Object? modes = decoded['modes'];
      if (modes is! List) {
        return const <String, GameModes>{};
      }
      return <String, GameModes>{
        for (final Object? entry in modes)
          if (entry is Map)
            (entry['gameId'] as String? ?? ''): GameModes.fromJson(
              Map<String, Object?>.from(entry),
            ),
      };
    } on FormatException {
      return const <String, GameModes>{};
    }
  }
}
