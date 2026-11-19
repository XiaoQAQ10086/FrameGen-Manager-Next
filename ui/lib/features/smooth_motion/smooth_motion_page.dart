import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../bridge/smooth_motion_bridge.dart';
import '../../bridge/smooth_motion_status.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// Smooth Motion page: driver toggle and component restore per game.
class SmoothMotionPage extends StatefulWidget {
  const SmoothMotionPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<SmoothMotionPage> createState() => _SmoothMotionPageState();
}

class _SmoothMotionPageState extends State<SmoothMotionPage> {
  List<LibraryGame> _games = const <LibraryGame>[];
  Map<String, SmoothMotionStatus> _statuses =
      const <String, SmoothMotionStatus>{};
  String? _busyGameId;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    if (!mounted) {
      return;
    }
    setState(() {
      _games = snapshot.games;
      _loading = snapshot.games.isNotEmpty;
    });
    if (_games.isEmpty) {
      return;
    }

    final Map<String, SmoothMotionStatus> statuses =
        await SmoothMotionBridge.inspectMany(_games);
    if (!mounted) {
      return;
    }
    setState(() {
      _statuses = statuses;
      _loading = false;
    });
  }

  Future<void> _toggle(LibraryGame game, bool enabled) async {
    setState(() => _busyGameId = game.id);
    final SmoothMotionStatus status = enabled
        ? await SmoothMotionBridge.enable(game)
        : await SmoothMotionBridge.disable(game);
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);

    final AppLocalizations l10n = AppLocalizations.of(context);
    if (status.code != null) {
      _show('${l10n.smOperationFailed} ${status.code}');
      return;
    }
    setState(() {
      _statuses = <String, SmoothMotionStatus>{
        ..._statuses,
        game.id: status,
      };
    });
  }

  Future<void> _restore(LibraryGame game) async {
    setState(() => _busyGameId = game.id);
    final SmoothMotionStatus status = await SmoothMotionBridge.restore(game);
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);

    final AppLocalizations l10n = AppLocalizations.of(context);
    if (status.code != null) {
      _show('${l10n.smOperationFailed} ${status.code}');
      return;
    }
    setState(() {
      _statuses = <String, SmoothMotionStatus>{
        ..._statuses,
        game.id: status,
      };
    });
    _show(l10n.smRestoreDone);
  }

  void _show(String message) {
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
          Row(
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      l10n.navSmoothMotion,
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      l10n.rewriteTitle,
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
              IconButton(
                onPressed: _loading ? null : _load,
                icon: const Icon(Icons.refresh),
                tooltip: l10n.scanGames,
              ),
            ],
          ),
          const SizedBox(height: 20),
          Expanded(
            child: Container(
              decoration: BoxDecoration(
                color: AppTokens.surface,
                borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
                border: Border.all(color: AppTokens.border),
                boxShadow: AppTokens.panelShadow,
              ),
              child: _loading
                  ? const Center(child: CircularProgressIndicator())
                  : _games.isEmpty
                  ? _empty(context, l10n)
                  : ListView.separated(
                      padding: const EdgeInsets.symmetric(vertical: 6),
                      itemCount: _games.length,
                      separatorBuilder: (BuildContext context, int index) =>
                          const Divider(height: 1, indent: 22, endIndent: 22),
                      itemBuilder: (BuildContext context, int index) {
                        final LibraryGame game = _games[index];
                        final SmoothMotionStatus status =
                            _statuses[game.id] ?? const SmoothMotionStatus();
                        return _SmRow(
                          game: game,
                          status: status,
                          busy: _busyGameId == game.id,
                          l10n: l10n,
                          onChanged: (bool value) => _toggle(game, value),
                          onRestore: () => _restore(game),
                        );
                      },
                    ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _empty(BuildContext context, AppLocalizations l10n) {
    return Center(
      child: Text(
        l10n.gameLibraryEmpty,
        style: Theme.of(context).textTheme.bodyMedium,
      ),
    );
  }
}

class _SmRow extends StatelessWidget {
  const _SmRow({
    required this.game,
    required this.status,
    required this.busy,
    required this.l10n,
    required this.onChanged,
    required this.onRestore,
  });

  final LibraryGame game;
  final SmoothMotionStatus status;
  final bool busy;
  final AppLocalizations l10n;
  final ValueChanged<bool> onChanged;
  final VoidCallback onRestore;

  @override
  Widget build(BuildContext context) {
    final bool hasExe = game.renderExe != null;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 10),
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
                  hasExe ? game.targetDirectory : l10n.smNoExe,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.labelMedium,
                ),
              ],
            ),
          ),
          _StateChip(status: status, l10n: l10n),
          const SizedBox(width: 6),
          if (busy)
            const SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          else
            Switch(
              value: status.enabled ?? false,
              onChanged: hasExe ? onChanged : null,
            ),
          IconButton(
            onPressed: hasExe && !busy ? onRestore : null,
            icon: const Icon(Icons.restore, size: 18),
            tooltip: l10n.restore,
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints.tightFor(width: 30, height: 30),
            color: AppTokens.textSecondary,
          ),
        ],
      ),
    );
  }
}

class _StateChip extends StatelessWidget {
  const _StateChip({required this.status, required this.l10n});

  final SmoothMotionStatus status;
  final AppLocalizations l10n;

  @override
  Widget build(BuildContext context) {
    final (String label, Color foreground, Color background) =
        switch (status.enabled) {
          true => (l10n.smOn, AppTokens.success, AppTokens.successSoft),
          false => (l10n.smOff, AppTokens.textSecondary, AppTokens.surfaceMuted),
          null => (l10n.smUnknown, AppTokens.warning, AppTokens.warningSoft),
        };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: foreground,
          fontSize: 10,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
