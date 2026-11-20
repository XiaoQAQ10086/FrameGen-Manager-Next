import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/game_modes.dart';
import '../../bridge/library_game.dart';
import '../../bridge/path_display.dart';
import '../../bridge/proxy_badge.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_button_styles.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';

/// Frame-generation game card in the legacy layout.
class FrameGenGameCard extends StatelessWidget {
  const FrameGenGameCard({
    super.key,
    required this.game,
    required this.modes,
    required this.proxies,
    required this.coverPath,
    required this.busy,
    required this.onPrimary,
    required this.onLaunch,
    required this.onOpenFolder,
    required this.onRemove,
    this.unavailable = false,
  });

  final LibraryGame game;
  final GameModes? modes;
  final List<ProxyBadge> proxies;
  final String? coverPath;
  final bool busy;
  final VoidCallback onPrimary;
  final VoidCallback onLaunch;
  final VoidCallback onOpenFolder;
  final VoidCallback onRemove;
  final bool unavailable;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String state = modes?.framegen ?? 'notDeployed';
    final bool deployed = state != 'notDeployed';
    final bool smOn = modes?.smoothMotionOn == true;
    final bool conflict = smOn && deployed;
    final String primaryLabel = switch (state) {
      'outdated' => l10n.update,
      'installedWithoutManifest' => l10n.clean,
      'installed' || 'partial' || 'conflict' => l10n.restore,
      _ => l10n.deploy,
    };
    final bool primaryEnabled = !busy && !unavailable && !smOn;
    final Color primaryColor = switch (state) {
      'outdated' => AppTokens.success,
      'installed' || 'partial' || 'conflict' => AppTokens.danger,
      _ => AppTokens.success,
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
      decoration: AppTokens.cardDecoration,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              _Cover(game: game, path: coverPath),
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
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Wrap(
                      spacing: 6,
                      runSpacing: 6,
                      children: <Widget>[
                        LegacyPill(label: _sourceLabel(l10n, game.source)),
                        LegacyPill(label: _apiLabel(game.graphicsApi)),
                        LegacyPill(
                          label: _stateLabel(l10n, state),
                          foreground: _stateColor(state),
                          background: _stateColor(
                            state,
                          ).withValues(alpha: 0.1),
                        ),
                        if (modes?.framegenVersion != null)
                          LegacyPill(
                            label: 'v${modes!.framegenVersion}',
                            foreground: AppTokens.textSecondary,
                          ),
                        if (deployed)
                          LegacyPill(
                            label: l10n.modeFrameGen,
                            foreground: AppTokens.accent,
                            background: AppTokens.accentSoft,
                          ),
                        if (smOn)
                          LegacyPill(
                            label: l10n.modeSm,
                            foreground: AppTokens.success,
                            background: AppTokens.successSoft,
                          ),
                        if (modes?.anrDeployed == true)
                          LegacyPill(
                            label: l10n.modeAnr,
                            foreground: AppTokens.accent,
                            background: AppTokens.accentSoft,
                          ),
                        for (final ProxyBadge proxy in proxies)
                          if (!proxy.ownedByFramegen)
                            LegacyPill(
                              label:
                                  '${proxy.module} · ${_ownerLabel(l10n, proxy.owner)}',
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
          if (unavailable) ...<Widget>[
            const SizedBox(height: 4),
            Text(
              l10n.gameFolderMissing,
              style: const TextStyle(
                color: AppTokens.danger,
                fontSize: 12,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
          if (conflict) ...<Widget>[
            const SizedBox(height: 4),
            Text(
              l10n.smConflict,
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
                  onPressed: primaryEnabled ? onPrimary : null,
                  style: LegacyButtonStyles.compact(
                    LegacyButtonStyles.of(primaryColor),
                  ),
                  child: Text(
                    primaryLabel,
                    style: const TextStyle(
                      fontSize: 12.5,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              const SizedBox(width: 8),
              OutlinedButton.icon(
                onPressed: game.canLaunch && !unavailable ? onLaunch : null,
                icon: const Icon(Icons.play_arrow, size: 16),
                label: Text(l10n.launchGame),
                style: LegacyButtonStyles.compact(
                  LegacyButtonStyles.launch,
                ),
              ),
              const Spacer(),
              IconButton(
                onPressed: unavailable ? null : onOpenFolder,
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

  String _stateLabel(AppLocalizations l10n, String state) {
    return switch (state) {
      'installed' => l10n.frameGenStateInstalled,
      'outdated' => l10n.frameGenStateOutdated,
      'partial' => l10n.frameGenStatePartial,
      'conflict' => l10n.frameGenStateConflict,
      'installedWithoutManifest' => l10n.frameGenStateNoManifest,
      _ => l10n.frameGenStateNotDeployed,
    };
  }

  Color _stateColor(String state) {
    return switch (state) {
      'installed' => AppTokens.success,
      'outdated' || 'partial' => AppTokens.warning,
      'conflict' => AppTokens.danger,
      _ => AppTokens.textSecondary,
    };
  }

  String _ownerLabel(AppLocalizations l10n, String owner) {
    return switch (owner) {
      'framegen' => l10n.proxyOwnerFramegen,
      'dlss5' => l10n.proxyOwnerDlss5,
      'anr' => l10n.proxyOwnerAnr,
      'reframework' => l10n.proxyOwnerReframework,
      _ => l10n.proxyOwnerOther,
    };
  }

  String _sourceLabel(AppLocalizations l10n, String source) {
    return switch (source.toLowerCase()) {
      'steam' => l10n.sourceSteam,
      'epic' => l10n.sourceEpic,
      'wegame' => l10n.sourceWeGame,
      'xbox' => l10n.sourceXbox,
      'manual' => l10n.sourceManual,
      _ => l10n.sourceLoose,
    };
  }

  String _apiLabel(String api) {
    return switch (api.toLowerCase()) {
      'dx12' => 'DX12',
      'dx11' => 'DX11',
      'dx10' => 'DX10',
      'dx9' => 'DX9',
      'vulkan' => 'Vulkan',
      'opengl' => 'OpenGL',
      _ => '未知',
    };
  }
}

class _Cover extends StatelessWidget {
  const _Cover({required this.game, required this.path});

  final LibraryGame game;
  final String? path;

  @override
  Widget build(BuildContext context) {
    final String? cover = path;
    if (cover != null && cover.isNotEmpty) {
      final bool icon = cover.contains('icons');
      return ClipRRect(
        borderRadius: BorderRadius.circular(10),
        child: Container(
          width: 52,
          height: 70,
          color: icon ? Colors.white : null,
          padding: icon ? const EdgeInsets.all(6) : null,
          child: Image.file(
            File(cover),
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
    final String initial = game.name.isEmpty
        ? '?'
        : game.name.substring(0, 1).toUpperCase();
    return Container(
      width: 52,
      height: 70,
      decoration: BoxDecoration(
        color: AppTokens.accentSoft,
        borderRadius: BorderRadius.circular(10),
      ),
      alignment: Alignment.center,
      child: Text(
        initial,
        style: const TextStyle(
          color: Color(0xFF4E6F9D),
          fontSize: 20,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}
