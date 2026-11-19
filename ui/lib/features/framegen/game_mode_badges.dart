import 'package:material_ui/material_ui.dart';

import '../../bridge/game_modes.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// Small deployment badges for frame generation, DLSS5 and ANR.
class GameModeBadges extends StatelessWidget {
  const GameModeBadges({super.key, required this.modes, required this.l10n});

  final GameModes? modes;
  final AppLocalizations l10n;

  @override
  Widget build(BuildContext context) {
    final GameModes? current = modes;
    if (current == null || !current.anyDeployed) {
      return const SizedBox.shrink();
    }
    return Padding(
      padding: const EdgeInsets.only(top: 4),
      child: Wrap(
        spacing: 4,
        runSpacing: 4,
        children: <Widget>[
          if (current.framegenDeployed)
            _StateBadge(label: l10n.navFrameGeneration, state: current.framegen),
          if (current.dlss5Deployed)
            _StateBadge(label: l10n.navDlss5, state: current.dlss5),
          if (current.anrDeployed)
            _StateBadge(label: l10n.navAnr, state: current.anr),
        ],
      ),
    );
  }
}

class _StateBadge extends StatelessWidget {
  const _StateBadge({required this.label, required this.state});

  final String label;
  final String state;

  @override
  Widget build(BuildContext context) {
    final bool healthy = state == 'installed';
    final Color color = healthy ? AppTokens.success : AppTokens.warning;
    final Color background = healthy
        ? AppTokens.successSoft
        : AppTokens.warningSoft;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: color,
          fontSize: 10,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
