import 'package:material_ui/material_ui.dart';

import '../../design/app_tokens.dart';
import '../../design/legacy_panel.dart';
import '../../l10n/app_localizations.dart';

/// Sidebar card listing ANR runtime requirements.
class AnrRequirementsPanel extends StatelessWidget {
  const AnrRequirementsPanel({super.key});

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return LegacyPanel(
      title: l10n.anrRequirementsTitle,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          for (final String item in <String>[
            l10n.anrRequirement1,
            l10n.anrRequirement2,
            l10n.anrRequirement3,
          ])
            Padding(
              padding: const EdgeInsets.only(bottom: 6),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  const Text(
                    '• ',
                    style: TextStyle(color: AppTokens.accent, fontSize: 12),
                  ),
                  Expanded(
                    child: Text(
                      item,
                      style: const TextStyle(
                        color: AppTokens.textSecondary,
                        fontSize: 12,
                        height: 1.45,
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
