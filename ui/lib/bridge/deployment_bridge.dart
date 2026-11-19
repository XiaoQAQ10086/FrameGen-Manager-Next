import 'dart:convert';
import 'dart:isolate';

import 'core_library.dart';
import 'deploy_options.dart';
import 'deployment_result.dart';
import 'library_game.dart';

/// Runs deploy/restore/inspect requests in a background isolate.
final class DeploymentBridge {
  const DeploymentBridge._();

  static const String appVersion = '2.3.4';

  static Future<DeploymentResult> deploy({
    required LibraryGame game,
    String packId = 'framegen.sm86',
    DeployOptions? options,
    String? gameExe,
  }) {
    final String request = jsonEncode(<String, Object?>{
      'packId': packId,
      'targetDir': game.targetDirectory,
      'proxySlot': options?.proxySlot,
      'ini': options?.iniOverrides() ?? const <Map<String, String>>[],
      'gameExe': gameExe,
      'appVersion': appVersion,
    });
    return _run('fgm_deploy_json', request);
  }

  static Future<DeploymentResult> restore({
    required LibraryGame game,
    String family = 'framegen.sm86',
  }) {
    final String request = jsonEncode(<String, Object?>{
      'targetDir': game.targetDirectory,
      'family': family,
    });
    return _run('fgm_restore_json', request);
  }

  static Future<DeploymentResult> inspect({
    required LibraryGame game,
    String family = 'framegen.sm86',
    String packId = 'framegen.sm86',
  }) {
    final String request = jsonEncode(<String, Object?>{
      'targetDir': game.targetDirectory,
      'family': family,
      'packId': packId,
    });
    return _run('fgm_inspect_json', request);
  }

  static Future<DeploymentResult> _run(String command, String request) async {
    final String? reply = await Isolate.run(() {
      final CoreLibrary? core = CoreLibrary.tryOpen();
      if (core == null) {
        return null;
      }
      return switch (command) {
        'fgm_restore_json' => core.restoreJson(request),
        'fgm_inspect_json' => core.inspectJson(request),
        _ => core.deployJson(request),
      };
    });
    return DeploymentResult.parse(reply);
  }
}
