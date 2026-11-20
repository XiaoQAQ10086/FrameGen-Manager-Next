import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/game_modes.dart';
import '../../bridge/library_game.dart';
import '../../bridge/path_display.dart';
import '../../bridge/smooth_motion_status.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_button_styles.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';

/// Smooth Motion game card in the legacy layout.
class SmGameCard extends StatelessWidget {
  const SmGameCard({
    super.key,
    required this.game,
    required this.status,
    required this.modes,
    required this.coverPath,
    required this.busy,
    required this.compatible,
    required this.onPrimary,
    required this.onLaunch,
    required this.onOpenFolder,
    required this.onRemove,
  });

  final LibraryGame game;
  final SmoothMotionStatus status;
  final GameModes? modes;
  final String? coverPath;
  final bool busy;
  final bool compatible;
  final VoidCallback onPrimary;
  final VoidCallback onLaunch;
  final VoidCallback onOpenFolder;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String component = modes?.smoothMotionComponent ?? 'notDeployed';
    final bool enabled = status.enabled == true;
    final bool framegenOn = (modes?.framegen ?? 'notDeployed') != 'notDeployed';
    final bool blocked = !compatible || framegenOn;
    final String primaryLabel = enabled
        ? l10n.smDisable
        : (component == 'outdated' ? l10n.smUpdate : l10n.smEnable);

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
                          label: _componentLabel(l10n, component),
                          foreground: _componentColor(component),
                          background: _componentColor(
                            component,
                          ).withValues(alpha: 0.1),
                        ),
                        if (modes?.smoothComponentDeployed == true)
                          LegacyPill(
                            label: l10n.modeSm,
                            foreground: AppTokens.success,
                            background: AppTokens.successSoft,
                          ),
                        if (framegenOn)
                          LegacyPill(
                            label: l10n.modeFrameGen,
                            foreground: AppTokens.accent,
                            background: AppTokens.accentSoft,
                          ),
                        if (modes?.anrDeployed == true)
                          LegacyPill(
                            label: l10n.modeAnr,
                            foreground: AppTokens.accent,
                            background: AppTokens.accentSoft,
                          ),
                        LegacyPill(
                          label: status.enabled == null
                              ? l10n.smDriverUnknown
                              : (enabled ? l10n.smDriverOn : l10n.smDriverOff),
                          foreground: enabled
                              ? AppTokens.success
                              : AppTokens.textSecondary,
                          background: enabled
                              ? AppTokens.successSoft
                              : AppTokens.surfaceMuted,
                        ),
                        if (framegenOn)
                          LegacyPill(
                            label: l10n.smFrameGenConflict,
                            foreground: AppTokens.danger,
                            background: AppTokens.dangerSoft,
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
          if (status.detail != null && status.detail!.isNotEmpty) ...<Widget>[
            const SizedBox(height: 4),
            Text(
              status.detail!,
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
              else
                FilledButton(
                  onPressed: blocked ? null : onPrimary,
                  style: LegacyButtonStyles.compact(
                    enabled
                        ? LegacyButtonStyles.danger
                        : LegacyButtonStyles.success,
                  ),
                  child: Text(primaryLabel),
                ),
              const SizedBox(width: 8),
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

  String _componentLabel(AppLocalizations l10n, String state) {
    return switch (state) {
      'installed' => l10n.smComponentDeployed,
      'outdated' => l10n.smComponentOutdated,
      'partial' => l10n.smComponentPartial,
      'conflict' => l10n.smComponentConflict,
      'installedWithoutManifest' => l10n.smComponentPresent,
      _ => l10n.smComponentNotDeployed,
    };
  }

  Color _componentColor(String state) {
    return switch (state) {
      'installed' => AppTokens.success,
      'outdated' || 'partial' => AppTokens.warning,
      'conflict' => AppTokens.danger,
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
