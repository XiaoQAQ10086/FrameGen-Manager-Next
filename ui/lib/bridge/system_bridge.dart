import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'library_game.dart';
import 'proxy_badge.dart';
import 'system_paths.dart';

/// Asset folders, Windows settings pages and proxy ownership.
final class SystemBridge {
  const SystemBridge._();

  static Future<SystemPaths> paths() async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.invokeJson('fgm_paths_json');
    });
    return SystemPaths.parse(reply);
  }

  static Future<bool> openFolder(String which) =>
      _request('fgm_open_folder_json', jsonEncode({'which': which}));

  static Future<bool> openHags() => _json('fgm_open_hags_json');

  static Future<bool> openPath(String path) =>
      _request('fgm_open_path_json', jsonEncode({'path': path}));

  static Future<bool> restartComputer() => _json('fgm_restart_computer_json');

  static Future<void> openUrl(String url) async {
    if (url.isEmpty) {
      return;
    }
    await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      core?.invokeRequestJson('fgm_update_open_json', jsonEncode({'url': url}));
    });
  }

  static Future<List<ProxyBadge>> proxyStatus(LibraryGame game) async {
    final String request = jsonEncode({'targetDir': game.targetDirectory});
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.invokeRequestJson('fgm_proxy_status_json', request);
    });
    return ProxyBadge.parse(reply);
  }

  static Future<bool> _request(String symbol, String request) async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.invokeRequestJson(symbol, request);
    });
    return _ok(reply);
  }

  static Future<bool> _json(String symbol) async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      return core?.invokeJson(symbol);
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
