import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/app_settings.dart';
import '../../bridge/core_library.dart';
import '../../bridge/settings_bridge.dart';
import '../../bridge/system_bridge.dart';
import '../../bridge/system_paths.dart';
import '../../bridge/update_bridge.dart';
import '../../bridge/update_info.dart';
import '../../design/app_tokens.dart';
import '../../design/error_dialog.dart';
import '../../design/legacy_button_styles.dart';
import '../../design/legacy_dialog.dart';
import '../../design/legacy_panel.dart';
import '../../design/operation_progress_dialog.dart';
import '../../l10n/app_localizations.dart';

/// Legacy About page: version, startup, RE Engine and feedback panels.
class AboutPage extends StatefulWidget {
  const AboutPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<AboutPage> createState() => _AboutPageState();
}

class _AboutPageState extends State<AboutPage> {
  AppSettings _settings = const AppSettings();
  SystemPaths _paths = const SystemPaths();
  bool _checking = false;

  @override
  void initState() {
    super.initState();
    _settings = SettingsBridge.load(widget.core);
    _loadPaths();
  }

  Future<void> _loadPaths() async {
    final SystemPaths paths = await SystemBridge.paths();
    if (mounted) {
      setState(() => _paths = paths);
    }
  }

  Future<void> _save(AppSettings settings) async {
    setState(() => _settings = settings);
    if (!SettingsBridge.save(settings, widget.core)) {
      if (!mounted) {
        return;
      }
      await ErrorDialog.show(
        context,
        message: AppLocalizations.of(context).settingsSaveFailed,
        code: 'SettingsSaveFailed',
      );
      setState(() => _settings = SettingsBridge.load(widget.core));
    }
  }

