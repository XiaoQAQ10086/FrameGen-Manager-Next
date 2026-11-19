import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../bridge/update_bridge.dart';
import '../../bridge/update_info.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// About page with version information and the update workflow.
class AboutPage extends StatefulWidget {
  const AboutPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<AboutPage> createState() => _AboutPageState();
}

class _AboutPageState extends State<AboutPage> {
  UpdateInfo? _info;
  bool _checking = false;
  bool _downloading = false;
  bool _installing = false;

  Future<void> _check() async {
    setState(() => _checking = true);
    final UpdateInfo info = await UpdateBridge.check();
    if (!mounted) {
      return;
    }
    setState(() {
      _checking = false;
      _info = info;
    });
    if (!info.ok) {
      _notify(AppLocalizations.of(context).updateFailed(info.code ?? ''));
    }
  }

  Future<void> _download() async {
    setState(() => _downloading = true);
    final UpdateInfo info = await UpdateBridge.download();
    if (!mounted) {
      return;
    }
    setState(() {
      _downloading = false;
      _info = info;
    });
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (!info.ok) {
      _notify(l10n.updateFailed(info.code ?? ''));
    } else if (info.downloaded && info.portable) {
      _notify(l10n.updatePortableHint);
    } else if (info.downloaded) {
      _notify(l10n.updateDownloaded);
    }
  }

  Future<void> _install(UpdateInfo info) async {
    final String? path = info.path;
    if (path == null) {
      return;
    }
    setState(() => _installing = true);
    final bool started = await UpdateBridge.install(path);
    if (!mounted) {
      return;
    }
    setState(() => _installing = false);
    if (started) {
      exit(0);
    }
    _notify(AppLocalizations.of(context).updateFailed('UpdateInstall'));
  }

  void _notify(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String coreVersion = widget.core?.version ?? '—';

    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(l10n.navAbout, style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 20),
          Container(
            padding: const EdgeInsets.all(24),
            decoration: _cardDecoration(),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  l10n.appTitle,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 6),
                Text(
                  l10n.aboutDescription,
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
                const SizedBox(height: 18),
                _InfoRow(label: l10n.coreVersion(coreVersion)),
                const SizedBox(height: 8),
                _InfoRow(label: l10n.footerPlatform),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Container(
            padding: const EdgeInsets.all(24),
            decoration: _cardDecoration(),
            child: _updateSection(context, l10n),
          ),
        ],
      ),
    );
  }

  Widget _updateSection(BuildContext context, AppLocalizations l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: Text(
                l10n.updateTitle,
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            TextButton.icon(
              onPressed: _checking || _downloading ? null : _check,
              icon: const Icon(Icons.refresh, size: 16),
              label: Text(_checking ? l10n.updateChecking : l10n.updateCheck),
            ),
          ],
        ),
        if (_checking || _downloading) ...<Widget>[
          const SizedBox(height: 12),
          const LinearProgressIndicator(minHeight: 3),
          const SizedBox(height: 8),
          Text(
            _downloading ? l10n.updateDownloading : l10n.updateChecking,
            style: Theme.of(context).textTheme.labelMedium,
          ),
        ],
        if (_info != null && !_checking) ...<Widget>[
          const SizedBox(height: 12),
          _updateBody(context, l10n, _info!),
        ],
      ],
    );
  }

  Widget _updateBody(
    BuildContext context,
    AppLocalizations l10n,
    UpdateInfo info,
  ) {
    if (!info.ok) {
      return Text(
        l10n.updateFailed(info.code ?? ''),
        style: Theme.of(context).textTheme.bodyMedium,
      );
    }
    if (!info.available) {
      return Text(
        l10n.updateLatest,
        style: Theme.of(context).textTheme.bodyMedium,
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          l10n.updateAvailable(info.version),
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        if (info.notes.trim().isNotEmpty) ...<Widget>[
          const SizedBox(height: 10),
          Text(l10n.updateNotes, style: Theme.of(context).textTheme.labelMedium),
          const SizedBox(height: 4),
          ConstrainedBox(
            constraints: const BoxConstraints(maxHeight: 120),
            child: SingleChildScrollView(
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: AppTokens.surfaceMuted,
                  borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
                  border: Border.all(color: AppTokens.border),
                ),
                child: Text(
                  info.notes,
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
            ),
          ),
        ],
        const SizedBox(height: 14),
        Wrap(
          spacing: 10,
          runSpacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: <Widget>[
            if (!info.downloaded && !_downloading)
              FilledButton.icon(
                onPressed: _download,
                icon: const Icon(Icons.download, size: 16),
                label: Text(l10n.updateDownload),
              ),
            if (info.downloaded && !info.portable)
              FilledButton.icon(
                onPressed: _installing ? null : () => _install(info),
                icon: const Icon(Icons.system_update_alt, size: 16),
                label: Text(l10n.updateInstall),
              ),
            if (info.downloaded && info.portable)
              Text(
                l10n.updatePortableHint,
                style: Theme.of(context).textTheme.labelMedium,
              ),
            if ((info.pageUrl ?? '').isNotEmpty)
              TextButton.icon(
                onPressed: () => UpdateBridge.open(info.pageUrl!),
                icon: const Icon(Icons.open_in_new, size: 14),
                label: Text(l10n.updateOpenPage),
              ),
          ],
        ),
      ],
    );
  }

  static BoxDecoration _cardDecoration() {
    return BoxDecoration(
      color: AppTokens.surface,
      borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
      border: Border.all(color: AppTokens.border),
      boxShadow: AppTokens.panelShadow,
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Text(label, style: Theme.of(context).textTheme.labelMedium);
  }
}
