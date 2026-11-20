import 'package:material_ui/material_ui.dart';

import '../../bridge/gpu_info.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_panel.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';

/// Sidebar card describing whether this machine can use Smooth Motion.
class SmCompatibilityPanel extends StatelessWidget {
  const SmCompatibilityPanel({super.key, required this.gpu});

  final GpuInfo? gpu;

  static const String minimumDriver = '616.64';

  bool get _ready {
    final GpuInfo? info = gpu;
    if (info == null) {
      return false;
    }
    final String route = info.route.toLowerCase();
    return info.driverSupported &&
        (route.contains('sm86') || route.contains('sm75'));
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final GpuInfo? info = gpu;
    return LegacyPanel(
      title: l10n.smCompatibilityTitle,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          LegacyPill(
            label: info == null
                ? l10n.smUnknownGate
                : (_ready ? l10n.smReady : l10n.smNotApplicable),
            foreground: _ready ? AppTokens.success : AppTokens.textSecondary,
            background: _ready
                ? AppTokens.successSoft
                : AppTokens.surfaceMuted,
          ),
          const SizedBox(height: 8),
          _row(l10n.smGpuLabel, info?.name ?? '-'),
          _row(l10n.gpuDriverVersionLabel, info?.driverVersion ?? '-'),
          _row(l10n.smMinimum, minimumDriver),
          const SizedBox(height: 8),
          Text(
            _note(l10n, info),
            style: TextStyle(
              color: _ready ? AppTokens.textSecondary : AppTokens.warning,
              fontSize: 12,
              height: 1.5,
            ),
          ),
        ],
      ),
    );
  }

  String _note(AppLocalizations l10n, GpuInfo? info) {
    if (info == null) {
      return l10n.smUnknownGate;
    }
    if (_ready) {
      return l10n.smDriverOk;
    }
    if (!info.driverSupported) {
      return l10n.smDriverTooOld(info.driverVersion, minimumDriver);
    }
    return l10n.smNotApplicable;
  }

  Widget _row(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SizedBox(
            width: 64,
            child: Text(
              label,
              style: const TextStyle(
                color: AppTokens.textSecondary,
                fontSize: 13,
              ),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Align(
              alignment: Alignment.centerLeft,
              child: LegacyPill(
                label: value,
                foreground: AppTokens.textPrimary,
                background: AppTokens.surfaceMuted,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
