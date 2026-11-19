import 'package:material_ui/material_ui.dart';

import '../../bridge/gpu_spoof_bridge.dart';
import '../../bridge/gpu_spoof_state.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// GPU name spoofing card: adapter, whitelist preset, apply and restore.
class GpuSpoofCard extends StatefulWidget {
  const GpuSpoofCard({super.key});

  @override
  State<GpuSpoofCard> createState() => _GpuSpoofCardState();
}

class _GpuSpoofCardState extends State<GpuSpoofCard> {
  GpuSpoofState? _state;
  bool _loading = true;
  bool _busy = false;
  int _adapterIndex = 0;
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
      _loading = false;
      if (_adapterIndex >= state.adapters.length) {
        _adapterIndex = 0;
      }
      if (_preset == null && state.presets.isNotEmpty) {
        _preset = state.presets.first;
      }
    });
  }

  Future<bool> _confirm(String message) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? result = await showDialog<bool>(
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

  Future<void> _apply(String preset) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (!await _confirm(l10n.gpuSpoofConfirm(preset))) {
      return;
    }
    setState(() => _busy = true);
    final String? code = await GpuSpoofBridge.apply(_adapterIndex, preset);
    if (!mounted) {
      return;
    }
    setState(() => _busy = false);
    _notify(code == null ? l10n.gpuSpoofDone : '${l10n.gpuSpoofFailed} $code');
    if (code == null) {
      await _load();
    }
  }

  Future<void> _restore(String mode) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (!await _confirm(l10n.gpuSpoofRestoreConfirm)) {
      return;
    }
    setState(() => _busy = true);
    final String? code = await GpuSpoofBridge.restore(_adapterIndex, mode);
    if (!mounted) {
      return;
    }
    setState(() => _busy = false);
    _notify(
      code == null ? l10n.gpuSpoofRestored : '${l10n.gpuSpoofFailed} $code',
    );
    if (code == null) {
      await _load();
    }
  }

  void _notify(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final GpuSpoofState? state = _state;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppTokens.surface,
        borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
        border: Border.all(color: AppTokens.border),
        boxShadow: AppTokens.panelShadow,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  l10n.gpuSpoofTitle,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
              if (state != null && !state.elevated)
                _Chip(label: l10n.gpuSpoofAdminChip, good: false),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            l10n.gpuSpoofWarning,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: AppTokens.warning,
            ),
          ),
          if (_loading)
            const Padding(
              padding: EdgeInsets.only(top: 16),
              child: LinearProgressIndicator(minHeight: 3),
            )
          else if (state == null || state.errorCode != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                '${l10n.gpuSpoofFailed} ${state?.errorCode ?? ''}',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
            )
          else if (state.adapters.isEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                l10n.gpuSpoofNoAdapter,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
            )
          else
            _controls(context, l10n, state),
        ],
      ),
    );
  }

  Widget _controls(
    BuildContext context,
    AppLocalizations l10n,
    GpuSpoofState state,
  ) {
    final bool enabled = state.elevated && !_busy;
    final SpoofAdapter adapter = state.adapters[_adapterIndex];
    return Padding(
      padding: const EdgeInsets.only(top: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          if (state.adapters.length > 1)
            DropdownButtonFormField<int>(
              initialValue: _adapterIndex,
              decoration: InputDecoration(
                labelText: l10n.gpuSpoofAdapter,
                isDense: true,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
                ),
              ),
              items: state.adapters
                  .map(
                    (SpoofAdapter item) => DropdownMenuItem<int>(
                      value: item.index,
                      child: Text(item.displayName),
                    ),
                  )
                  .toList(growable: false),
              onChanged: (int? value) {
                if (value != null) {
                  setState(() => _adapterIndex = value);
                }
              },
            )
          else
            Text(
              '${l10n.gpuSpoofAdapter}: ${adapter.displayName}',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            initialValue: _preset,
            decoration: InputDecoration(
              labelText: l10n.gpuSpoofTarget,
              isDense: true,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
              ),
            ),
            items: state.presets
                .map(
                  (String preset) => DropdownMenuItem<String>(
                    value: preset,
                    child: Text(preset),
                  ),
                )
                .toList(growable: false),
            onChanged: (String? value) {
              if (value != null) {
                setState(() => _preset = value);
              }
            },
          ),
          const SizedBox(height: 14),
          Wrap(
            spacing: 10,
            runSpacing: 8,
            children: <Widget>[
              FilledButton.icon(
                onPressed: enabled && _preset != null
                    ? () => _apply(_preset!)
                    : null,
                icon: const Icon(Icons.memory, size: 16),
                label: Text(l10n.gpuSpoofApply),
              ),
              if (state.hasBackup)
                OutlinedButton(
                  onPressed: enabled ? () => _restore('driver') : null,
                  child: Text(l10n.gpuSpoofRestoreDriver),
                ),
              if (state.hasBackup)
                TextButton(
                  onPressed: enabled ? () => _restore('backup') : null,
                  child: Text(l10n.gpuSpoofRestoreBackup),
                ),
            ],
          ),
          if (state.appliedName != null) ...<Widget>[
            const SizedBox(height: 10),
            Text(
              l10n.gpuSpoofApplied(state.appliedName!),
              style: Theme.of(context).textTheme.labelMedium,
            ),
          ],
        ],
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.label, required this.good});

  final String label;
  final bool good;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: good ? AppTokens.successSoft : AppTokens.warningSoft,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: good ? AppTokens.success : AppTokens.warning,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
