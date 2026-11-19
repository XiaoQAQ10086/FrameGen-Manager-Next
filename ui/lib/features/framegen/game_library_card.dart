import 'package:material_ui/material_ui.dart';

import '../../bridge/game_modes.dart';
import '../../bridge/library_game.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';
import 'game_cover.dart';
import 'game_mode_badges.dart';

/// Game library panel with scan control and per-game rows.
class GameLibraryCard extends StatelessWidget {
  const GameLibraryCard({
    super.key,
    required this.l10n,
    required this.games,
    required this.loading,
    required this.error,
    required this.onScan,
    required this.onAddManual,
    required this.onLaunch,
    required this.onDeploy,
    required this.onRestore,
    this.busyGameId,
    this.showCovers = true,
    this.covers = const <String, String?>{},
    this.modes = const <String, GameModes>{},
  });

  final AppLocalizations l10n;
  final List<LibraryGame> games;
  final bool loading;
  final String? error;
  final Future<void> Function() onScan;
  final Future<void> Function() onAddManual;
  final Future<void> Function(LibraryGame game) onLaunch;
  final Future<void> Function(LibraryGame game) onDeploy;
  final Future<void> Function(LibraryGame game) onRestore;
  final String? busyGameId;
  final bool showCovers;
  final Map<String, String?> covers;
  final Map<String, GameModes> modes;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppTokens.surface,
        borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
        border: Border.all(color: AppTokens.border),
        boxShadow: AppTokens.panelShadow,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(22, 18, 22, 12),
            child: Column(
              children: <Widget>[
                Row(
                  children: <Widget>[
                    Expanded(
                      child: Text(
                        l10n.gameLibraryTitle,
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Row(
                  children: <Widget>[
                    Text(
                      l10n.gamesCount(games.length),
                      style: Theme.of(context).textTheme.labelMedium,
                    ),
                    const Spacer(),
                    if (loading)
                      Text(
                        l10n.scanningGames,
                        style: Theme.of(context).textTheme.labelMedium,
                      )
                    else ...<Widget>[
                      TextButton.icon(
                        onPressed: onAddManual,
                        icon: const Icon(Icons.add, size: 16),
                        label: Text(l10n.addGame),
                      ),
                      TextButton.icon(
                        onPressed: onScan,
                        icon: const Icon(Icons.refresh, size: 16),
                        label: Text(l10n.scanGames),
                      ),
                    ],
                  ],
                ),
              ],
            ),
          ),
          const Divider(height: 1),
          Expanded(child: _body(context)),
        ],
      ),
    );
  }

  Widget _body(BuildContext context) {
    if (error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              const Icon(
                Icons.error_outline,
                color: AppTokens.warning,
                size: 28,
              ),
              const SizedBox(height: 12),
              Text(
                l10n.libraryLoadFailed,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 4),
              Text(error!, style: Theme.of(context).textTheme.labelMedium),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: onScan,
                icon: const Icon(Icons.refresh, size: 16),
                label: Text(l10n.scanGames),
              ),
            ],
          ),
        ),
      );
    }

    if (games.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Container(
                width: 64,
                height: 64,
                decoration: const BoxDecoration(
                  color: AppTokens.surfaceMuted,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.sports_esports_outlined,
                  color: AppTokens.textSecondary,
                  size: 28,
                ),
              ),
              const SizedBox(height: 16),
              Text(
                l10n.gameLibraryEmpty,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 12),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  FilledButton.icon(
                    onPressed: onScan,
                    icon: const Icon(Icons.search, size: 16),
                    label: Text(l10n.scanGames),
                  ),
                  const SizedBox(width: 8),
                  OutlinedButton.icon(
                    onPressed: onAddManual,
                    icon: const Icon(Icons.add, size: 16),
                    label: Text(l10n.addGame),
                  ),
                ],
              ),
            ],
          ),
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.symmetric(vertical: 6),
      itemCount: games.length,
      separatorBuilder: (BuildContext context, int index) =>
          const Divider(height: 1, indent: 22, endIndent: 22),
      itemBuilder: (BuildContext context, int index) => _GameRow(
        game: games[index],
        l10n: l10n,
        busy: busyGameId == games[index].id,
        coverPath: showCovers ? covers[games[index].id] : null,
        modes: modes[games[index].id],
        onLaunch: onLaunch,
        onDeploy: onDeploy,
        onRestore: onRestore,
      ),
    );
  }
}

class _GameRow extends StatelessWidget {
  const _GameRow({
    required this.game,
    required this.l10n,
    required this.busy,
    required this.coverPath,
    this.modes,
    required this.onLaunch,
    required this.onDeploy,
    required this.onRestore,
  });

  final LibraryGame game;
  final AppLocalizations l10n;
  final bool busy;
  final String? coverPath;
  final GameModes? modes;
  final Future<void> Function(LibraryGame game) onLaunch;
  final Future<void> Function(LibraryGame game) onDeploy;
  final Future<void> Function(LibraryGame game) onRestore;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 12),
      child: Row(
        children: <Widget>[
          GameCover(game: game, path: coverPath),
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
                GameModeBadges(modes: modes, l10n: l10n),
              ],
            ),
          ),
          const SizedBox(width: 8),
          _MiniChip(label: _sourceLabel(l10n, game.source)),
          const SizedBox(width: 6),
          _MiniChip(label: _apiLabel(game.graphicsApi)),
          const SizedBox(width: 4),
          if (busy)
            const SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          else ...<Widget>[
            _RowAction(
              icon: Icons.play_arrow,
              tooltip: l10n.launchGame,
              onPressed: game.canLaunch ? () => onLaunch(game) : null,
            ),
            _RowAction(
              icon: Icons.system_update_alt,
              tooltip: l10n.deploy,
              onPressed: () => onDeploy(game),
            ),
            _RowAction(
              icon: Icons.restore,
              tooltip: l10n.restore,
              onPressed: modes?.framegenDeployed == true
                  ? () => onRestore(game)
                  : null,
            ),
          ],
        ],
      ),
    );
  }
}

class _RowAction extends StatelessWidget {
  const _RowAction({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return IconButton(
      icon: Icon(icon, size: 18),
      tooltip: tooltip,
      onPressed: onPressed,
      padding: EdgeInsets.zero,
      constraints: const BoxConstraints.tightFor(width: 30, height: 30),
      color: AppTokens.textSecondary,
    );
  }
}

class _MiniChip extends StatelessWidget {
  const _MiniChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: AppTokens.surfaceMuted,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: AppTokens.textSecondary,
          fontSize: 10,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
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
    _ => 'Unknown',
  };
}
