import 'package:material_ui/material_ui.dart';

import '../../bridge/anr_bridge.dart';
import '../../bridge/anr_package.dart';
import '../../bridge/anr_result.dart';
import '../../bridge/core_library.dart';
import '../../bridge/gpu_bridge.dart';
import '../../bridge/gpu_info.dart';
import '../../bridge/system_bridge.dart';
import '../../bridge/system_paths.dart';
import '../../design/app_tokens.dart';
import '../../design/error_dialog.dart';
import '../../design/legacy_button_styles.dart';
import '../../design/legacy_panel.dart';
import '../../design/legacy_pill.dart';
import '../../design/operation_progress_dialog.dart';
import '../../l10n/app_localizations.dart';

/// Sidebar card: imported ANR package, download/import and netdisk links.
class AnrPackagePanel extends StatefulWidget {
  const AnrPackagePanel({super.key, this.core, this.onChanged});

  final CoreLibrary? core;
  final VoidCallback? onChanged;

  @override
  State<AnrPackagePanel> createState() => _AnrPackagePanelState();
}

class _AnrPackagePanelState extends State<AnrPackagePanel> {
  AnrPackage? _package;
  GpuInfo? _gpu;
  SystemPaths _paths = const SystemPaths();
  bool _downloading = false;
  bool _importing = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final AnrPackage? package = AnrBridge.current(widget.core);
    final GpuInfo? gpu = await GpuBridge.load(widget.core);
    final SystemPaths paths = await SystemBridge.paths();
    if (mounted) {
      setState(() {
        _package = package;
        _gpu = gpu;
        _paths = paths;
      });
    }
  }

  ///
  /// The busy flag is cleared in `finally` so a thrown action can never leave
  /// the button disabled for the rest of the session.
  Future<void> _download() async {
    setState(() => _downloading = true);
    try {
      final AnrResult? result = await OperationProgressDialog.run<AnrResult>(
        context: context,
        title: AppLocalizations.of(context).operationDownloading,
        action: () => AnrBridge.downloadLatest(widget.core),
      );
      if (!mounted || result == null) {
        return;
      }
      if (result.code == 'OperationCancelled') {
        return;
      }
      if (!result.ok) {
        await ErrorDialog.show(
          context,
          message: AppLocalizations.of(context).deployFailed,
          code: result.code ?? 'AnrDownloadFailed',
        );
        return;
      }
      await _load();
      widget.onChanged?.call();
    } finally {
      if (mounted) {
        setState(() => _downloading = false);
      }
    }
  }

  Future<void> _import() async {
    setState(() => _importing = true);
    try {
      final AnrResult? result = await OperationProgressDialog.run<AnrResult>(
        context: context,
        title: AppLocalizations.of(context).operationDownloading,
        action: () => AnrBridge.importPackage(widget.core),
      );
      if (!mounted || result == null) {
        return;
      }
      if (result.cancelled) {
        return;
      }
      if (!result.ok) {
        await ErrorDialog.show(
          context,
          message: AppLocalizations.of(context).deployFailed,
          code: result.code ?? 'AnrImportFailed',
        );
        return;
      }
      await _load();
      widget.onChanged?.call();
    } finally {
      if (mounted) {
        setState(() => _importing = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final AnrPackage? package = _package;
    final bool ready = package != null;
    final bool addon = package?.kind.toLowerCase() == 'addon';
    return LegacyPanel(
      title: l10n.anrPackageTitle,
      trailing: LegacyPill(
        label: ready ? l10n.anrPackageReady : l10n.anrNotDownloaded,
        foreground: ready ? AppTokens.success : AppTokens.danger,
        background: ready ? AppTokens.successSoft : AppTokens.dangerSoft,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          if (package == null)
            Text(
              l10n.anrPackageMissing,
              style: const TextStyle(
                color: AppTokens.danger,
                fontSize: 12,
                height: 1.5,
              ),
            )
          else
            Text(
              l10n.anrPackageVersion(package.version) +
                  (addon ? l10n.anrPackageAddon : ''),
              style: const TextStyle(
                color: AppTokens.textPrimary,
                fontSize: 13,
                fontWeight: FontWeight.w700,
              ),
            ),
          const SizedBox(height: 6),
          Text(
            _gpu == null
                ? l10n.gpuMissing
                : l10n.anrCurrentGpu(_gpu!.name, _gpu!.driverVersion),
            style: const TextStyle(
              color: AppTokens.textSecondary,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 10),
          FilledButton.icon(
            onPressed: _downloading || _importing ? null : _download,
            style: LegacyButtonStyles.tall(LegacyButtonStyles.primary),
            icon: const Icon(Icons.cloud_download_outlined, size: 18),
            label: Text(
              _downloading ? l10n.operationDownloading : l10n.anrDownload,
            ),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: _downloading || _importing ? null : _import,
            style: LegacyButtonStyles.tall(LegacyButtonStyles.standard),
            icon: const Icon(Icons.folder_zip_outlined, size: 18),
            label: Text(l10n.anrImportZip),
          ),
          const SizedBox(height: 8),
          Row(
            children: <Widget>[
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => SystemBridge.openUrl(_paths.anrBaiduUrl),
                  style: LegacyButtonStyles.standard,
                  icon: const Icon(Icons.cloud_outlined, size: 16),
                  label: Text(l10n.anrBaidu),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => SystemBridge.openUrl(_paths.anrQuarkUrl),
                  style: LegacyButtonStyles.standard,
                  icon: const Icon(Icons.cloud_outlined, size: 16),
                  label: Text(l10n.anrQuark),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
