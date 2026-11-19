import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../bridge/dlss5_bridge.dart';
import '../../bridge/dlss5_result.dart';
import '../../bridge/dlss5_state.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';
import 'dlss5_game_row.dart';
import 'dlss5_hotkey.dart';
import 'dlss5_hotkey_dialog.dart';

/// DLSS5 page: installs the pack and the ReShade proxy per game.
class Dlss5Page extends StatefulWidget {
  const Dlss5Page({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<Dlss5Page> createState() => _Dlss5PageState();
}

class _Dlss5PageState extends State<Dlss5Page> {
  List<LibraryGame> _games = const <LibraryGame>[];
  Map<String, Dlss5State> _states = const <String, Dlss5State>{};
  String? _busyGameId;

  @override
  void initState() {
    super.initState();
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    _games = snapshot.games;
    _loadStates();
  }

  Future<void> _loadStates() async {
    if (_games.isEmpty) {
      return;
    }
    final List<MapEntry<String, Dlss5State>> entries = await Future.wait(
      _games.map(
        (LibraryGame game) async =>
            MapEntry<String, Dlss5State>(game.id, await Dlss5Bridge.inspect(game)),
      ),
    );
    if (!mounted) {
      return;
    }
    setState(() => _states = Map<String, Dlss5State>.fromEntries(entries));
  }

  Future<void> _refreshState(LibraryGame game) async {
    final Dlss5State state = await Dlss5Bridge.inspect(game);
    if (!mounted) {
      return;
    }
    setState(() {
      _states = <String, Dlss5State>{..._states, game.id: state};
    });
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
    if (result.ok) {
      await _refreshState(game);
    }
  }

  Future<void> _uninstall(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: Text(l10n.dlss5Uninstall),
        content: Text(l10n.dlss5UninstallConfirm(game.name)),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.dlss5Uninstall),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
      return;
    }

    setState(() => _busyGameId = game.id);
    final Dlss5Result result = await Dlss5Bridge.uninstall(game);
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);
    final String message;
    if (!result.ok) {
      message = '${l10n.deployFailed} ${result.code}';
    } else if (result.conflicts.isEmpty) {
      message = l10n.dlss5Uninstalled;
    } else {
      message = l10n.dlss5UninstallConflicts(result.conflicts.length);
    }
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
    if (result.ok) {
      await _refreshState(game);
    }
  }

  void _notify(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
  }

  Future<void> _cleanLegacy(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: Text(l10n.dlss5CleanLegacy),
        content: Text(l10n.dlss5CleanConfirm(game.name)),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.dlss5CleanLegacy),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
      return;
    }
    final int? removed = await Dlss5Bridge.cleanLegacy(game);
    if (!mounted) {
      return;
    }
    _notify(
      removed == null
          ? l10n.dlss5CleanFailed
          : l10n.dlss5Cleaned(removed),
    );
    if (removed != null) {
      await _refreshState(game);
    }
  }

  Future<void> _recordHotkey(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final int? virtualKey = await showDialog<int>(
      context: context,
      builder: (BuildContext context) => Dlss5HotkeyDialog(
        initialVirtualKey: _states[game.id]?.hotkeyVk,
      ),
    );
    if (virtualKey == null || !mounted) {
      return;
    }
    final bool saved = await Dlss5Bridge.setHotkey(game, virtualKey);
    if (!mounted) {
      return;
    }
    _notify(
      saved
          ? l10n.dlss5HotkeySaved(Dlss5Hotkey.label(virtualKey))
          : '${l10n.deployFailed} SetHotkey',
    );
    if (saved) {
      await _refreshState(game);
    }
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
                        return Dlss5GameRow(
                          game: game,
                          state: _states[game.id],
                          busy: _busyGameId == game.id,
                          onInstall: () => _install(game),
                          onHotkey: () => _recordHotkey(game),
                          onCleanLegacy: () => _cleanLegacy(game),
                          onUninstall: () => _uninstall(game),
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
