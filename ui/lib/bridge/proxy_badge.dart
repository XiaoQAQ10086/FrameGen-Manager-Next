import 'dart:convert';

/// One occupied proxy module and the deployment family that owns it.
final class ProxyBadge {
  const ProxyBadge({
    required this.module,
    required this.owner,
    required this.ownedByFramegen,
  });

  factory ProxyBadge.fromJson(Map<String, Object?> json) {
    return ProxyBadge(
      module: json['module'] as String? ?? '',
      owner: json['owner'] as String? ?? 'other',
      ownedByFramegen: json['ownedByFramegen'] == true,
    );
  }

  final String module;
  final String owner;
  final bool ownedByFramegen;

  static List<ProxyBadge> parse(String? reply) {
    if (reply == null) {
      return const <ProxyBadge>[];
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return const <ProxyBadge>[];
      }
      final Object? proxies = decoded['proxies'];
      if (proxies is! List) {
        return const <ProxyBadge>[];
      }
      return proxies
          .whereType<Map<Object?, Object?>>()
          .map(
            (Map<Object?, Object?> value) =>
                ProxyBadge.fromJson(Map<String, Object?>.from(value)),
          )
          .toList(growable: false);
    } on FormatException {
      return const <ProxyBadge>[];
    }
  }
}
