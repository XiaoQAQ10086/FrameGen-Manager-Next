import 'package:material_ui/material_ui.dart';

import '../../bridge/app_settings.dart';
import '../../bridge/core_library.dart';
import '../../bridge/deploy_dialog_result.dart';
import '../../bridge/deploy_options.dart';
import '../../bridge/deployment_bridge.dart';
import '../../bridge/deployment_result.dart';
import '../../bridge/game_modes.dart';
import '../../bridge/gpu_bridge.dart';
import '../../bridge/gpu_info.dart';
import '../../bridge/library_add_result.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../bridge/proxy_badge.dart';
import '../../bridge/settings_bridge.dart';
import '../../bridge/system_bridge.dart';
import '../../design/app_tokens.dart';
import '../../design/error_dialog.dart';
import '../../design/library_list_head.dart';
import '../../design/library_notice.dart';
import '../../design/operation_progress_dialog.dart';
import '../../l10n/app_localizations.dart';
import 'assets_panel.dart';
import 'frame_gen_actions.dart';
import 'framegen_game_list.dart';
import 'gpu_panel.dart';
import 'mode_source_panel.dart';
import 'remove_game_dialog.dart';
import 'removed_games_dialog.dart';
import 'spoof_panel.dart';

/// Legacy frame-generation workspace: list header, sidebar and game cards.
class FrameGenPage extends StatefulWidget {
  const FrameGenPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<FrameGenPage> createState() => _FrameGenPageState();
}

class _FrameGenPageState extends State<FrameGenPage> {
  List<LibraryGame> _games = const <LibraryGame>[];
  List<LibraryGame> _removed = const <LibraryGame>[];
  Map<String, GameModes> _modes = const <String, GameModes>{};
  Map<String, List<ProxyBadge>> _proxies = const <String, List<ProxyBadge>>{};
  Map<String, String?> _covers = const <String, String?>{};
  GpuInfo? _gpu;
  bool _showCovers = true;
  bool _scanning = false;
  bool _adding = false;
  bool _loaded = false;
  String? _error;
  String? _busyGameId;

  @override
  void initState() {
    super.initState();
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    _games = snapshot.games;
    _error = snapshot.hasError ? snapshot.errorCode : null;
    _loaded = true;
    final AppSettings settings = SettingsBridge.load(widget.core);
    _showCovers = settings.showCovers;
    _loadExtras();
    if (settings.autoScanOnStartup) {
      _scan();
    }
  }

  Future<void> _loadExtras() async {
    final Map<String, GameModes> modes = await LibraryBridge.modes();
    final LibrarySnapshot removed = await LibraryBridge.removed();
    final GpuInfo? gpu = await GpuBridge.load(widget.core);
    final List<MapEntry<String, String?>> covers = await Future.wait(
      _games
          .map(
            (LibraryGame game) async =>
                MapEntry<String, String?>(game.id, await LibraryBridge.cover(game)),
          ),
    );
    final List<MapEntry<String, List<ProxyBadge>>> proxies = await Future.wait(
      _games.map(
        (LibraryGame game) async =>
            MapEntry<String, List<ProxyBadge>>(
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
      _gpu = gpu;
      _covers = Map<String, String?>.fromEntries(covers);
      _proxies = Map<String, List<ProxyBadge>>.fromEntries(proxies);
    });
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
      _error = snapshot.hasError ? snapshot.errorCode : null;
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
      await ErrorDialog.show(
        context,
        message: l10n.addGameFailed,
        code: added.code ?? 'AddGameFailed',
      );
      return;
    }
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    setState(() {
      _games = snapshot.games;
      _error = snapshot.hasError ? snapshot.errorCode : null;
    });
    await _loadExtras();
  }


  Future<void> _openRemoved() async {
    final LibrarySnapshot snapshot = await LibraryBridge.removed();
    if (!mounted) {
      return;
    }
    setState(() => _removed = snapshot.games);
    final bool changed = await RemovedGamesDialog.show(context, _removed);
    if (!changed || !mounted) {
      return;
    }
    final LibrarySnapshot visible = LibraryBridge.list(widget.core);
    setState(() => _games = visible.games);
    await _loadExtras();
  }

