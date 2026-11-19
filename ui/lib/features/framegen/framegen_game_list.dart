import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/game_modes.dart';
import '../../bridge/library_game.dart';
import '../../bridge/proxy_badge.dart';
import '../../l10n/app_localizations.dart';
import 'framegen_game_card.dart';

/// Scrollable legacy game-card list with the empty state.
class FrameGenGameList extends StatelessWidget {
  const FrameGenGameList({
    super.key,
    required this.games,
    required this.modes,
    required this.proxies,
    required this.covers,
    required this.showCovers,
    required this.busyGameId,
    required this.scanning,
    required this.onScan,
    required this.onPrimary,
    required this.onLaunch,
    required this.onOpenFolder,
    required this.onRemove,
  });

  final List<LibraryGame> games;
  final Map<String, GameModes> modes;
  final Map<String, List<ProxyBadge>> proxies;
  final Map<String, String?> covers;
  final bool showCovers;
  final String? busyGameId;
  final bool scanning;
  final VoidCallback onScan;
  final ValueChanged<LibraryGame> onPrimary;
  final ValueChanged<LibraryGame> onLaunch;
  final ValueChanged<LibraryGame> onOpenFolder;
  final ValueChanged<LibraryGame> onRemove;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (games.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(l10n.gameLibraryEmpty),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: scanning ? null : onScan,
              child: Text(l10n.scanGames),
            ),
          ],
        ),
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.only(right: 4, bottom: 4),
      itemCount: games.length,
      separatorBuilder: (BuildContext context, int index) =>
          const SizedBox(height: 12),
      itemBuilder: (BuildContext context, int index) {
        final LibraryGame game = games[index];
        return FrameGenGameCard(
          game: game,
          modes: modes[game.id],
          proxies: proxies[game.id] ?? const <ProxyBadge>[],
          coverPath: showCovers ? covers[game.id] : null,
          busy: busyGameId == game.id,
          unavailable: !Directory(game.installDirectory).existsSync(),
          onPrimary: () => onPrimary(game),
          onLaunch: () => onLaunch(game),
          onOpenFolder: () => onOpenFolder(game),
          onRemove: () => onRemove(game),
        );
      },
    );
  }
}
