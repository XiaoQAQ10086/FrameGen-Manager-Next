import 'package:material_ui/material_ui.dart';

import '../../bridge/dlss5_state.dart';
import '../../bridge/library_game.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// One game row on the DLSS5 page: name, target, deployment state and actions.
class Dlss5GameRow extends StatelessWidget {
  const Dlss5GameRow({
    super.key,
    required this.game,
    required this.state,
    required this.busy,
    required this.onInstall,
    required this.onHotkey,
    required this.onCleanLegacy,
    required this.onUninstall,
  });

  final LibraryGame game;
  final Dlss5State? state;
  final bool busy;
  final VoidCallback onInstall;
  final VoidCallback onHotkey;
  final VoidCallback onCleanLegacy;
  final VoidCallback onUninstall;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final Dlss5State? current = state;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 10),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  game.name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: AppTokens.textPrimary,
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  game.targetDirectory,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.labelMedium,
                ),
                if (current != null) ...<Widget>[
                  const SizedBox(height: 6),
                  Wrap(
                    spacing: 6,
                    runSpacing: 4,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: <Widget>[
                      if (current.state != 'coreUnavailable')
                        _StatusChip(label: _stateLabel(l10n, current), state: current),
                      if (current.reShadePresent) const _ReShadeChip(),
                      if (current.hasLegacyResidue)
                        _LegacyChip(files: current.legacyFiles),
                    ],
                  ),
                ],
              ],
            ),
          ),
          if (busy)
            const SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          else ...<Widget>[
            if (current?.hasLegacyResidue == true)
              IconButton(
                onPressed: onCleanLegacy,
                icon: const Icon(Icons.cleaning_services_outlined, size: 18),
                tooltip: l10n.dlss5CleanLegacy,
                color: AppTokens.warning,
              ),
            IconButton(
              onPressed: game.renderExe == null ? null : onInstall,
              icon: const Icon(Icons.system_update_alt, size: 18),
              tooltip: current?.needsUpdate == true
                  ? l10n.dlss5Update
                  : l10n.dlss5Install,
              color: AppTokens.textSecondary,
            ),
            IconButton(
              onPressed: current?.isDeployed == true ? onHotkey : null,
              icon: const Icon(Icons.keyboard, size: 18),
              tooltip: l10n.dlss5Hotkey,
              color: AppTokens.textSecondary,
            ),
            IconButton(
              onPressed: current?.isDeployed == true ? onUninstall : null,
              icon: const Icon(Icons.delete_outline, size: 18),
              tooltip: l10n.dlss5Uninstall,
              color: AppTokens.textSecondary,
            ),
          ],
        ],
      ),
    );
  }

  String _stateLabel(AppLocalizations l10n, Dlss5State current) {
    return switch (current.state) {
      'installed' => l10n.dlss5StateInstalled,
      'outdated' => l10n.dlss5StateOutdated,
      'partial' => l10n.dlss5StatePartial,
      'conflict' => l10n.dlss5StateConflict,
      'installedWithoutManifest' => l10n.dlss5StateNoManifest,
      'notDeployed' => l10n.dlss5StateNotDeployed,
      _ => l10n.dlss5StateUnknown,
    };
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.label, required this.state});

  final String label;
  final Dlss5State state;

  @override
  Widget build(BuildContext context) {
    final (Color foreground, Color background) = switch (state.state) {
      'installed' || 'installedWithoutManifest' => (
        AppTokens.success,
        AppTokens.successSoft,
      ),
      'outdated' || 'partial' => (AppTokens.warning, AppTokens.warningSoft),
      'conflict' => (AppTokens.warning, AppTokens.warningSoft),
      _ => (AppTokens.textSecondary, AppTokens.surfaceMuted),
    };
    return _Pill(
      color: foreground,
      background: background,
      child: Text(
        label,
        style: TextStyle(
          color: foreground,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

class _ReShadeChip extends StatelessWidget {
  const _ReShadeChip();

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return _Pill(
      color: AppTokens.accent,
      background: AppTokens.accentSoft,
      child: Text(
        l10n.dlss5ReShadePresent,
        style: const TextStyle(color: AppTokens.accent, fontSize: 11),
      ),
    );
  }
}

class _LegacyChip extends StatelessWidget {
  const _LegacyChip({required this.files});

  final List<String> files;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Tooltip(
      message: files.join('\n'),
      child: _Pill(
        color: AppTokens.warning,
        background: AppTokens.warningSoft,
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.warning_amber_rounded,
              size: 12,
              color: AppTokens.warning,
            ),
            const SizedBox(width: 4),
            Text(
              l10n.dlss5LegacyResidue(files.length),
              style: const TextStyle(color: AppTokens.warning, fontSize: 11),
            ),
          ],
        ),
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({
    required this.color,
    required this.background,
    required this.child,
  });

  final Color color;
  final Color background;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: color.withValues(alpha: 0.25)),
      ),
      child: child,
    );
  }
}