  Future<void> _checkUpdates() async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    setState(() => _checking = true);
    final UpdateInfo info = await UpdateBridge.check();
    if (!mounted) {
      return;
    }
    setState(() => _checking = false);
    if (!info.ok) {
      await ErrorDialog.show(
        context,
        message: l10n.updateFailed(info.code ?? ''),
        code: info.code ?? 'UpdateCheckFailed',
      );
      return;
    }
    if (!info.available) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(l10n.updateLatest),
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }
    await _showUpdate(info);
  }

  Future<void> _showUpdate(UpdateInfo info) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool? update = await showLegacyDialog<bool>(
      context: context,
      builder: (BuildContext context) => AlertDialog(
        title: Text(l10n.updateAvailable(info.version)),
        content: SizedBox(
          width: 520,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text('${l10n.coreVersion(info.currentVersion)}'),
                const SizedBox(height: 8),
                if (info.notes.trim().isNotEmpty)
                  Text(
                    info.notes,
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
              ],
            ),
          ),
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(l10n.updateModalLater),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(l10n.updateDownload),
          ),
        ],
      ),
    );
    if (update == true && mounted) {
      await _downloadAndInstall(info);
    }
  }

  Future<void> _downloadAndInstall(UpdateInfo info) async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final UpdateInfo? downloaded = await OperationProgressDialog.run<UpdateInfo>(
      context: context,
      title: l10n.operationDownloading,
      action: UpdateBridge.download,
    );
    if (!mounted || downloaded == null) {
      return;
    }
    if (downloaded.code == 'OperationCancelled') {
      return;
    }
    if (!downloaded.ok) {
      await ErrorDialog.show(
        context,
        message: l10n.updateFailed(downloaded.code ?? ''),
        code: downloaded.code ?? 'UpdateDownloadFailed',
      );
      return;
    }
    if (downloaded.portable) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(l10n.updatePortableHint),
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }
    final String? path = downloaded.path;
    if (path == null) {
      return;
    }
    final bool started = await UpdateBridge.install(path);
    if (!mounted) {
      return;
    }
    if (started) {
      exit(0);
    }
    await ErrorDialog.show(
      context,
      message: l10n.updateFailed('UpdateInstall'),
      code: 'UpdateInstallFailed',
    );
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String version = widget.core?.version ?? '2.3.4';
    return SingleChildScrollView(
      padding: AppTokens.pagePadding,
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 760),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Text(
                l10n.navAbout,
                style: const TextStyle(
                  color: AppTokens.textPrimary,
                  fontSize: 28,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 18),
              LegacyPanel(
                title: l10n.aboutSoftwareVersion,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      'v$version',
                      style: const TextStyle(
                        color: AppTokens.textPrimary,
                        fontSize: 20,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      l10n.aboutDescription,
                      style: const TextStyle(
                        color: AppTokens.textSecondary,
                        fontSize: 12.5,
                      ),
                    ),
                    const SizedBox(height: 12),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: <Widget>[
                        FilledButton(
                          onPressed: _checking ? null : _checkUpdates,
                          style: LegacyButtonStyles.scan,
                          child: Text(
                            _checking
                                ? l10n.updateChecking
                                : l10n.aboutCheckUpdates,
                          ),
                        ),
                        OutlinedButton(
                          onPressed: () =>
                              SystemBridge.openUrl(_paths.netdiskUrl),
                          child: Text(l10n.aboutQuark),
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    _LinkRow(
                      label: l10n.aboutGithub,
                      value: _paths.repository,
                      action: l10n.aboutOpenRepo,
                      onPressed: () =>
                          SystemBridge.openUrl(_paths.repository),
                    ),
                    const SizedBox(height: 10),
                    _LinkRow(
                      label: l10n.aboutLicenses,
                      value: l10n.aboutLicensesHint,
                      action: l10n.aboutOpenLicenses,
                      onPressed: () =>
                          SystemBridge.openFolder('licenses'),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 14),
              LegacyPanel(
                title: l10n.aboutStartup,
                child: _SwitchRow(
                  title: l10n.aboutStartupScan,
                  hint: l10n.aboutStartupScanHint,
                  value: _settings.autoScanOnStartup,
                  onChanged: (bool value) =>
                      _save(_settings.copyWith(autoScanOnStartup: value)),
                ),
              ),
              const SizedBox(height: 14),
              LegacyPanel(
                title: l10n.aboutReEngine,
                child: _SwitchRow(
                  title: l10n.aboutReEngineInstall,
                  hint: l10n.aboutReEngineHint,
                  value: _settings.autoInstallReframework,
                  onChanged: (bool value) =>
                      _save(_settings.copyWith(autoInstallReframework: value)),
                ),
              ),
              const SizedBox(height: 14),
              LegacyPanel(
                title: l10n.aboutFeedback,
                child: Text(
                  l10n.aboutFeedbackBody,
                  style: const TextStyle(
                    color: AppTokens.textSecondary,
                    fontSize: 13,
                    height: 1.6,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _LinkRow extends StatelessWidget {
  const _LinkRow({
    required this.label,
    required this.value,
    required this.action,
    required this.onPressed,
  });

  final String label;
  final String value;
  final String action;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SizedBox(
          width: 130,
          child: Text(
            label,
            style: const TextStyle(
              color: AppTokens.textSecondary,
              fontSize: 12.5,
            ),
          ),
        ),
        Expanded(
          child: Text(
            value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: AppTokens.textPrimary,
              fontSize: 12,
              height: 1.45,
            ),
          ),
        ),
        const SizedBox(width: 8),
        TextButton(onPressed: onPressed, child: Text(action)),
      ],
    );
  }
}

class _SwitchRow extends StatelessWidget {
  const _SwitchRow({
    required this.title,
    required this.hint,
    required this.value,
    required this.onChanged,
  });

  final String title;
  final String hint;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                title,
                style: const TextStyle(
                  color: AppTokens.textPrimary,
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                hint,
                style: const TextStyle(
                  color: AppTokens.textSecondary,
                  fontSize: 11.5,
                  height: 1.45,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: 12),
        Switch(value: value, onChanged: onChanged),
      ],
    );
  }
}
