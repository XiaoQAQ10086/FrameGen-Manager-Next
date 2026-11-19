import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'update_info.dart';

/// Update check, download and installer launch behind the Rust core.
final class UpdateBridge {
  const UpdateBridge._();

  static Future<UpdateInfo> check() async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.updateCheckJson();
    });
    return UpdateInfo.parse(reply);
  }

  static Future<UpdateInfo> download() async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.updateDownloadJson();
    });
    return UpdateInfo.parse(reply);
  }

  static Future<bool> install(String path) async {
    final String request = jsonEncode(<String, Object?>{'path': path});
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.updateInstallJson(request);
    });
    return _ok(reply);
  }

  static Future<bool> open(String url) async {
    final String request = jsonEncode(<String, Object?>{'url': url});
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.updateOpenJson(request);
    });
    return _ok(reply);
  }

  static bool _ok(String? reply) {
    if (reply == null) {
      return false;
    }
    try {
      final Object? decoded = jsonDecode(reply);
      return decoded is Map && decoded['ok'] == true;
    } on FormatException {
      return false;
    }
  }
}
