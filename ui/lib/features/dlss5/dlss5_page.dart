import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../bridge/dlss5_bridge.dart';
import '../../bridge/dlss5_result.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// DLSS5 page: installs the pack and the ReShade proxy per game.
class Dlss5Page extends StatefulWidget {
  const Dlss5Page({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<Dlss5Page> createState() => _Dlss5PageState();
}

class _Dlss5PageState extends State<Dlss5Page> {
  List<LibraryGame> _games = const <LibraryGame>[];
  String? _busyGameId;

  @override
  void initState() {
    super.initState();
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    _games = snapshot.games;
  }

  Future<void> _install(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: Text(l10n.dlss5Install),
        content: Text(l10n.dlss5ConfirmBody(game.name)),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.dlss5Install),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
      return;
    }

    setState(() => _busyGameId = game.id);
    final Dlss5Result result = await Dlss5Bridge.install(game);
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);
    final String message = result.ok
        ? l10n.dlss5Complete(result.proxyModule ?? '')
        : '${l10n.deployFailed} ${result.code}';
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);

    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(l10n.navDlss5, style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 20),
          Expanded(
            child: Container(
              decoration: BoxDecoration(
                color: AppTokens.surface,
                borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
                border: Border.all(color: AppTokens.border),
                boxShadow: AppTokens.panelShadow,
              ),
              child: _games.isEmpty
                  ? Center(
                      child: Text(
                        l10n.gameLibraryEmpty,
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.symmetric(vertical: 6),
                      itemCount: _games.length,
                      separatorBuilder: (BuildContext context, int index) =>
                          const Divider(height: 1, indent: 22, endIndent: 22),
                      itemBuilder: (BuildContext context, int index) {
                        final LibraryGame game = _games[index];
                        final bool busy = _busyGameId == game.id;
                        return Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 22,
                            vertical: 10,
                          ),
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
                                      style: Theme.of(context)
                                          .textTheme
                                          .labelMedium,
                                    ),
                                  ],
                                ),
                              ),
                              if (busy)
                                const SizedBox(
                                  width: 22,
                                  height: 22,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              else
                                IconButton(
                                  onPressed: game.renderExe == null
                                      ? null
                                      : () => _install(game),
                                  icon: const Icon(
                                    Icons.system_update_alt,
                                    size: 18,
                                  ),
                                  tooltip: l10n.dlss5Install,
                                  color: AppTokens.textSecondary,
                                ),
                            ],
                          ),
                        );
                      },
                    ),
            ),
          ),
        ],
      ),
    );
  }
}
