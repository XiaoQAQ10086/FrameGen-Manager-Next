import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// About page with version and platform information.
class AboutPage extends StatelessWidget {
  const AboutPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String coreVersion = core?.version ?? '—';

    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(l10n.navAbout, style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 20),
          Container(
            padding: const EdgeInsets.all(24),
            decoration: BoxDecoration(
              color: AppTokens.surface,
              borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
              border: Border.all(color: AppTokens.border),
              boxShadow: AppTokens.panelShadow,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  l10n.appTitle,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 6),
                Text(
                  l10n.aboutDescription,
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
                const SizedBox(height: 18),
                _InfoRow(label: l10n.coreVersion(coreVersion)),
                const SizedBox(height: 8),
                _InfoRow(label: l10n.footerPlatform),
                const SizedBox(height: 8),
                _InfoRow(label: l10n.phaseBadge),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Text(label, style: Theme.of(context).textTheme.labelMedium);
  }
}
