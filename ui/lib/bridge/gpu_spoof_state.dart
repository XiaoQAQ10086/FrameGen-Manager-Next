import 'dart:convert';

/// One NVIDIA adapter exposed by the GPU spoof API.
final class SpoofAdapter {
  const SpoofAdapter({
    required this.index,
    required this.displayName,
    required this.driverName,
    required this.deviceDesc,
    required this.instancePath,
  });

  factory SpoofAdapter.fromJson(Map<String, Object?> json) {
    return SpoofAdapter(
      index: json['index'] as int? ?? 0,
      displayName: json['displayName'] as String? ?? '',
      driverName: json['driverName'] as String? ?? '',
      deviceDesc: json['deviceDesc'] as String? ?? '',
      instancePath: json['instancePath'] as String? ?? '',
    );
  }

  final int index;
  final String displayName;
  final String driverName;
  final String deviceDesc;
  final String instancePath;
}

/// Adapters, whitelisted presets and the current backup state.
final class GpuSpoofState {
  const GpuSpoofState({
    required this.elevated,
    required this.adapters,
    required this.presets,
    this.appliedName,
    this.originalName,
    this.liveName,
    this.errorCode,
  });

  factory GpuSpoofState.failure(String code) =>
      GpuSpoofState(elevated: false, adapters: const [], presets: const [], errorCode: code);

  factory GpuSpoofState.parse(String? reply) {
    if (reply == null) {
      return GpuSpoofState.failure('CoreUnavailable');
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map) {
        return GpuSpoofState.failure('InvalidReply');
      }
      if (decoded['ok'] != true) {
        return GpuSpoofState.failure(decoded['code'] as String? ?? 'Internal');
      }
      final List<Object?> adapters = decoded['adapters'] as List<Object?>? ?? <Object?>[];
      final List<Object?> presets = decoded['presets'] as List<Object?>? ?? <Object?>[];
      final Object? applied = decoded['applied'];
      return GpuSpoofState(
        elevated: decoded['elevated'] == true,
        adapters: adapters
            .whereType<Map<Object?, Object?>>()
            .map(
              (Map<Object?, Object?> value) =>
                  SpoofAdapter.fromJson(Map<String, Object?>.from(value)),
            )
            .toList(growable: false),
        presets: presets.whereType<String>().toList(growable: false),
        appliedName: applied is Map ? applied['appliedName'] as String? : null,
        originalName: applied is Map
            ? _displayName(applied['originalDeviceDesc'] as String? ?? '')
            : null,
        liveName: decoded['liveName'] as String?,
      );
    } on FormatException {
      return GpuSpoofState.failure('InvalidReply');
    }
  }

  final bool elevated;
  final List<SpoofAdapter> adapters;
  final List<String> presets;
  final String? appliedName;
  final String? originalName;

  /// Name reported by the running driver; ignores stale registry entries.
  final String? liveName;
  final String? errorCode;

  bool get hasBackup => appliedName != null;

  static String _displayName(String deviceDesc) {
    final int index = deviceDesc.lastIndexOf(';');
    return index >= 0 ? deviceDesc.substring(index + 1).trim() : deviceDesc;
  }
}
