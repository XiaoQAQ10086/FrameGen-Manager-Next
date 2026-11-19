import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../bridge/gpu_bridge.dart';
import '../../bridge/gpu_info.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// Read-only GPU card backed by nvidia-smi.
class GpuPage extends StatefulWidget {
  const GpuPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<GpuPage> createState() => _GpuPageState();
}

class _GpuPageState extends State<GpuPage> {
  bool _loading = true;
  GpuInfo? _gpu;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final GpuInfo? gpu = await GpuBridge.load(widget.core);
    if (!mounted) {
      return;
    }
    setState(() {
      _gpu = gpu;
      _loading = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);

    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(l10n.navGpu, style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 20),
          Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
              color: AppTokens.surface,
              borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
              border: Border.all(color: AppTokens.border),
              boxShadow: AppTokens.panelShadow,
            ),
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _gpu == null
                ? Text(
                    l10n.gpuMissing,
                    style: Theme.of(context).textTheme.bodyMedium,
                  )
                : _details(context, l10n, _gpu!),
          ),
        ],
      ),
    );
  }

  Widget _details(BuildContext context, AppLocalizations l10n, GpuInfo gpu) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: AppTokens.accentSoft,
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(
                Icons.developer_board,
                color: AppTokens.accent,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(gpu.name, style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 2),
                  Text(
                    l10n.gpuDriver(gpu.driverVersion),
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                ],
              ),
            ),
            _Chip(
              label: gpu.driverSupported
                  ? l10n.gpuSupported
                  : l10n.gpuUnsupported,
              good: gpu.driverSupported,
            ),
          ],
        ),
        const SizedBox(height: 16),
        Text(
          l10n.gpuRoute(gpu.route),
          style: Theme.of(context).textTheme.labelMedium,
        ),
      ],
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
