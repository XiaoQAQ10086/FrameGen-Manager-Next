import 'package:material_ui/material_ui.dart';

import '../../bridge/gpu_info.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_panel.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';

/// Sidebar card: driver version and hardware-accelerated GPU scheduling.
class GpuPanel extends StatelessWidget {
  const GpuPanel({super.key, required this.gpu, required this.onOpenHags});

  final GpuInfo? gpu;
  final VoidCallback onOpenHags;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final GpuInfo? info = gpu;
    return LegacyPanel(
      title: l10n.gpuPanelTitle,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          if (info != null)
            LegacyPill(
              label: info.name,
              foreground: AppTokens.textPrimary,
              background: AppTokens.accentSoft,
              border: AppTokens.accent.withValues(alpha: 0.2),
              maxWidth: double.infinity,
            ),
          const SizedBox(height: 8),
          _Row(
            label: l10n.gpuDriverVersionLabel,
            value: info?.driverVersion ?? '-',
          ),
          _Row(
            label: l10n.gpuHagsLabel,
            value: _hagsLabel(l10n, info?.hags ?? 'unknown'),
            tone: _hagsTone(info?.hags ?? 'unknown'),
          ),
          const SizedBox(height: 10),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton(
              onPressed: onOpenHags,
              child: Text(l10n.gpuOpenHags),
            ),
          ),
        ],
      ),
    );
  }

  String _hagsLabel(AppLocalizations l10n, String hags) {
    return switch (hags) {
      'on' => l10n.gpuHagsOn,
      'off' => l10n.gpuHagsOff,
      'pending' => l10n.gpuHagsPending,
      'unsupported' => l10n.gpuHagsUnsupported,
      _ => l10n.gpuHagsUnknown,
    };
  }

  Color _hagsTone(String hags) {
    return switch (hags) {
      'on' => AppTokens.success,
      'off' || 'pending' => AppTokens.warning,
      _ => AppTokens.textSecondary,
    };
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.label, required this.value, this.tone});

  final String label;
  final String value;
  final Color? tone;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: Text(
              label,
              style: const TextStyle(
                color: AppTokens.textSecondary,
                fontSize: 12.5,
                height: 1.35,
              ),
            ),
          ),
          const SizedBox(width: 8),
          LegacyPill(
            label: value,
            foreground: tone ?? AppTokens.textPrimary,
            background: (tone ?? AppTokens.textSecondary).withValues(
              alpha: 0.08,
            ),
            maxWidth: 150,
          ),
        ],
      ),
    );
  }
}
