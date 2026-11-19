import 'dart:convert';

import 'core_library.dart';

/// Reads the tail of the operation log.
final class LogsBridge {
  const LogsBridge._();

  static List<String> tail(CoreLibrary? core, {int maxLines = 500}) {
    if (core == null) {
      return const <String>[];
    }
    final String? reply = core.logsTailJson(
      jsonEncode(<String, Object?>{'maxLines': maxLines}),
    );
    if (reply == null) {
      return const <String>[];
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return const <String>[];
      }
      final List<Object?> lines =
          decoded['lines'] as List<Object?>? ?? <Object?>[];
      return lines.map((Object? line) => line.toString()).toList(growable: false);
    } on FormatException {
      return const <String>[];
    }
  }
}
