import 'package:material_ui/material_ui.dart';

import '../../bridge/system_bridge.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_panel.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';

/// Sidebar card: supported GPU copy plus the upstream asset source link.
class ModeSourcePanel extends StatelessWidget {
  const ModeSourcePanel({
    super.key,
    required this.gpuLabel,
    required this.sourceUrl,
    this.extraChips = const <String>[],
  });

  final String gpuLabel;
  final String sourceUrl;
  final List<String> extraChips;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return LegacyPanel(
      title: l10n.modeSourceTitle,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: <Widget>[
              LegacyPill(
                label: gpuLabel,
                fontSize: 11,
                foreground: AppTokens.textPrimary,
                background: AppTokens.accentSoft,
                border: AppTokens.accent.withValues(alpha: 0.2),
              ),
              for (final String chip in extraChips)
                LegacyPill(
                  label: chip,
                  fontSize: 11,
                  foreground: AppTokens.textPrimary,
                  background: AppTokens.accentSoft,
                  border: AppTokens.accent.withValues(alpha: 0.2),
                ),
            ],
          ),
          const SizedBox(height: 10),
          TextButton.icon(
            onPressed: () => SystemBridge.openUrl(sourceUrl),
            icon: Image.asset('assets/github.png', width: 16, height: 16),
            label: Text(l10n.modeSourceLink),
            style: TextButton.styleFrom(
              foregroundColor: AppTokens.accent,
              padding: EdgeInsets.zero,
              alignment: Alignment.centerLeft,
            ),
          ),
        ],
      ),
    );
  }
}
