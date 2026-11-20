import 'dart:async';

import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../bridge/cover_cache.dart';
import '../../bridge/game_modes.dart';
import '../../bridge/gpu_bridge.dart';
import '../../bridge/gpu_info.dart';
import '../../bridge/library_add_result.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../bridge/panel_cache.dart';
import '../../bridge/smooth_motion_bridge.dart';
import '../../bridge/smooth_motion_status.dart';
import '../../bridge/system_bridge.dart';
import '../../design/app_tokens.dart';
import '../../design/error_dialog.dart';
import '../../design/legacy_panel.dart';
import '../../design/library_list_head.dart';
import '../../l10n/app_localizations.dart';
import '../framegen/mode_source_panel.dart';
import '../framegen/remove_game_dialog.dart';
import '../framegen/removed_games_dialog.dart';
import 'sm_compatibility_panel.dart';
import 'sm_game_card.dart';

/// Legacy Smooth Motion workspace.
class SmoothMotionPage extends StatefulWidget {
  const SmoothMotionPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<SmoothMotionPage> createState() => _SmoothMotionPageState();
}

class _SmoothMotionPageState extends State<SmoothMotionPage> {
  List<LibraryGame> _games = const <LibraryGame>[];
  List<LibraryGame> _removed = const <LibraryGame>[];
  Map<String, GameModes> _modes = const <String, GameModes>{};
  Map<String, SmoothMotionStatus> _statuses =
      const <String, SmoothMotionStatus>{};
  Map<String, String?> _covers = CoverCache.snapshot();
  GpuInfo? _gpu = PanelCache.gpu;
  bool _scanning = false;
  bool _adding = false;
  bool _loaded = false;
  String? _busyGameId;

  @override
  void initState() {
    super.initState();
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    _games = snapshot.games;
    _loaded = true;
    _loadExtras();
  }

  /// The four lookups are independent, so they run together: the card badges
  /// should not queue behind each other.
  Future<void> _loadExtras() async {
    final (
      Map<String, GameModes> modes,
      LibrarySnapshot removed,
      GpuInfo? gpu,
      Map<String, SmoothMotionStatus> statuses,
    ) = await (
      LibraryBridge.modes(),
      LibraryBridge.removed(),
      GpuBridge.load(widget.core),
      SmoothMotionBridge.inspectMany(_games),
    ).wait;
    if (!mounted) {
      return;
    }
    PanelCache.gpu = gpu;
    setState(() {
      _modes = modes;
      _removed = removed.games;
      _gpu = gpu;
      _statuses = statuses;
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
      await ErrorDialog.show(
        context,
        message: l10n.addGameFailed,
        code: added.code ?? 'AddGameFailed',
      );
      return;
    }
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    setState(() => _games = snapshot.games);
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

  Future<void> _toggle(LibraryGame game) async {
    final SmoothMotionStatus current =
        _statuses[game.id] ?? const SmoothMotionStatus();
    final bool enabled = current.enabled == true;
    setState(() => _busyGameId = game.id);
    final SmoothMotionStatus status = enabled
        ? await SmoothMotionBridge.disable(game)
        : await SmoothMotionBridge.enable(game);
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (status.code != null) {
      await ErrorDialog.show(
        context,
        message: l10n.smOperationFailed,
        code: status.code!,
        detail: status.detail,
      );
      return;
    }
    setState(() {
      _statuses = <String, SmoothMotionStatus>{
        ..._statuses,
        game.id: status,
      };
    });
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

  bool _compatible(GpuInfo? gpu) {
    // Any NVIDIA adapter may attempt the deployment; the compatibility panel
    // still reports model/driver recommendations as reference only.
    return gpu != null && gpu.name.toUpperCase().contains('NVIDIA');
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool compatible = _compatible(_gpu);
    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          LibraryListHead(
            title: l10n.navSmoothMotion,
            status: _loaded
                ? l10n.smChecked(_games.length)
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
                      SmCompatibilityPanel(gpu: _gpu),
                      const SizedBox(height: AppTokens.gap),
                      ModeSourcePanel(
                        gpuLabel: l10n.modeGpusSmDlss5,
                        sourceUrl:
                            'https://github.com/pipotoufikxyz-lgtm/dlssg_for_sm86-MFG-version',
                      ),
                      const SizedBox(height: AppTokens.gap),
                      LegacyPanel(
                        title: l10n.smNotesTitle,
                        child: Text(
                          l10n.smNotesBody,
                          style: const TextStyle(
                            color: AppTokens.textSecondary,
                            fontSize: 12,
                            height: 1.5,
                          ),
                        ),
                      ),
                    ],
                  ),
                );
                final Widget content = _content(l10n, compatible);
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

  Widget _content(AppLocalizations l10n, bool compatible) {
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
        return SmGameCard(
          game: game,
          status: _statuses[game.id] ?? const SmoothMotionStatus(),
          modes: _modes[game.id],
          coverPath: _covers[game.id],
          busy: _busyGameId == game.id,
          compatible: compatible,
          onPrimary: () => _toggle(game),
          onLaunch: () => _launch(game),
          onOpenFolder: () => SystemBridge.openPath(game.targetDirectory),
          onRemove: () => _remove(game),
        );
      },
    );
  }
}
