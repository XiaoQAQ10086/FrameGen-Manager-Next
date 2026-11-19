import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_dialog.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';

/// Legacy modal listing games the user removed from the library.
class RemovedGamesDialog extends StatefulWidget {
  const RemovedGamesDialog({super.key, required this.games});

  final List<LibraryGame> games;

  /// Returns true when the library changed.
  static Future<bool> show(
    BuildContext context,
    List<LibraryGame> games,
  ) async {
    final bool? changed = await showLegacyDialog<bool>(
      context: context,
      builder: (BuildContext context) => RemovedGamesDialog(games: games),
    );
    return changed ?? false;
  }

  @override
  State<RemovedGamesDialog> createState() => _RemovedGamesDialogState();
}

class _RemovedGamesDialogState extends State<RemovedGamesDialog> {
  late List<LibraryGame> _games = widget.games;
  bool _changed = false;

  Future<void> _reload() async {
    final LibrarySnapshot snapshot = await LibraryBridge.removed();
    if (mounted) {
      setState(() => _games = snapshot.games);
    }
  }

  Future<void> _restore(LibraryGame game) async {
    if (await LibraryBridge.restoreRemoved(game) && mounted) {
      _changed = true;
      await _reload();
    }
  }

  Future<void> _forget(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? confirmed = await showLegacyDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        content: Text(l10n.deleteRecordConfirm(game.name)),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.deleteRecord),
          ),
        ],
      ),
    );
    if (confirmed == true && await LibraryBridge.forget(game) && mounted) {
      _changed = true;
      await _reload();
    }
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Dialog(
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(20),
      ),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 620, maxHeight: 620),
        child: Padding(
          padding: const EdgeInsets.all(22),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          l10n.removedTitle,
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        const SizedBox(height: 2),
                        Text(
                          l10n.removedHint,
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                    ),
                  ),
                  IconButton(
                    onPressed: () => Navigator.pop(context, _changed),
                    icon: const Icon(Icons.close, size: 18),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              if (_games.isEmpty)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 24),
                  child: Center(
                    child: Text(
                      l10n.removedEmpty,
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ),
                )
              else
                Flexible(
                  child: ListView.separated(
                    shrinkWrap: true,
                    itemCount: _games.length,
                    separatorBuilder: (BuildContext context, int index) =>
                        const Divider(height: 1),
                    itemBuilder: (BuildContext context, int index) {
                      final LibraryGame game = _games[index];
                      final bool exists = Directory(
                        game.installDirectory,
                      ).existsSync();
                      return Padding(
                        padding: const EdgeInsets.symmetric(vertical: 10),
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
                                      fontSize: 13.5,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                  const SizedBox(height: 2),
                                  Text(
                                    game.targetDirectory,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: const TextStyle(
                                      color: AppTokens.textSecondary,
                                      fontFamily: 'Consolas',
                                      fontSize: 11.5,
                                    ),
                                  ),
                                  const SizedBox(height: 4),
                                  Wrap(
                                    spacing: 6,
                                    children: <Widget>[
                                      LegacyPill(label: game.source),
                                      LegacyPill(
                                        label: game.graphicsApi,
                                      ),
                                      if (!exists)
                                        LegacyPill(
                                          label: l10n.gameFolderMissing,
                                          foreground: AppTokens.danger,
                                          background: AppTokens.dangerSoft,
                                        ),
                                    ],
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(width: 8),
                            OutlinedButton(
                              onPressed: exists ? () => _restore(game) : null,
                              child: Text(l10n.restoreGame),
                            ),
                            const SizedBox(width: 6),
                            TextButton(
                              onPressed: () => _forget(game),
                              child: Text(l10n.deleteRecord),
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
