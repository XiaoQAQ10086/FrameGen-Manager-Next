import 'dart:convert';

import 'anr_package.dart';

/// Result of an ANR import/deploy/restore call.
final class AnrResult {
  const AnrResult({
    required this.ok,
    this.package,
    this.proxyModule,
    this.filesWritten = 0,
    this.cancelled = false,
    this.code,
  });

  factory AnrResult.parse(String? reply) {
    if (reply == null) {
      return const AnrResult(ok: false, code: 'CoreUnavailable');
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return AnrResult(
          ok: false,
          code: decoded is Map ? decoded['code'] as String? ?? 'Internal' : 'InvalidReply',
        );
      }
      final Object? package = decoded['package'];
      return AnrResult(
        ok: true,
        cancelled: decoded['cancelled'] == true,
        package: package is Map
            ? AnrPackage.fromJson(Map<String, Object?>.from(package))
            : null,
        proxyModule: decoded['proxyModule'] as String?,
        filesWritten: decoded['filesWritten'] is int
            ? decoded['filesWritten'] as int
            : 0,
      );
    } on FormatException {
      return const AnrResult(ok: false, code: 'InvalidReply');
    }
  }

  final bool ok;
  final AnrPackage? package;
  final String? proxyModule;
  final int filesWritten;
  final bool cancelled;
  final String? code;
}
