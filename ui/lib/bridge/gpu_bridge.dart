import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'gpu_info.dart';

/// Loads GPU information without blocking the UI thread.
final class GpuBridge {
  const GpuBridge._();

  static GpuInfo? _last;

  /// Loads GPU info, falling back to the last successful probe.
  static Future<GpuInfo?> load(CoreLibrary? core) async {
    if (core == null) {
      return _last;
    }
    final String? reply = await Isolate.run(() {
      final CoreLibrary? local = CoreLibrary.tryOpen();
      return local?.gpuJson();
    });
    if (reply == null) {
      return null;
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return null;
      }
      final Object? gpu = decoded['gpu'];
      if (gpu is Map) {
        _last = GpuInfo.fromJson(Map<String, Object?>.from(gpu));
        return _last;
      }
    } on FormatException {
      return _last;
    }
    return _last;
  }
}