  Future<void> _openDeploy(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String state = _modes[game.id]?.framegen ?? 'notDeployed';
    final DeployDialogResult? choice = await FrameGenActions.showDeployOptions(
      context,
      l10n,
      game,
      deployed: state != 'notDeployed',
      proxies: _proxies[game.id] ?? const <ProxyBadge>[],
      coverPath: _showCovers ? _covers[game.id] : null,
    );
    if (choice == null || !mounted) {
      return;
    }
    if (choice.restore) {
      await _restore(game);
      return;
    }
    final DeployOptions? options = choice.options;
    if (options == null) {
      return;
    }
    setState(() => _busyGameId = game.id);
    final DeploymentResult? result =
        await OperationProgressDialog.run<DeploymentResult>(
          context: context,
          title: l10n.operationDeploying,
          action: () => DeploymentBridge.deploy(game: game, options: options),
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
      await ErrorDialog.show(
        context,
        message: l10n.deployFailed,
        code: result.code ?? 'DeployFailed',
      );
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('${l10n.deployComplete} ${game.name}'),
        behavior: SnackBarBehavior.floating,
      ),
    );
    if (options.persistExe) {
      final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
      setState(() => _games = snapshot.games);
    }
    await _loadExtras();
  }

  Future<void> _restore(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    setState(() => _busyGameId = game.id);
    final DeploymentResult result = await DeploymentBridge.restore(game: game);
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);
    if (!result.ok) {
      await ErrorDialog.show(
        context,
        message: l10n.deployFailed,
        code: result.code ?? 'RestoreFailed',
      );
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          result.conflicts.isEmpty
              ? l10n.restoreComplete
              : l10n.restoreConflicts,
        ),
        behavior: SnackBarBehavior.floating,
      ),
    );
    await _loadExtras();
  }

  Future<void> _launch(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (!await LibraryBridge.launch(game) && mounted) {
      await ErrorDialog.show(
        context,
        message: l10n.launchFailed,
        code: 'LaunchFailed',
      );
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
    final int outdated = _modes.values
        .where((GameModes modes) => modes.framegenOutdated)
        .length;
    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          LibraryListHead(
            title: l10n.libraryTitle,
            status: _loaded
                ? l10n.libraryStatusLoaded(_games.length)
                : l10n.libraryStatusLoading,
            removedCount: _removed.length,
            scanning: _scanning,
            adding: _adding,
            onAdd: _addGame,
            onRemoved: _openRemoved,
            onScan: _scan,
          ),
          if (_error != null) LibraryNotice(
            message: '${l10n.libraryLoadFailed} $_error',
            tone: AppTokens.warning,
            background: AppTokens.warningSoft,
          ),
          if (outdated > 0)
            LibraryNotice(
              message: l10n.outdatedAssetNotice(outdated),
              tone: AppTokens.accent,
              background: AppTokens.accentSoft,
            ),
          const SizedBox(height: 14),
          Expanded(
            child: LayoutBuilder(
              builder: (BuildContext context, BoxConstraints constraints) {
                final bool wide = constraints.maxWidth >= 980;
                final Widget sidebar = SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      GpuPanel(
                        gpu: _gpu,
                        onOpenHags: () => SystemBridge.openHags(),
                      ),
                      const SizedBox(height: AppTokens.gap),
                      const SpoofPanel(),
                      const SizedBox(height: AppTokens.gap),
                      const AssetsPanel(),
                      const SizedBox(height: AppTokens.gap),
                      ModeSourcePanel(
                        gpuLabel: l10n.modeGpusFramegen,
                        sourceUrl: 'https://github.com/sdli1995/dlssg_for_sm86',
                      ),
                    ],
                  ),
                );
                final Widget content = _content(context, l10n);
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

  Widget _content(BuildContext context, AppLocalizations l10n) {
    return FrameGenGameList(
      games: _games,
      modes: _modes,
      proxies: _proxies,
      covers: _covers,
      showCovers: _showCovers,
      busyGameId: _busyGameId,
      scanning: _scanning,
      onScan: _scan,
      onPrimary: (LibraryGame game) {
        final String state = _modes[game.id]?.framegen ?? 'notDeployed';
        final bool restore =
            state == 'installed' ||
            state == 'partial' ||
            state == 'conflict';
        if (restore) {
          _restore(game);
        } else {
          _openDeploy(game);
        }
      },
      onLaunch: _launch,
      onOpenFolder: (LibraryGame game) =>
          SystemBridge.openPath(game.targetDirectory),
      onRemove: _remove,
    );
  }
}
