import 'dart:convert';

/// Result of an update check or download.
final class UpdateInfo {
  const UpdateInfo({
    required this.ok,
    this.available = false,
    this.currentVersion = '',
    this.version = '',
    this.notes = '',
    this.pageUrl,
    this.path,
    this.portable = false,
    this.installerSize = 0,
    this.code,
  });

  factory UpdateInfo.parse(String? reply) {
    if (reply == null) {
      return const UpdateInfo(ok: false, code: 'CoreUnavailable');
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map) {
        return const UpdateInfo(ok: false, code: 'InvalidReply');
      }
      if (decoded['ok'] != true) {
        return UpdateInfo(
          ok: false,
          code: decoded['code'] as String? ?? 'Internal',
        );
      }
      final Object? installer = decoded['installer'];
      return UpdateInfo(
        ok: true,
        available: decoded['available'] == true,
        currentVersion: decoded['currentVersion'] as String? ?? '',
        version: decoded['version'] as String? ?? '',
        notes: decoded['notes'] as String? ?? '',
        pageUrl: decoded['pageUrl'] as String?,
        path: decoded['path'] as String?,
        portable: decoded['portable'] == true,
        installerSize: installer is Map ? _int(installer['size']) : 0,
      );
    } on FormatException {
      return const UpdateInfo(ok: false, code: 'InvalidReply');
    }
  }

  final bool ok;
  final bool available;
  final String currentVersion;
  final String version;
  final String notes;
  final String? pageUrl;
  final String? path;
  final bool portable;
  final int installerSize;
  final String? code;

  bool get downloaded => (path ?? '').isNotEmpty;

  static int _int(Object? value) => value is int ? value : 0;
}
