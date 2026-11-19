import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'gpu_info.dart';

/// Loads GPU information without blocking the UI thread.
final class GpuBridge {
  const GpuBridge._();

  static Future<GpuInfo?> load(CoreLibrary? core) async {
    if (core == null) {
      return null;
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
        return GpuInfo.fromJson(Map<String, Object?>.from(gpu));
      }
    } on FormatException {
      return null;
    }
    return null;
  }
}
