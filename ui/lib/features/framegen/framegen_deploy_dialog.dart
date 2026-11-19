import 'package:material_ui/material_ui.dart';

import '../../bridge/deploy_options.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// Deploy panel: proxy slot and INI overrides for one game.
class FrameGenDeployDialog extends StatefulWidget {
  const FrameGenDeployDialog({
    super.key,
    required this.game,
    this.initial = const DeployOptions(),
  });

  final LibraryGame game;
  final DeployOptions initial;

  @override
  State<FrameGenDeployDialog> createState() => _FrameGenDeployDialogState();
}

class _FrameGenDeployDialogState extends State<FrameGenDeployDialog> {
  static const List<String> _proxies = <String>[
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

  late DeployOptions _options = widget.initial;
  late String? _gameExe = widget.game.renderExe;
  bool _exeChanged = false;

  Future<void> _pickExe() async {
    final String? path = await LibraryBridge.pickExecutable();
    if (path == null || !mounted) {
      return;
    }
    setState(() {
      _gameExe = path;
      _exeChanged = true;
    });
  }

  void _update(DeployOptions options) => setState(() => _options = options);

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool xeSS = _options.engine == 'XeSS';

    return AlertDialog(
      title: Text(l10n.deployConfirmTitle),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Text(
                l10n.deployConfirmBody(widget.game.name),
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 16),
              Text(
                l10n.optionGameExe,
                style: Theme.of(context).textTheme.labelMedium,
              ),
              const SizedBox(height: 4),
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      _gameExe ?? '—',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ),
                  TextButton(
                    onPressed: _pickExe,
                    child: Text(l10n.chooseExe),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              _Dropdown<String>(
                label: l10n.optionProxySlot,
                value: _options.proxySlot,
                values: _proxies,
                onChanged: (String value) =>
                    _update(_options.copyWith(proxySlot: value)),
              ),
              const SizedBox(height: 12),
              _Dropdown<String>(
                label: l10n.optionEngine,
                value: _options.engine,
                values: const <String>['DLSS', 'XeSS'],
                onChanged: (String value) {
                  final int frames = value == 'XeSS' && _options.maxGeneratedFrames > 3
                      ? 3
                      : _options.maxGeneratedFrames;
                  _update(
                    _options.copyWith(engine: value, maxGeneratedFrames: frames),
                  );
                },
              ),
              const SizedBox(height: 12),
              _Dropdown<int>(
                label: l10n.optionFrames,
                value: _options.maxGeneratedFrames,
                values: <int>[for (var i = 1; i <= (xeSS ? 3 : 5); i++) i],
                onChanged: (int value) =>
                    _update(_options.copyWith(maxGeneratedFrames: value)),
              ),
              const SizedBox(height: 12),
              _Dropdown<int>(
                label: l10n.optionOptimized,
                value: _options.optimized,
                values: const <int>[0, 1, 2, 3],
                labelFor: (int value) => _tierLabel(l10n, value),
                onChanged: (int value) =>
                    _update(_options.copyWith(optimized: value)),
              ),
              const SizedBox(height: 12),
              _Dropdown<String>(
                label: l10n.optionPreset,
                value: _options.preset,
                values: _presets,
                onChanged: (String value) =>
                    _update(_options.copyWith(preset: value)),
              ),
              if (xeSS) ...<Widget>[
                const SizedBox(height: 12),
                _Dropdown<String>(
                  label: l10n.optionFlowResolution,
                  value: _options.xeSSFlowResolution,
                  values: const <String>[
                    'Coarse',
                    'Middle',
                    'Fine',
                    'CoarseAt4K',
                  ],
                  onChanged: (String value) =>
                      _update(_options.copyWith(xeSSFlowResolution: value)),
                ),
                const SizedBox(height: 12),
                _Dropdown<String>(
                  label: l10n.optionUseHudless,
                  value: _options.xeSSUseHudless,
                  values: const <String>['Auto', '1', '0'],
                  onChanged: (String value) =>
                      _update(_options.copyWith(xeSSUseHudless: value)),
                ),
              ],
              const SizedBox(height: 12),
              _Dropdown<String>(
                label: l10n.optionSuperResolution,
                value: _options.superResolution,
                values: _srModes,
                labelFor: (String value) =>
                    value == 'Off' ? l10n.optionOff : value,
                onChanged: (String value) =>
                    _update(_options.copyWith(superResolution: value)),
              ),
            ],
          ),
        ),
      ),
      actions: <Widget>[
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: Text(l10n.cancel),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(
            context,
            _options.copyWith(gameExe: _gameExe, persistExe: _exeChanged),
          ),
          child: Text(l10n.deploy),
        ),
      ],
    );
  }
}

String _tierLabel(AppLocalizations l10n, int value) {
  return switch (value) {
    0 => l10n.optimizedTier0,
    1 => l10n.optimizedTier1,
    2 => l10n.optimizedTier2,
    _ => l10n.optimizedTier3,
  };
}

class _Dropdown<T> extends StatelessWidget {
  const _Dropdown({
    required this.label,
    required this.value,
    required this.values,
    required this.onChanged,
    this.labelFor,
  });

  final String label;
  final T value;
  final List<T> values;
  final ValueChanged<T> onChanged;
  final String Function(T value)? labelFor;

  @override
  Widget build(BuildContext context) {
    return DropdownButtonFormField<T>(
      initialValue: value,
      decoration: InputDecoration(
        labelText: label,
        isDense: true,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
        ),
      ),
      items: values
          .map(
            (T item) => DropdownMenuItem<T>(
              value: item,
              child: Text(labelFor?.call(item) ?? '$item'),
            ),
          )
          .toList(growable: false),
      onChanged: (T? selected) {
        if (selected != null) {
          onChanged(selected);
        }
      },
    );
  }
}
