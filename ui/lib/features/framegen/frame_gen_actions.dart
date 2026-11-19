import 'package:material_ui/material_ui.dart';

import '../../bridge/deploy_dialog_result.dart';
import '../../bridge/library_game.dart';
import '../../bridge/proxy_badge.dart';
import '../../design/legacy_dialog.dart';
import '../../l10n/app_localizations.dart';
import 'framegen_deploy_dialog.dart';

/// Dialog helpers shared by the frame-generation page actions.
final class FrameGenActions {
  const FrameGenActions._();

  static Future<DeployDialogResult?> showDeployOptions(
    BuildContext context,
    AppLocalizations l10n,
    LibraryGame game, {
    bool deployed = false,
    List<ProxyBadge> proxies = const <ProxyBadge>[],
    String? coverPath,
  }) {
    return showLegacyDialog<DeployDialogResult>(
      context: context,
      builder: (BuildContext context) => FrameGenDeployDialog(
        game: game,
        deployed: deployed,
        proxies: proxies,
        coverPath: coverPath,
      ),
    );
  }
}
