import 'package:material_ui/material_ui.dart';

import '../../bridge/deploy_options.dart';
import '../../bridge/library_game.dart';
import '../../l10n/app_localizations.dart';
import 'framegen_deploy_dialog.dart';

/// Dialog helpers shared by the frame-generation page actions.
final class FrameGenActions {
  const FrameGenActions._();

  static Future<DeployOptions?> showDeployOptions(
    BuildContext context,
    AppLocalizations l10n,
    LibraryGame game,
  ) {
    return showDialog<DeployOptions>(
      context: context,
      builder: (BuildContext context) => FrameGenDeployDialog(game: game),
    );
  }
}
