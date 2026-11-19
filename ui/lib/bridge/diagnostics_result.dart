import 'dart:convert';

/// Result of exporting a diagnostics bundle through the save dialog.
final class DiagnosticsResult {
  const DiagnosticsResult({
    this.ok = false,
    this.cancelled = false,
    this.path,
    this.code,
  });

  factory DiagnosticsResult.parse(String? reply) {
    if (reply == null) {
      return const DiagnosticsResult(code: 'CoreUnavailable');
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map) {
        return const DiagnosticsResult(code: 'InvalidReply');
      }
      if (decoded['ok'] != true) {
        return DiagnosticsResult(code: decoded['code'] as String? ?? 'Internal');
      }
      if (decoded['cancelled'] == true) {
        return const DiagnosticsResult(ok: true, cancelled: true);
      }
      return DiagnosticsResult(ok: true, path: decoded['path'] as String?);
    } on FormatException {
      return const DiagnosticsResult(code: 'InvalidReply');
    }
  }

  final bool ok;
  final bool cancelled;
  final String? path;
  final String? code;
}
