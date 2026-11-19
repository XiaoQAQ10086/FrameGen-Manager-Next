import 'package:material_ui/material_ui.dart';

import '../../l10n/app_localizations.dart';

/// Shared confirmation for hiding a game from the library.
final class RemoveGameDialog {
  const RemoveGameDialog._();

  static Future<bool> show(
    BuildContext context,
    AppLocalizations l10n,
    String gameName,
  ) async {
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        content: Text(l10n.removeGameConfirm(gameName)),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.removeGame),
          ),
        ],
      ),
    );
    return confirmed ?? false;
  }
}
