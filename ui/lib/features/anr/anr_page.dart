import 'dart:async';

import 'package:material_ui/material_ui.dart';

import '../../bridge/anr_bridge.dart';
import '../../bridge/anr_package.dart';
import '../../bridge/anr_result.dart';
import '../../bridge/core_library.dart';
import '../../bridge/cover_cache.dart';
import '../../bridge/game_modes.dart';
import '../../bridge/library_add_result.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../bridge/proxy_badge.dart';
import '../../bridge/system_bridge.dart';
import '../../design/app_tokens.dart';
import '../../design/error_dialog.dart';
import '../../design/legacy_pill.dart';
import '../../design/legacy_toast.dart';
import '../../design/library_list_head.dart';
import '../../design/operation_progress_dialog.dart';
import '../../l10n/app_localizations.dart';
import '../framegen/mode_source_panel.dart';
import '../framegen/remove_game_dialog.dart';
import '../framegen/removed_games_dialog.dart';
import 'anr_deploy_dialog.dart';
import 'anr_game_card.dart';
import 'anr_package_panel.dart';
import 'anr_requirements_panel.dart';

/// Legacy ANR frame-generation workspace.
class AnrPage extends StatefulWidget {
  const AnrPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<AnrPage> createState() => _AnrPageState();
}

class _AnrPageState extends State<AnrPage> {
  List<LibraryGame> _games = const <LibraryGame>[];
  List<LibraryGame> _removed = const <LibraryGame>[];
  Map<String, GameModes> _modes = const <String, GameModes>{};
  Map<String, List<ProxyBadge>> _proxies = const <String, List<ProxyBadge>>{};
  Map<String, String?> _covers = CoverCache.snapshot();
  AnrPackage? _package;
  bool _scanning = false;
  bool _adding = false;
  bool _loaded = false;
  String? _busyGameId;

  @override
  void initState() {
    super.initState();
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    _games = snapshot.games;
    _package = AnrBridge.current(widget.core);
    _loaded = true;
    _loadExtras();
  }

  Future<void> _loadExtras() async {
    final Map<String, GameModes> modes = await LibraryBridge.modes();
    final LibrarySnapshot removed = await LibraryBridge.removed();
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
      _modes = modes;
      _removed = removed.games;
      _proxies = Map<String, List<ProxyBadge>>.fromEntries(proxies);
      _package = AnrBridge.current(widget.core);
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

  Future<void> _deploy(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String? choice = await AnrDeployDialog.show(
      context,
      game,
      _proxies[game.id] ?? const <ProxyBadge>[],
      chooseProxy: (_package?.kind ?? 'full') == 'full',
    );
    if (choice == null || !mounted) {
      return;
    }
    setState(() => _busyGameId = game.id);
    final AnrResult? result = await OperationProgressDialog.run<AnrResult>(
      context: context,
      title: l10n.operationDeployingAnr,
      action: () => AnrBridge.deploy(
        game,
        proxy: choice == AnrDeployDialog.auto ? null : choice,
      ),
    );
    if (!mounted || result == null) {
      if (mounted) {
        setState(() => _busyGameId = null);
      }
      return;
    }
    setState(() => _busyGameId = null);
    if (result.code == 'OperationCancelled') {
      return;
    }
    if (!result.ok) {
      LegacyToast.show(context, "${l10n.deployFailed} (result.code ?? 'DeployFailed')");
      return;
    }
    LegacyToast.show(context, l10n.anrDeployDone(result.proxyModule ?? '-'));
    await _loadExtras();
  }

  Future<void> _restore(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    setState(() => _busyGameId = game.id);
    final AnrResult result = await AnrBridge.restore(game);
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);
    if (!result.ok) {
      LegacyToast.show(context, "${l10n.deployFailed} (result.code ?? 'RestoreFailed')");
      return;
    }
    LegacyToast.show(context, l10n.anrRestored);
    await _loadExtras();
  }

  Future<void> _launch(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (!await LibraryBridge.launch(game) && mounted) {
      LegacyToast.show(context, "${l10n.launchFailed} ('LaunchFailed')");
    }
  }

  Future<void> _remove(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (!await RemoveGameDialog.show(context, l10n, game.name) || !mounted) {
      return;
    }
    if (await LibraryBridge.remove(game) && mounted) {
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
            title: l10n.navAnr,
            badge: LegacyPill(
              label: l10n.anrExperimental,
              foreground: AppTokens.warning,
              background: AppTokens.warningSoft,
            ),
            status: _loaded
                ? l10n.anrChecked(_games.length)
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
            child: LayoutBuilder(
              builder: (BuildContext context, BoxConstraints constraints) {
                final bool wide = constraints.maxWidth >= 980;
                final Widget sidebar = SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      AnrPackagePanel(
                        core: widget.core,
                        onChanged: _loadExtras,
                      ),
                      const SizedBox(height: AppTokens.gap),
                      ModeSourcePanel(
                        gpuLabel: l10n.modeGpusFramegen,
                        sourceUrl: 'https://github.com/apex-compute/ANR',
                        extraChips: <String>[
                          l10n.modeGpusAmd,
                          l10n.modeGpusIntel,
                        ],
                      ),
                      const SizedBox(height: AppTokens.gap),
                      const AnrRequirementsPanel(),
                    ],
                  ),
                );
                final Widget content = _content(l10n);
                if (!wide) {
                  return ListView(
                    children: <Widget>[
                      sidebar,
                      const SizedBox(height: AppTokens.gap),
                      SizedBox(height: 460, child: content),
                    ],
                  );
                }
                return Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    SizedBox(width: 340, child: sidebar),
                    const SizedBox(width: 18),
                    Expanded(child: content),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _content(AppLocalizations l10n) {
    if (_games.isEmpty) {
      return Center(child: Text(l10n.gameLibraryEmpty));
    }
    return ListView.separated(
      padding: const EdgeInsets.only(right: 4, bottom: 4),
      itemCount: _games.length,
      separatorBuilder: (BuildContext context, int index) =>
          const SizedBox(height: 12),
      itemBuilder: (BuildContext context, int index) {
        final LibraryGame game = _games[index];
        return AnrGameCard(
          game: game,
          modes: _modes[game.id],
          proxies: _proxies[game.id] ?? const <ProxyBadge>[],
          package: _package,
          coverPath: _covers[game.id],
          busy: _busyGameId == game.id,
          onDeploy: () => _deploy(game),
          onRestore: () => _restore(game),
          onLaunch: () => _launch(game),
          onOpenFolder: () => SystemBridge.openPath(game.targetDirectory),
          onRemove: () => _remove(game),
        );
      },
    );
  }
}
