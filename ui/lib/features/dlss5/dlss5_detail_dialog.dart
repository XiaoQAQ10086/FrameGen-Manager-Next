import 'package:material_ui/material_ui.dart';

import '../../bridge/dlss5_bridge.dart';
import '../../bridge/dlss5_hotkey_result.dart';
import '../../bridge/dlss5_result.dart';
import '../../bridge/dlss5_state.dart';
import '../../bridge/library_game.dart';
import '../../bridge/path_display.dart';
import '../../bridge/proxy_badge.dart';
import '../../bridge/system_bridge.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_button_styles.dart';
import '../../design/legacy_dialog.dart';
import '../../design/legacy_dialog_header.dart';
import '../../design/legacy_toast.dart';
import '../../design/operation_progress_dialog.dart';
import '../../l10n/app_localizations.dart';
import 'dlss5_detail_form.dart';
import 'dlss5_hotkey.dart';
import 'dlss5_hotkey_dialog.dart';

/// Legacy DLSS5/ReShade detail modal.
class Dlss5DetailDialog extends StatefulWidget {
  const Dlss5DetailDialog({
    super.key,
    required this.game,
    required this.initial,
    required this.proxies,
  });

  final LibraryGame game;
  final Dlss5State initial;
  final List<ProxyBadge> proxies;

  static Future<bool> show(
    BuildContext context,
    LibraryGame game,
    Dlss5State state,
    List<ProxyBadge> proxies,
  ) async {
    final bool? changed = await showLegacyDialog<bool>(
      context: context,
      builder: (BuildContext context) => Dlss5DetailDialog(
        game: game,
        initial: state,
        proxies: proxies,
      ),
    );
    return changed ?? false;
  }

  @override
  State<Dlss5DetailDialog> createState() => _Dlss5DetailDialogState();
}

class _Dlss5DetailDialogState extends State<Dlss5DetailDialog> {
  late Dlss5State _state = widget.initial;
  String _api = 'auto';
  String _proxy = 'auto';
  int? _hotkey;
  bool _busy = false;
  bool _changed = false;

  @override
  void initState() {
    super.initState();
    _hotkey = widget.initial.hotkeyVk;
  }

  bool get _legacy => _state.hasLegacyResidue;
  bool get _deployed =>
      _state.state == 'installed' ||
      _state.state == 'outdated' ||
      _state.state == 'partial' ||
      _state.state == 'conflict' ||
      _state.state == 'installedWithoutManifest';

  String get _resolvedApi =>
      _api == 'auto' ? widget.game.graphicsApi.toLowerCase() : _api;

  Future<void> _refresh() async {
    final Dlss5State state = await Dlss5Bridge.inspect(widget.game);
    if (mounted) {
      setState(() => _state = state);
    }
  }

