import 'package:material_ui/material_ui.dart';

import '../../bridge/gpu_spoof_bridge.dart';
import '../../bridge/gpu_spoof_state.dart';
import '../../bridge/system_bridge.dart';
import '../../design/app_tokens.dart';
import '../../design/error_dialog.dart';
import '../../design/legacy_panel.dart';
import '../../design/legacy_pill.dart';
import '../../design/legacy_select.dart';
import '../../l10n/app_localizations.dart';

/// Sidebar card: rename the adapter in the driver record.
class SpoofPanel extends StatefulWidget {
  const SpoofPanel({super.key});

  @override
  State<SpoofPanel> createState() => _SpoofPanelState();
}

class _SpoofPanelState extends State<SpoofPanel> {
  GpuSpoofState? _state;
  bool _busy = false;
  String? _preset;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final GpuSpoofState state = await GpuSpoofBridge.inspect();
    if (!mounted) {
      return;
    }
    setState(() {
      _state = state;
      _preset ??= state.presets.isEmpty ? null : state.presets.first;
    });
  }

  Future<bool> _confirm(String message) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? result = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: Text(l10n.gpuSpoofTitle),
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

  Future<void> _apply(String preset) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (!await _confirm(l10n.gpuSpoofConfirm(preset))) {
      return;
    }
    setState(() => _busy = true);
    final String? code = await GpuSpoofBridge.apply(0, preset);
    if (!mounted) {
      return;
    }
    setState(() => _busy = false);
    if (code != null) {
      await ErrorDialog.show(
        context,
        message: l10n.gpuSpoofFailed,
        code: code,
      );
      return;
    }
    await _load();
    if (mounted) {
      await _showRestartPrompt();
    }
  }

  Future<void> _restore() async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (!await _confirm(l10n.gpuSpoofRestoreConfirm)) {
      return;
    }
    setState(() => _busy = true);
    final String? code = await GpuSpoofBridge.restore(0, 'driver');
    if (!mounted) {
      return;
    }
    setState(() => _busy = false);
    if (code != null) {
      await ErrorDialog.show(
        context,
        message: l10n.gpuSpoofFailed,
        code: code,
      );
      return;
    }
    await _load();
  }

  Future<void> _showRestartPrompt() async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? restart = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: Text(l10n.gpuSpoofRestartTitle),
        content: Text(l10n.gpuSpoofRestartBody),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.gpuSpoofRestartLater),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.gpuSpoofRestartNow),
          ),
        ],
      ),
    );
    if (restart == true && mounted) {
      await SystemBridge.restartComputer();
    }
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final GpuSpoofState? state = _state;
    final SpoofAdapter? adapter =
        state == null || state.adapters.isEmpty ? null : state.adapters.first;
    return LegacyPanel(
      title: l10n.gpuSpoofTitle,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _ValueRow(
            label: l10n.gpuSpoofCurrent,
            value: state?.appliedName ?? adapter?.displayName ?? '-',
          ),
          _ValueRow(
            label: l10n.gpuSpoofDriverRecord,
            value: adapter?.driverName ?? '-',
          ),
          const SizedBox(height: 8),
          Text(
            l10n.gpuSpoofAdminWarning,
            style: const TextStyle(
              color: AppTokens.danger,
              fontSize: 12,
              fontWeight: FontWeight.w600,
              height: 1.5,
            ),
          ),
          const SizedBox(height: 10),
          LegacySelect<String>(
            label: l10n.gpuSpoofTarget,
            value: _preset,
            items: state?.presets ?? const <String>[],
            labelFor: (String preset) => preset,
            onChanged: (String preset) => setState(() => _preset = preset),
          ),
          const SizedBox(height: 10),
          FilledButton(
            onPressed: _busy || _preset == null ? null : () => _apply(_preset!),
            child: Text(l10n.gpuSpoofApply),
          ),
          const SizedBox(height: 6),
          OutlinedButton(
            onPressed: _busy || state?.hasBackup != true ? null : _restore,
            child: Text(l10n.gpuSpoofRestoreDriver),
          ),
        ],
      ),
    );
  }
}

class _ValueRow extends StatelessWidget {
  const _ValueRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: Text(
              label,
              style: const TextStyle(
                color: AppTokens.textSecondary,
                fontSize: 12.5,
              ),
            ),
          ),
          const SizedBox(width: 8),
          LegacyPill(
            label: value,
            foreground: AppTokens.textPrimary,
            background: AppTokens.surfaceMuted,
            maxWidth: 170,
          ),
        ],
      ),
    );
  }
}
