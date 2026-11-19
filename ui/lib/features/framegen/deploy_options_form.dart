import 'package:material_ui/material_ui.dart';

import '../../bridge/deploy_options.dart';
import '../../bridge/proxy_badge.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_select.dart';
import '../../l10n/app_localizations.dart';

/// Option grid inside the deploy modal.
class DeployOptionsForm extends StatelessWidget {
  const DeployOptionsForm({
    super.key,
    required this.options,
    required this.proxies,
    required this.onChanged,
  });

  final DeployOptions options;
  final List<ProxyBadge> proxies;
  final ValueChanged<DeployOptions> onChanged;

  static const List<String> _proxySlots = <String>[
    'version.dll',
    'winmm.dll',
    'dbghelp.dll',
    'dinput8.dll',
    'dxgi.dll',
    'd3d12.dll',
  ];
  static const List<String> _presets = <String>['A', 'B', 'C'];
  static const List<String> _srModes = <String>[
    'Off',
    'M-INT8',
    'L-INT8',
    'M-FP16',
    'L-FP16',
  ];

  bool get _xeSS => options.engine == 'XeSS';
  Set<String> get _occupied =>
      proxies.map((ProxyBadge proxy) => proxy.module.toLowerCase()).toSet();

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: <Widget>[
            _field<String>(
              l10n,
              width: 300,
              label: l10n.optionProxySlot,
              value: options.proxySlot,
              values: _proxySlots,
              labelFor: (String slot) => _proxyLabel(slot, l10n),
              enabled: (String slot) =>
                  !_occupied.contains(slot.toLowerCase()),
              onChanged: (String value) =>
                  onChanged(options.copyWith(proxySlot: value)),
            ),
            _field<String>(
              l10n,
              width: 300,
              label: l10n.optionEngine,
              value: options.engine,
              values: const <String>['DLSS', 'XeSS'],
              onChanged: (String value) {
                final int frames = value == 'XeSS' &&
                        options.maxGeneratedFrames > 3
                    ? 3
                    : options.maxGeneratedFrames;
                onChanged(
                  options.copyWith(
                    engine: value,
                    maxGeneratedFrames: frames,
                  ),
                );
              },
            ),
            _field<int>(
              l10n,
              width: 300,
              label: l10n.optionFrames,
              value: options.maxGeneratedFrames,
              values: _xeSS ? const <int>[1, 2, 3] : const <int>[1, 2, 3, 5],
              labelFor: (int value) => '${value + 1}X',
              onChanged: (int value) =>
                  onChanged(options.copyWith(maxGeneratedFrames: value)),
            ),
            _field<int>(
              l10n,
              width: 300,
              label: l10n.optionOptimized,
              value: options.optimized,
              values: const <int>[0, 1, 2, 3],
              labelFor: (int value) => _tierLabel(l10n, value),
              enabled: (_) => !_xeSS,
              onChanged: (int value) =>
                  onChanged(options.copyWith(optimized: value)),
            ),
            _field<String>(
              l10n,
              width: 300,
              label: l10n.optionPreset,
              value: options.preset,
              values: _presets,
              enabled: (_) => !_xeSS,
              onChanged: (String value) =>
                  onChanged(options.copyWith(preset: value)),
            ),
            _field<String>(
              l10n,
              width: 300,
              label: l10n.optionSuperResolution,
              value: options.superResolution,
              values: _srModes,
              labelFor: (String value) =>
                  value == 'Off' ? l10n.optionOff : value,
              onChanged: (String value) =>
                  onChanged(options.copyWith(superResolution: value)),
            ),
            if (_xeSS) ...<Widget>[
              _field<String>(
                l10n,
                width: 300,
                label: l10n.optionFlowResolution,
                value: options.xeSSFlowResolution,
                values: const <String>[
                  'Coarse',
                  'Middle',
                  'Fine',
                  'CoarseAt4K',
                ],
                onChanged: (String value) =>
                    onChanged(options.copyWith(xeSSFlowResolution: value)),
              ),
              _field<String>(
                l10n,
                width: 300,
                label: l10n.optionUseHudless,
                value: options.xeSSUseHudless,
                values: const <String>['Auto', '1', '0'],
                onChanged: (String value) =>
                    onChanged(options.copyWith(xeSSUseHudless: value)),
              ),
            ],
          ],
        ),
        if (_xeSS) ...<Widget>[
          const SizedBox(height: 8),
          Text(
            l10n.deployXeSSNote,
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
        if (_occupied.isNotEmpty) ...<Widget>[
          const SizedBox(height: 8),
          Text(
            l10n.deployProxyNote(
              _proxySlots
                  .where((String slot) => _occupied.contains(slot.toLowerCase()))
                  .join(', '),
            ),
            style: const TextStyle(color: AppTokens.warning, fontSize: 12),
          ),
        ],
      ],
    );
  }

  Widget _field<T>(
    AppLocalizations l10n, {
    required double width,
    required String label,
    required T value,
    required List<T> values,
    required ValueChanged<T> onChanged,
    String Function(T value)? labelFor,
    bool Function(T value)? enabled,
  }) {
    final List<T> available = values
        .where((T item) => enabled?.call(item) ?? true)
        .toList(growable: false);
    final T selected = available.contains(value)
        ? value
        : (available.isEmpty ? value : available.first);
    return SizedBox(
      width: width,
      child: LegacySelect<T>(
        label: label,
        value: selected,
        items: available,
        labelFor: labelFor ?? (T item) => '$item',
        onChanged: (T next) {
          if (next != selected) {
            onChanged(next);
          }
        },
      ),
    );
  }

  String _proxyLabel(String slot, AppLocalizations l10n) {
    if (!_occupied.contains(slot.toLowerCase())) {
      return slot;
    }
    final ProxyBadge proxy = proxies.firstWhere(
      (ProxyBadge value) => value.module.toLowerCase() == slot.toLowerCase(),
    );
    return '$slot (${_ownerLabel(l10n, proxy.owner)})';
  }

  String _tierLabel(AppLocalizations l10n, int value) {
    return switch (value) {
      0 => l10n.optimizedTier0,
      1 => l10n.optimizedTier1,
      2 => l10n.optimizedTier2,
      _ => l10n.optimizedTier3,
    };
  }

  String _ownerLabel(AppLocalizations l10n, String owner) {
    return switch (owner) {
      'framegen' => l10n.proxyOwnerFramegen,
      'dlss5' => l10n.proxyOwnerDlss5,
      'anr' => l10n.proxyOwnerAnr,
      'reframework' => l10n.proxyOwnerReframework,
      _ => l10n.proxyOwnerOther,
    };
  }
}
