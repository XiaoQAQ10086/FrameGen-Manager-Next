import 'package:material_ui/material_ui.dart';

import '../../bridge/anr_bridge.dart';
import '../../bridge/anr_package.dart';
import '../../bridge/anr_result.dart';
import '../../bridge/core_library.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/library_snapshot.dart';
import '../../design/app_tokens.dart';
import '../../design/error_dialog.dart';
import '../../design/operation_progress_dialog.dart';
import '../../l10n/app_localizations.dart';

/// ANR page: package import and per-game deploy/restore.
class AnrPage extends StatefulWidget {
  const AnrPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<AnrPage> createState() => _AnrPageState();
}

class _AnrPageState extends State<AnrPage> {
  List<LibraryGame> _games = const <LibraryGame>[];
  AnrPackage? _package;
  String? _busyGameId;
  bool _importing = false;
  bool _downloading = false;

  @override
  void initState() {
    super.initState();
    final LibrarySnapshot snapshot = LibraryBridge.list(widget.core);
    _games = snapshot.games;
    _package = AnrBridge.current(widget.core);
  }

  Future<void> _import() async {
    setState(() => _importing = true);
    final AnrResult result = await AnrBridge.importPackage(widget.core);
    if (!mounted) {
      return;
    }
    setState(() {
      _importing = false;
      if (result.package != null) {
        _package = result.package;
      }
    });
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (result.cancelled) {
      return;
    }
    if (result.ok) {
      _show(l10n.anrImported);
    } else {
      await ErrorDialog.show(
        context,
        message: l10n.deployFailed,
        code: result.code ?? 'ImportFailed',
      );
    }
  }

  Future<void> _download() async {
    setState(() => _downloading = true);
    final AnrResult? result = await OperationProgressDialog.run<AnrResult>(
      context: context,
      title: AppLocalizations.of(context).operationDownloading,
      action: () => AnrBridge.downloadLatest(widget.core),
    );
    if (!mounted || result == null) {
      if (mounted) {
        setState(() => _downloading = false);
      }
      return;
    }
    setState(() {
      _downloading = false;
      if (result.package != null) {
        _package = result.package;
      }
    });
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (result.code == 'OperationCancelled') {
      return;
    }
    if (result.ok) {
      _show(l10n.anrImported);
    } else {
      await ErrorDialog.show(
        context,
        message: l10n.deployFailed,
        code: result.code ?? 'ImportFailed',
      );
    }
  }

  Future<void> _deploy(LibraryGame game) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (_package == null) {
      _show(l10n.anrNoPackage);
      return;
    }
    setState(() => _busyGameId = game.id);
    final AnrResult? result = await OperationProgressDialog.run<AnrResult>(
      context: context,
      title: l10n.operationDeployingAnr,
      action: () => AnrBridge.deploy(game),
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
    if (result.ok) {
      _show(l10n.anrDeployDone(result.proxyModule ?? '-'));
    } else {
      await ErrorDialog.show(
        context,
        message: l10n.deployFailed,
        code: result.code ?? 'DeployFailed',
      );
    }
  }

  Future<void> _restore(LibraryGame game) async {
    setState(() => _busyGameId = game.id);
    final AnrResult result = await AnrBridge.restore(game);
    if (!mounted) {
      return;
    }
    setState(() => _busyGameId = null);
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (result.ok) {
      _show(l10n.anrRestored);
    } else {
      await ErrorDialog.show(
        context,
        message: l10n.deployFailed,
        code: result.code ?? 'RestoreFailed',
      );
    }
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
                child: Text(
                  l10n.navAnr,
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              OutlinedButton.icon(
                onPressed: _downloading || _importing ? null : _download,
                icon: _downloading
                    ? const SizedBox(
                        width: 14,
                        height: 14,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.cloud_download, size: 16),
                label: Text(l10n.anrDownload),
              ),
              const SizedBox(width: 8),
              FilledButton.icon(
                onPressed: _downloading || _importing ? null : _import,
                icon: _importing
                    ? const SizedBox(
                        width: 14,
                        height: 14,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.upload_file, size: 16),
                label: Text(l10n.anrImport),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: AppTokens.surface,
              borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
              border: Border.all(color: AppTokens.border),
            ),
            child: Text(
              _package == null
                  ? l10n.anrNoPackage
                  : l10n.anrReady(_package!.version, _package!.kind),
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ),
          const SizedBox(height: 16),
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
                              else ...<Widget>[
                                IconButton(
                                  onPressed: game.renderExe == null
                                      ? null
                                      : () => _deploy(game),
                                  icon: const Icon(
                                    Icons.system_update_alt,
                                    size: 18,
                                  ),
                                  tooltip: l10n.deploy,
                                  color: AppTokens.textSecondary,
                                ),
                                IconButton(
                                  onPressed: () => _restore(game),
                                  icon: const Icon(Icons.restore, size: 18),
                                  tooltip: l10n.restore,
                                  color: AppTokens.textSecondary,
                                ),
                              ],
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
