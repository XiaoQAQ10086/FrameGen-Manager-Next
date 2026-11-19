import 'dart:convert';

/// Result of a DLSS5 install request.
final class Dlss5Result {
  const Dlss5Result({
    required this.ok,
    this.proxyModule,
    this.conflicts = const <String>[],
    this.code,
    this.detail,
  });

  factory Dlss5Result.parse(String? reply) {
    if (reply == null) {
      return const Dlss5Result(ok: false, code: 'CoreUnavailable');
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map) {
        return const Dlss5Result(ok: false, code: 'InvalidReply');
      }
      if (decoded['ok'] == true) {
        return Dlss5Result(
          ok: true,
          proxyModule: decoded['proxyModule'] as String? ?? '',
          conflicts: _stringList(decoded['conflicts']),
        );
      }
      return Dlss5Result(
        ok: false,
        code: decoded['code'] as String? ?? 'Internal',
        detail: _contextDetail(decoded['context']),
      );
    } on FormatException {
      return const Dlss5Result(ok: false, code: 'InvalidReply');
    }
  }

  final bool ok;
  final String? proxyModule;
  final List<String> conflicts;
  final String? code;
  final String? detail;

  static List<String> _stringList(Object? value) {
    if (value is! List) {
      return const <String>[];
    }
    return value.whereType<String>().toList(growable: false);
  }

  static String? _contextDetail(Object? context) {
    if (context is! List) {
      return null;
    }
    for (final Object? entry in context) {
      if (entry is List && entry.length >= 2 && entry[0] == 'reason') {
        return entry[1]?.toString();
      }
    }
    return null;
  }
}
