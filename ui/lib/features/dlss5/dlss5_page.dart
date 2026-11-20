import 'dart:async';

import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../bridge/cover_cache.dart';
import '../../bridge/dlss5_bridge.dart';
import '../../bridge/dlss5_state.dart';
import '../../bridge/library_add_result.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../bridge/proxy_badge.dart';
import '../../bridge/system_bridge.dart';
import '../../design/app_tokens.dart';
import '../../design/error_dialog.dart';
import '../../design/legacy_dialog.dart';
import '../../design/legacy_toast.dart';
import '../../design/library_list_head.dart';
import '../../l10n/app_localizations.dart';
import '../framegen/mode_source_panel.dart';
import '../framegen/removed_games_dialog.dart';
import 'dlss5_detail_dialog.dart';
import 'dlss5_tile.dart';

/// Legacy DLSS5/ReShade cover wall.
class Dlss5Page extends StatefulWidget {
  const Dlss5Page({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<Dlss5Page> createState() => _Dlss5PageState();
}

class _Dlss5PageState extends State<Dlss5Page> {
  List<LibraryGame> _games = const <LibraryGame>[];
  List<LibraryGame> _removed = const <LibraryGame>[];
  Map<String, Dlss5State> _states = const <String, Dlss5State>{};
  Map<String, List<ProxyBadge>> _proxies = const <String, List<ProxyBadge>>{};
  Map<String, String?> _covers = CoverCache.snapshot();
  bool _scanning = false;
  bool _adding = false;
  bool _loaded = false;

  @override
  void initState() {
    super.initState();
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    _games = snapshot.games;
    _loaded = true;
    _loadExtras();
  }

  Future<void> _loadExtras() async {
    final LibrarySnapshot removed = await LibraryBridge.removed();
    final List<MapEntry<String, Dlss5State>> states = await Future.wait(
      _games.map(
        (LibraryGame game) async =>
            MapEntry<String, Dlss5State>(game.id, await Dlss5Bridge.inspect(game)),
      ),
    );
    final List<MapEntry<String, List<ProxyBadge>>> proxies = await Future.wait(
      _games.map(
        (LibraryGame game) async => MapEntry<String, List<ProxyBadge>>(
          game.id,
          await SystemBridge.proxyStatus(game),
        ),
      ),
    );
    if (!mounted) {
      return;
    }
    setState(() {
      _removed = removed.games;
      _states = Map<String, Dlss5State>.fromEntries(states);
      _proxies = Map<String, List<ProxyBadge>>.fromEntries(proxies);
    });
    unawaited(_loadCovers());
  }

  /// Covers resolve outside the badge refresh: a slow lookup must not delay the
  /// deployment state the page is waiting on.
  Future<void> _loadCovers() async {
    final List<LibraryGame> pending = _games
        .where((LibraryGame game) => !CoverCache.knows(game.id))
        .toList(growable: false);
    if (pending.isEmpty) {
      return;
    }
    final List<String?> paths = await Future.wait(
      pending.map(LibraryBridge.cover),
    );
    if (!mounted) {
      return;
    }
    for (var index = 0; index < pending.length; index++) {
      CoverCache.put(pending[index].id, paths[index]);
    }
    CoverCache.save();
    setState(() => _covers = CoverCache.snapshot());
  }

  Future<void> _scan() async {
    setState(() => _scanning = true);
    final LibrarySnapshot snapshot = await LibraryBridge.scan(widget.core);
    if (!mounted) {
      return;
    }
    setState(() {
      _scanning = false;
      _games = snapshot.games;
    });
    if (snapshot.hasError) {
      final AppLocalizations l10n = AppLocalizations.of(context);
      await ErrorDialog.show(
        context,
        message: l10n.libraryLoadFailed,
        code: snapshot.errorCode ?? 'ScanFailed',
        detail: snapshot.errorDetail,
      );
      return;
    }
    await _loadExtras();
  }

  Future<void> _addGame() async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    setState(() => _adding = true);
    final LibraryAddResult added = await LibraryBridge.addManual();
    if (!mounted) {
      return;
    }
    setState(() => _adding = false);
    if (added.cancelled) {
      return;
    }
    if (!added.ok) {
      LegacyToast.show(context, "${l10n.addGameFailed} (added.code ?? 'AddGameFailed')");
      return;
    }
    _games = LibraryBridge.list(widget.core).games;
    setState(() {});
    await _loadExtras();
  }

  Future<void> _openRemoved() async {
    final LibrarySnapshot snapshot = await LibraryBridge.removed();
    if (!mounted) {
      return;
    }
    final bool changed = await RemovedGamesDialog.show(context, snapshot.games);
    if (!changed || !mounted) {
      return;
    }
    _games = LibraryBridge.list(widget.core).games;
    setState(() {});
    await _loadExtras();
  }

  Future<void> _openDetail(LibraryGame game) async {
    final Dlss5State state = _states[game.id] ?? const Dlss5State(state: 'unknown');
    final bool changed = await Dlss5DetailDialog.show(
      context,
      game,
      state,
      _proxies[game.id] ?? const <ProxyBadge>[],
    );
    if (changed && mounted) {
      await _loadExtras();
    }
  }

  Future<void> _remove(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? confirmed = await showLegacyDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        content: Text(l10n.removeGameConfirm(game.name)),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.removeGame),
          ),
        ],
      ),
    );
    if (confirmed == true && await LibraryBridge.remove(game) && mounted) {
      _games = LibraryBridge.list(widget.core).games;
      setState(() {});
      await _loadExtras();
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
          LibraryListHead(
            title: l10n.navDlss5,
            status: _loaded
                ? l10n.dlss5Checked(_games.length)
                : l10n.libraryStatusLoading,
            removedCount: _removed.length,
            scanning: _scanning,
            adding: _adding,
            onAdd: _addGame,
            onRemoved: _openRemoved,
            onScan: _scan,
          ),
          const SizedBox(height: AppTokens.gap),
          Expanded(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                SizedBox(
                  width: 300,
                  child: SingleChildScrollView(
                    child: ModeSourcePanel(
                      gpuLabel: l10n.modeGpusSmDlss5,
                      sourceUrl:
                          'https://github.com/ghostinthecamera/GITC-Uplift',
                    ),
                  ),
                ),
                const SizedBox(width: 18),
                Expanded(child: _wall(l10n)),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _wall(AppLocalizations l10n) {
    if (_games.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(l10n.dlss5EmptyWall, textAlign: TextAlign.center),
        ),
      );
    }
    return LayoutBuilder(
      builder: (BuildContext context, BoxConstraints constraints) {
        final int columns = (constraints.maxWidth / 182).floor().clamp(1, 8);
        return GridView.builder(
          padding: const EdgeInsets.only(right: 4, bottom: 4),
          gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: columns,
            mainAxisSpacing: 14,
            crossAxisSpacing: 14,
            childAspectRatio: 0.72,
          ),
          itemCount: _games.length,
          itemBuilder: (BuildContext context, int index) {
            final LibraryGame game = _games[index];
            return Dlss5Tile(
              game: game,
              state: _states[game.id],
              coverPath: _covers[game.id],
              onOpen: () => _openDetail(game),
              onRemove: () => _remove(game),
            );
          },
        );
      },
    );
  }
}
