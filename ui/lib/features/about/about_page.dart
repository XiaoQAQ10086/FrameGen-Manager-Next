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
import '../../design/legacy_toast.dart';
import '../../design/operation_progress_dialog.dart';
import '../../l10n/app_localizations.dart';
import 'about_link_row.dart';
import 'about_switch_row.dart';

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
      // A source that has published nothing yet is not something the user can
      // act on, so it does not deserve a modal.
      LegacyToast.show(
        context,
        info.code == 'UpdateNotFound'
            ? l10n.updateLatest
            : l10n.updateFailed(info.code ?? ''),
      );
      return;
    }
    if (!info.available) {
      LegacyToast.show(context, l10n.updateLatest);
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
      LegacyToast.show(context, l10n.updatePortableHint);
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
          Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 760),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
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
                        fontSize: 13,
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
                    AboutLinkRow(
                      label: l10n.aboutGithub,
                      value: _paths.repository,
                      action: l10n.aboutOpenRepo,
                      onPressed: () =>
                          SystemBridge.openUrl(_paths.repository),
                    ),
                    const SizedBox(height: 10),
                    AboutLinkRow(
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
                child: AboutSwitchRow(
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
                child: AboutSwitchRow(
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
        ],
      ),
    );
  }
}
