import 'dart:convert';

/// Result of one deploy/restore/inspect FFI call.
final class DeploymentResult {
  const DeploymentResult({
    required this.ok,
    this.state,
    this.code,
    this.detail,
    this.filesWritten = 0,
    this.filesBackedUp = 0,
    this.restoredFiles = 0,
    this.removedFiles = 0,
    this.conflicts = const <String>[],
    this.warnings = const <String>[],
    this.details = const <String>[],
  });

  factory DeploymentResult.parse(String? reply) {
    if (reply == null) {
      return const DeploymentResult(
        ok: false,
        code: 'CoreUnavailable',
        detail: 'empty reply',
      );
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map) {
        return const DeploymentResult(
          ok: false,
          code: 'InvalidReply',
          detail: 'not an object',
        );
      }
      final bool ok = decoded['ok'] == true;
      return DeploymentResult(
        ok: ok,
        state: decoded['state'] as String?,
        code: ok ? null : decoded['code'] as String? ?? 'Internal',
        detail: ok ? null : _contextDetail(decoded['context']),
        filesWritten: _int(decoded['filesWritten']),
        filesBackedUp: _int(decoded['filesBackedUp']),
        restoredFiles: _int(decoded['restoredFiles']),
        removedFiles: _int(decoded['removedFiles']),
        conflicts: _strings(decoded['conflicts']),
        warnings: _strings(decoded['warnings']),
        details: _strings(decoded['details']),
      );
    } on FormatException catch (error) {
      return DeploymentResult(
        ok: false,
        code: 'InvalidReply',
        detail: error.message,
      );
    }
  }

  final bool ok;
  final String? state;
  final String? code;
  final String? detail;
  final int filesWritten;
  final int filesBackedUp;
  final int restoredFiles;
  final int removedFiles;
  final List<String> conflicts;
  final List<String> warnings;
  final List<String> details;

  static int _int(Object? value) => value is int ? value : 0;

  static List<String> _strings(Object? value) {
    if (value is! List) {
      return const <String>[];
    }
    return value.map((Object? item) => item.toString()).toList(growable: false);
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
