import 'package:material_ui/material_ui.dart';

import '../../bridge/dlss5_state.dart';
import '../../bridge/library_game.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';
import 'dlss5_hotkey.dart';

/// Body of the DLSS5 detail modal: badges, API/proxy pickers and hotkey.
class Dlss5DetailForm extends StatelessWidget {
  const Dlss5DetailForm({
    super.key,
    required this.game,
    required this.state,
    required this.api,
    required this.proxy,
    required this.hotkey,
    required this.busy,
    required this.resolvedApi,
    required this.onApiChanged,
    required this.onProxyChanged,
    required this.onRecordHotkey,
  });

  final LibraryGame game;
  final Dlss5State state;
  final String api;
  final String proxy;
  final int? hotkey;
  final bool busy;
  final String resolvedApi;
  final ValueChanged<String> onApiChanged;
  final ValueChanged<String> onProxyChanged;
  final VoidCallback onRecordHotkey;

  static const List<String> apis = <String>[
    'auto',
    'dx12',
    'dx11',
    'dx10',
    'dx9',
    'vulkan',
    'opengl',
  ];
  static const List<String> proxies = <String>[
    'auto',
    'dxgi.dll',
    'd3d12.dll',
    'd3d11.dll',
    'd3d10.dll',
    'd3d9.dll',
    'opengl32.dll',
  ];

  bool get _legacy => state.hasLegacyResidue;
  bool get _deployed =>
      state.state == 'installed' ||
      state.state == 'outdated' ||
      state.state == 'partial' ||
      state.state == 'conflict' ||
      state.state == 'installedWithoutManifest';

  String get _autoProxy {
    return switch (resolvedApi) {
      'dx9' => 'd3d9.dll',
      'opengl' => 'opengl32.dll',
      'vulkan' => '',
      _ => 'dxgi.dll',
    };
  }

  List<String> get _proxyChoices =>
      resolvedApi == 'vulkan' ? const <String>['auto'] : proxies;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Wrap(
          spacing: 6,
          runSpacing: 6,
          children: <Widget>[
            LegacyPill(label: game.graphicsApi),
            LegacyPill(
              label: _stateLabel(l10n, state.state),
              foreground: _stateColor(state.state),
              background: _stateColor(
                state.state,
              ).withValues(alpha: 0.1),
            ),
            if (_legacy)
              LegacyPill(
                label: l10n.dlss5CleanFirst,
                foreground: AppTokens.warning,
                background: AppTokens.warningSoft,
              ),
            if (state.reShadePresent && _deployed)
              LegacyPill(
                label: l10n.dlss5ReShadePresent,
                foreground: AppTokens.accent,
                background: AppTokens.accentSoft,
              ),
          ],
        ),
        if (_legacy) ...<Widget>[
          const SizedBox(height: 8),
          Text(
            l10n.dlss5LegacyHint(state.legacyFiles.join(', ')),
            style: const TextStyle(
              color: AppTokens.danger,
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
        const SizedBox(height: 12),
        Row(
          children: <Widget>[
            Expanded(
              child: _field(
                label: l10n.deployGraphicsApi,
                value: api,
                values: apis,
                labelFor: (String value) => value == 'auto'
                    ? l10n.dlss5ApiAuto(game.graphicsApi)
                    : value.toUpperCase(),
                onChanged: onApiChanged,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _field(
                label: l10n.optionProxySlot,
                value: proxy,
                values: _proxyChoices,
                labelFor: (String value) {
                  if (value != 'auto') {
                    return value;
                  }
                  return _autoProxy.isEmpty
                      ? l10n.optionOff
                      : l10n.dlss5ProxyAuto(_autoProxy);
                },
                onChanged: onProxyChanged,
              ),
            ),
          ],
        ),
        const SizedBox(height: 10),
        Row(
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    l10n.dlss5OverlayKey,
                    style: const TextStyle(
                      color: AppTokens.textSecondary,
                      fontSize: 12,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    hotkey == null ? '-' : Dlss5Hotkey.label(hotkey!),
                    style: const TextStyle(
                      color: AppTokens.textPrimary,
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ],
              ),
            ),
            OutlinedButton(
              onPressed: busy ? null : onRecordHotkey,
              child: Text(l10n.dlss5RecordHotkey),
            ),
          ],
        ),
        const SizedBox(height: 10),
        Text(
          l10n.dlss5DetailSubtitle,
          style: Theme.of(context).textTheme.bodySmall,
        ),
        if (resolvedApi == 'vulkan') ...<Widget>[
          const SizedBox(height: 6),
          Text(
            l10n.dlss5VulkanHint,
            style: const TextStyle(color: AppTokens.warning, fontSize: 12),
          ),
        ],
      ],
    );
  }

  Widget _field({
    required String label,
    required String value,
    required List<String> values,
    required ValueChanged<String> onChanged,
    required String Function(String value) labelFor,
  }) {
    return DropdownButtonFormField<String>(
      initialValue: values.contains(value) ? value : values.first,
      isDense: true,
      decoration: InputDecoration(
        labelText: label,
        isDense: true,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppTokens.radiusSmall),
        ),
      ),
      items: values
          .map(
            (String item) => DropdownMenuItem<String>(
              value: item,
              child: Text(
                labelFor(item),
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 12.5),
              ),
            ),
          )
          .toList(growable: false),
      onChanged: busy
          ? null
          : (String? selected) {
              if (selected != null) {
                onChanged(selected);
              }
            },
    );
  }

  String _stateLabel(AppLocalizations l10n, String state) {
    return switch (state) {
      'installed' => l10n.frameGenStateInstalled,
      'outdated' => l10n.frameGenStateOutdated,
      'partial' => l10n.frameGenStatePartial,
      'conflict' => l10n.frameGenStateConflict,
      'installedWithoutManifest' => l10n.frameGenStateNoManifest,
      _ => l10n.frameGenStateNotDeployed,
    };
  }

  Color _stateColor(String state) {
    return switch (state) {
      'installed' => AppTokens.success,
      'outdated' || 'partial' => AppTokens.warning,
      'conflict' => AppTokens.danger,
      _ => AppTokens.textSecondary,
    };
  }
}
