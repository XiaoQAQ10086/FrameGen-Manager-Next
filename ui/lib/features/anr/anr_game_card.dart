import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/anr_package.dart';
import '../../bridge/game_modes.dart';
import '../../bridge/library_game.dart';
import '../../bridge/path_display.dart';
import '../../bridge/proxy_badge.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_button_styles.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';

/// ANR game card in the legacy layout.
class AnrGameCard extends StatelessWidget {
  const AnrGameCard({
    super.key,
    required this.game,
    required this.modes,
    required this.proxies,
    required this.package,
    required this.coverPath,
    required this.busy,
    required this.onDeploy,
    required this.onRestore,
    required this.onLaunch,
    required this.onOpenFolder,
    required this.onRemove,
  });

  final LibraryGame game;
  final GameModes? modes;
  final List<ProxyBadge> proxies;
  final AnrPackage? package;
  final String? coverPath;
  final bool busy;
  final VoidCallback onDeploy;
  final VoidCallback onRestore;
  final VoidCallback onLaunch;
  final VoidCallback onOpenFolder;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String state = modes?.anr ?? 'notDeployed';
    final bool deployed = state != 'notDeployed';
    final String? reason = _reason(l10n, deployed);
    final bool canDeploy = reason == null && package != null;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
      decoration: AppTokens.cardDecoration,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              _cover(),
              const SizedBox(width: 12),
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
                        fontSize: 14,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Wrap(
                      spacing: 6,
                      runSpacing: 6,
                      children: <Widget>[
                        LegacyPill(
                          label: _stateLabel(l10n, state),
                          foreground: _stateColor(state),
                          background: _stateColor(
                            state,
                          ).withValues(alpha: 0.1),
                        ),
                        if (deployed)
                          LegacyPill(
                            label: l10n.modeAnr,
                            foreground: AppTokens.accent,
                            background: AppTokens.accentSoft,
                          ),
                        if ((modes?.framegen ?? 'notDeployed') != 'notDeployed')
                          LegacyPill(
                            label: l10n.modeFrameGen,
                            foreground: AppTokens.accent,
                            background: AppTokens.accentSoft,
                          ),
                        if (modes?.smoothMotionOn == true)
                          LegacyPill(
                            label: l10n.modeSm,
                            foreground: AppTokens.success,
                            background: AppTokens.successSoft,
                          ),
                        for (final ProxyBadge proxy in proxies)
                          if (proxy.owner == 'anr' && deployed)
                            LegacyPill(
                              label: l10n.anrProxy(proxy.module),
                              foreground: AppTokens.warning,
                              background: AppTokens.warningSoft,
                            ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            displayPath(game.targetDirectory),
            style: const TextStyle(
              color: AppTokens.textSecondary,
              fontFamily: 'Consolas',
              fontSize: 12,
              height: 1.45,
            ),
          ),
          if (reason != null) ...<Widget>[
            const SizedBox(height: 4),
            Text(
              reason,
              style: const TextStyle(
                color: AppTokens.danger,
                fontSize: 12,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
          const SizedBox(height: 10),
          Row(
            children: <Widget>[
              if (busy)
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 12),
                  child: SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                )
              else if (deployed)
                FilledButton(
                  onPressed: onRestore,
                  style: LegacyButtonStyles.compact(LegacyButtonStyles.danger),
                  child: Text(l10n.restore),
                )
              else
                FilledButton(
                  // Shown even without an imported package, greyed out, so the
                  // action list looks the same on every card.
                  onPressed: package != null && canDeploy ? onDeploy : null,
                  style: LegacyButtonStyles.compact(
                    LegacyButtonStyles.success,
                  ),
                  child: Text(l10n.deploy),
                ),
              const SizedBox(width: 10),
              OutlinedButton.icon(
                onPressed: game.canLaunch ? onLaunch : null,
                icon: const Icon(Icons.play_arrow, size: 16),
                label: Text(l10n.launchGame),
                style: LegacyButtonStyles.compact(
                  LegacyButtonStyles.launch,
                ),
              ),
              const Spacer(),
              IconButton(
                onPressed: onOpenFolder,
                icon: const Icon(Icons.folder_outlined),
                tooltip: l10n.openFolder,
                style: LegacyButtonStyles.compactIcon(
                  LegacyButtonStyles.standard,
                ),
              ),
              const SizedBox(width: 6),
              IconButton(
                onPressed: onRemove,
                icon: const Icon(Icons.delete_outline),
                tooltip: l10n.removeGame,
                style: LegacyButtonStyles.compactIcon(
                  LegacyButtonStyles.danger,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  String? _reason(AppLocalizations l10n, bool deployed) {
    if (deployed) {
      return null;
    }
    final AnrPackage? current = package;
    if (current == null) {
      return null;
    }
    if (game.renderExe == null) {
      return l10n.anrRenderExeMissing;
    }
    final bool addon = current.kind.toLowerCase() == 'addon';
    final bool optiScaler = File(
      '${game.targetDirectory}${Platform.pathSeparator}OptiScaler.ini',
    ).existsSync();
    if (addon && !optiScaler) {
      return l10n.anrOptiScalerIniMissing;
    }
    if (!addon && optiScaler) {
      return l10n.anrOptiScalerNeeded;
    }
    const List<String> slots = <String>['dxgi.dll', 'winmm.dll', 'version.dll'];
    final Set<String> occupied = proxies
        .map((ProxyBadge proxy) => proxy.module.toLowerCase())
        .toSet();
    if (slots.every(occupied.contains)) {
      return l10n.anrProxySlotsBusy;
    }
    return null;
  }

  String _stateLabel(AppLocalizations l10n, String state) {
    return switch (state) {
      'installed' => l10n.anrStateDeployed,
      'partial' || 'conflict' => l10n.anrStateNeedsRepair,
      _ => l10n.anrStateNotDeployed,
    };
  }

  Color _stateColor(String state) {
    return switch (state) {
      'installed' => AppTokens.success,
      'partial' || 'conflict' => AppTokens.warning,
      _ => AppTokens.textSecondary,
    };
  }

  Widget _cover() {
    final String? path = coverPath;
    if (path != null && path.isNotEmpty) {
      final bool icon = path.contains('icons');
      return ClipRRect(
        borderRadius: BorderRadius.circular(10),
        child: Container(
          width: 52,
          height: 70,
          color: icon ? Colors.white : null,
          padding: icon ? const EdgeInsets.all(6) : null,
          child: Image(
            image: ResizeImage(FileImage(File(path)), width: 156),
            fit: icon ? BoxFit.contain : BoxFit.cover,
            errorBuilder:
                (BuildContext context, Object error, StackTrace? stack) =>
                    _placeholder(),
          ),
        ),
      );
    }
    return _placeholder();
  }

  Widget _placeholder() {
    return Container(
      width: 52,
      height: 70,
      decoration: BoxDecoration(
        color: AppTokens.accentSoft,
        borderRadius: BorderRadius.circular(10),
      ),
      alignment: Alignment.center,
      child: Text(
        game.name.isEmpty ? '?' : game.name.substring(0, 1).toUpperCase(),
        style: const TextStyle(
          color: AppTokens.monogram,
          fontSize: 20,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