  Future<bool> _confirm(String message) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? result = await showLegacyDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        content: Text(message),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.ok),
          ),
        ],
      ),
    );
    return result ?? false;
  }

  Future<void> _install() async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String api = _resolvedApi;
    if (api == 'unknown' || api.isEmpty) {
      LegacyToast.show(context, "${l10n.deployFailed} ('UnknownGraphicsApi')");
      return;
    }
    if (!await _confirm(
      l10n.dlss5ConfirmBody(widget.game.name),
    )) {
      return;
    }
    setState(() => _busy = true);
    final Dlss5Result? result = await OperationProgressDialog.run<Dlss5Result>(
      context: context,
      title: l10n.operationInstallingDlss5,
      action: () => Dlss5Bridge.install(
        widget.game,
        api: api,
        proxy: _proxy == 'auto' ? null : _proxy,
        hotkey: _hotkey,
      ),
    );
    if (!mounted || result == null) {
      if (mounted) {
        setState(() => _busy = false);
      }
      return;
    }
    setState(() => _busy = false);
    if (result.code == 'OperationCancelled') {
      return;
    }
    if (!result.ok) {
      LegacyToast.show(context, "${l10n.deployFailed} (result.code ?? 'InstallFailed')");
      return;
    }
    _changed = true;
    await _refresh();
    if (!mounted) {
      return;
    }
    LegacyToast.show(context, l10n.dlss5Complete(result.proxyModule ?? ''));
  }

  Future<void> _uninstall() async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool cleanOnly = _legacy && !_deployed;
    if (!await _confirm(
      cleanOnly
          ? l10n.dlss5CleanConfirm(widget.game.name)
          : l10n.dlss5UninstallConfirm(widget.game.name),
    )) {
      return;
    }
    setState(() => _busy = true);
    if (cleanOnly) {
      final int? removed = await Dlss5Bridge.cleanLegacy(widget.game);
      if (!mounted) {
        return;
      }
      if (removed == null) {
        setState(() => _busy = false);
        LegacyToast.show(context, "${l10n.dlss5CleanFailed} ('LegacyCleanupFailed')");
        return;
      }
      _changed = true;
      setState(() => _busy = false);
      await _refresh();
      return;
    }
    final Dlss5Result result = await Dlss5Bridge.uninstall(widget.game);
    if (!mounted) {
      return;
    }
    setState(() => _busy = false);
    if (!result.ok) {
      LegacyToast.show(context, "${l10n.deployFailed} (result.code ?? 'UninstallFailed')");
      return;
    }
    _changed = true;
    await _refresh();
    if (!mounted) {
      return;
    }
    LegacyToast.show(
      context,
      result.conflicts.isEmpty
          ? l10n.dlss5Uninstalled
          : l10n.dlss5UninstallConflicts(result.conflicts.length),
    );
  }

  Future<void> _recordHotkey() async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final int? virtualKey = await showLegacyDialog<int>(
      context: context,
      builder: (BuildContext context) =>
          Dlss5HotkeyDialog(initialVirtualKey: _hotkey),
    );
    if (virtualKey == null || !mounted) {
      return;
    }
    setState(() => _hotkey = virtualKey);
    if (!_deployed) {
      return;
    }
    final HotkeyOutcome outcome = await Dlss5Bridge.setHotkey(
      widget.game,
      virtualKey,
    );
    if (!mounted) {
      return;
    }
    LegacyToast.show(
      context,
      outcome == HotkeyOutcome.failed
          ? '${l10n.deployFailed} SetHotkey'
          : (outcome == HotkeyOutcome.queued
                ? l10n.dlss5HotkeyQueued
                : l10n.dlss5HotkeySaved(Dlss5Hotkey.label(virtualKey))),
    );
    _changed = true;
    await _refresh();
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Dialog(
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(20),
      ),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 580, maxHeight: 700),
        child: Padding(
          padding: const EdgeInsets.all(22),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              LegacyDialogHeader(
                title: widget.game.name,
                subtitle: displayPath(widget.game.targetDirectory),
                onClose: () => Navigator.pop(context, _changed),
              ),
              const SizedBox(height: 14),
              const Divider(height: 1, thickness: 1, color: AppTokens.border),
              const SizedBox(height: 14),
              Flexible(
                child: SingleChildScrollView(
                  child: Dlss5DetailForm(
                    game: widget.game,
                    state: _state,
                    api: _api,
                    proxy: _proxy,
                    hotkey: _hotkey,
                    busy: _busy,
                    resolvedApi: _resolvedApi,
                    onApiChanged: (String value) =>
                        setState(() => _api = value),
                    onProxyChanged: (String value) =>
                        setState(() => _proxy = value),
                    onRecordHotkey: _recordHotkey,
                  ),
                ),
              ),
              const SizedBox(height: 14),
              const Divider(height: 1, thickness: 1, color: AppTokens.border),
              const SizedBox(height: 12),
              Row(
                children: <Widget>[
                  TextButton(
                    onPressed: () => SystemBridge.openPath(
                      widget.game.targetDirectory,
                    ),
                    child: Text(l10n.openFolder),
                  ),
                  const Spacer(),
                  TextButton(
                    onPressed: _busy || (!_deployed && !_legacy)
                        ? null
                        : _uninstall,
                    child: Text(
                      _legacy && !_deployed
                          ? l10n.dlss5CleanLegacy
                          : l10n.dlss5Uninstall,
                    ),
                  ),
                  const SizedBox(width: 8),
                  FilledButton(
                    onPressed: _busy || _legacy || _state.state == 'installed'
                        ? null
                        : _install,
                    style: LegacyButtonStyles.success,
                    child: Text(_installLabel(l10n)),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _installLabel(AppLocalizations l10n) {
    return switch (_state.state) {
      'installed' => l10n.dlss5Deployed,
      'installedWithoutManifest' => l10n.dlss5DeployAgain,
      'outdated' || 'partial' || 'conflict' => l10n.update,
      _ => l10n.dlss5Install,
    };
  }


}
