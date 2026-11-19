import 'package:material_ui/material_ui.dart';

import '../../bridge/app_settings.dart';
import '../../bridge/core_library.dart';
import '../../bridge/deploy_options.dart';
import '../../bridge/deployment_bridge.dart';
import '../../bridge/deployment_result.dart';
import '../../bridge/game_modes.dart';
import '../../bridge/library_add_result.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../bridge/settings_bridge.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';
import 'frame_gen_actions.dart';
import 'game_library_card.dart';

/// First migrated page: mirror of the existing frame-generation workspace.
class FrameGenPage extends StatefulWidget {
  const FrameGenPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<FrameGenPage> createState() => _FrameGenPageState();
}

class _FrameGenPageState extends State<FrameGenPage> {
  List<LibraryGame> _games = const <LibraryGame>[];
  Map<String, String?> _covers = const <String, String?>{};
  Map<String, GameModes> _modes = const <String, GameModes>{};
  bool _showCovers = true;
  bool _loading = false;
  String? _error;
  String? _busyGameId;

  @override
  void initState() {
    super.initState();
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    _games = snapshot.games;
    _error = snapshot.hasError ? snapshot.errorCode : null;
    final AppSettings settings = SettingsBridge.load(widget.core);
    _showCovers = settings.showCovers;
    _loadModes();
    if (settings.autoScanOnStartup) {
      _scan();
    } else {
      _loadCovers();
    }
  }

  Future<void> _loadModes() async {
    final Map<String, GameModes> modes = await LibraryBridge.modes();
    if (!mounted) {
      return;
    }
    setState(() => _modes = modes);
  }

  Future<void> _loadCovers() async {
    if (!_showCovers) {
      return;
    }
    final List<MapEntry<String, String?>> entries = await Future.wait(
      _games
          .where((LibraryGame game) => game.source == 'steam')
          .map(
            (LibraryGame game) async =>
                MapEntry<String, String?>(game.id, await LibraryBridge.cover(game)),
          ),
    );
    if (!mounted) {
      return;
    }
    setState(() => _covers = Map<String, String?>.fromEntries(entries));
  }

  Future<void> _scan() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final LibrarySnapshot snapshot = await LibraryBridge.scan(widget.core);
    if (!mounted) {
      return;
    }
    setState(() {
      _loading = false;
      _games = snapshot.games;
      _error = snapshot.hasError ? snapshot.errorCode : null;
    });
    await _loadCovers();
    await _loadModes();
  }

  Future<void> _addManual() async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final LibraryAddResult result = await LibraryBridge.addManual();
    if (!mounted || result.cancelled) {
      return;
    }
    if (!result.ok) {
      _showResult('${l10n.addGameFailed} ${result.code}');
      return;
    }
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    setState(() {
      _games = snapshot.games;
      _error = snapshot.hasError ? snapshot.errorCode : null;
    });
    await _loadCovers();
    await _loadModes();
  }

  Future<void> _launch(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool started = await LibraryBridge.launch(game);
    if (mounted && !started) {
      _showResult(l10n.launchFailed);
    }
  }

  Future<void> _deploy(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final DeployOptions? options = await FrameGenActions.showDeployOptions(
      context,
      l10n,
      game,
    );
    if (options == null || !mounted) {
      return;
    }

    setState(() => _busyGameId = game.id);
    final DeploymentResult result = await DeploymentBridge.deploy(
      game: game,
      options: options,
    );
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);
    _showResult(
      result.ok ? l10n.deployComplete : '${l10n.deployFailed} ${result.code}',
    );
    if (result.ok) {
      if (options.persistExe) {
        final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
        setState(() {
          _games = snapshot.games;
          _error = snapshot.hasError ? snapshot.errorCode : null;
        });
        await _loadCovers();
      }
      await _loadModes();
    }
  }

  Future<void> _restore(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    setState(() => _busyGameId = game.id);
    final DeploymentResult result = await DeploymentBridge.restore(game: game);
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);
    final String message = result.ok
        ? (result.conflicts.isEmpty
              ? l10n.restoreComplete
              : l10n.restoreConflicts)
        : '${l10n.deployFailed} ${result.code}';
    _showResult(message);
    if (result.ok) {
      await _loadModes();
    }
  }

  void _showResult(String message) {
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
          Text(
            l10n.navFrameGeneration,
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 4),
          Text(
            l10n.framegenSubtitle,
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 20),
          Expanded(
            child: LayoutBuilder(
              builder: (BuildContext context, BoxConstraints constraints) {
                final bool wide = constraints.maxWidth >= 900;
                final Widget sidebar = Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[_CoreCard(core: widget.core)],
                );
                final Widget library = GameLibraryCard(
                  l10n: l10n,
                  games: _games,
                  loading: _loading,
                  error: _error,
                  onScan: _scan,
                  onAddManual: _addManual,
                  onLaunch: _launch,
                  onDeploy: _deploy,
                  onRestore: _restore,
                  busyGameId: _busyGameId,
                  showCovers: _showCovers,
                  covers: _covers,
                  modes: _modes,
                );

                if (!wide) {
                  return SingleChildScrollView(
                    child: Column(
                      children: <Widget>[
                        sidebar,
                        const SizedBox(height: AppTokens.gap),
                        SizedBox(height: 420, child: library),
                      ],
                    ),
                  );
                }

                return Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    SizedBox(width: 320, child: sidebar),
                    const SizedBox(width: AppTokens.gap),
                    Expanded(child: library),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _CoreCard extends StatelessWidget {
  const _CoreCard({this.core});

  final CoreLibrary? core;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool online = core != null;
    final Color tone = online ? AppTokens.success : AppTokens.warning;
    final Color soft = online ? AppTokens.successSoft : AppTokens.warningSoft;

    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: soft,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(
                  online ? Icons.check_circle_outline : Icons.cloud_off,
                  color: tone,
                  size: 21,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      online ? l10n.coreConnected : l10n.coreOffline,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 2),
                    Text(
                      online
                          ? l10n.coreVersion(core!.version)
                          : l10n.coreOfflineHint,
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (online) ...<Widget>[
            const SizedBox(height: 16),
            Text(
              l10n.framegenCoreHint,
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
        ],
      ),
    );
  }
}

class _Panel extends StatelessWidget {
  const _Panel({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppTokens.surface,
        borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
        border: Border.all(color: AppTokens.border),
        boxShadow: AppTokens.panelShadow,
      ),
      child: child,
    );
  }
}
