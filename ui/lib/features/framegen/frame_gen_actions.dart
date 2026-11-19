import 'package:material_ui/material_ui.dart';

import '../../bridge/library_game.dart';
import '../../l10n/app_localizations.dart';

/// Dialog helpers shared by the frame-generation page actions.
final class FrameGenActions {
  const FrameGenActions._();

  static Future<bool> confirmDeploy(
    BuildContext context,
    AppLocalizations l10n,
    LibraryGame game,
  ) async {
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: Text(l10n.deployConfirmTitle),
        content: Text(l10n.deployConfirmBody(game.name)),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.deploy),
          ),
        ],
      ),
    );
    return confirmed ?? false;
  }
}
