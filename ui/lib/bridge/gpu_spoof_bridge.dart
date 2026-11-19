import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'gpu_spoof_state.dart';

/// Registry-backed GPU name spoofing behind the Rust core.
final class GpuSpoofBridge {
  const GpuSpoofBridge._();

  static Future<GpuSpoofState> inspect() async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.gpuSpoofInspectJson();
    });
    return GpuSpoofState.parse(reply);
  }

  /// Returns null on success, otherwise the error code.
  static Future<String?> apply(int adapterIndex, String preset) async {
    final String request = jsonEncode(<String, Object?>{
      'adapterIndex': adapterIndex,
      'preset': preset,
    });
    return _request((CoreLibrary core) => core.gpuSpoofApplyJson(request));
  }

  /// Returns null on success, otherwise the error code.
  static Future<String?> restore(int adapterIndex, String mode) async {
    final String request = jsonEncode(<String, Object?>{
      'adapterIndex': adapterIndex,
      'mode': mode,
    });
    return _request((CoreLibrary core) => core.gpuSpoofRestoreJson(request));
  }

  static Future<String?> _request(String? Function(CoreLibrary core) call) async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core == null ? null : call(core);
    });
    if (reply == null) {
      return 'CoreUnavailable';
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return decoded is Map ? decoded['code'] as String? ?? 'Internal' : 'InvalidReply';
      }
      return null;
    } on FormatException {
      return 'InvalidReply';
    }
  }
}
