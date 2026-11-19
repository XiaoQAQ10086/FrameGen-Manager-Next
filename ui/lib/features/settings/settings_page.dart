import 'package:material_ui/material_ui.dart';

import '../../bridge/app_settings.dart';
import '../../bridge/core_library.dart';
import '../../bridge/settings_bridge.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// Settings page: language, startup scan and component toggles.
class SettingsPage extends StatefulWidget {
  const SettingsPage({super.key, this.core, this.onLanguageChanged});

  final CoreLibrary? core;
  final ValueChanged<String>? onLanguageChanged;

  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  late AppSettings _settings;

  @override
  void initState() {
    super.initState();
    _settings = SettingsBridge.load(widget.core);
  }

  void _update(AppSettings settings, {String? language}) {
    if (!SettingsBridge.save(settings, widget.core)) {
      final AppLocalizations l10n = AppLocalizations.of(context);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.settingsSaveFailed)),
      );
      return;
    }
    setState(() => _settings = settings);
    if (language != null) {
      widget.onLanguageChanged?.call(language);
    }
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);

    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.navSettings,
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 20),
          Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
              color: AppTokens.surface,
              borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
              border: Border.all(color: AppTokens.border),
              boxShadow: AppTokens.panelShadow,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                DropdownButtonFormField<String>(
                  initialValue: _settings.language,
                  decoration: InputDecoration(
                    labelText: l10n.settingsLanguage,
                  ),
                  items: <DropdownMenuItem<String>>[
                    DropdownMenuItem<String>(
                      value: 'system',
                      child: Text(l10n.languageSystem),
                    ),
                    DropdownMenuItem<String>(
                      value: 'chineseSimplified',
                      child: Text(l10n.languageChinese),
                    ),
                    DropdownMenuItem<String>(
                      value: 'english',
                      child: Text(l10n.languageEnglish),
                    ),
                  ],
                  onChanged: (String? value) {
                    if (value == null) {
                      return;
                    }
                    _update(
                      _settings.copyWith(language: value),
                      language: value,
                    );
                  },
                ),
                const SizedBox(height: 16),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(l10n.settingsAutoScan),
                  value: _settings.autoScanOnStartup,
                  onChanged: (bool value) => _update(
                    _settings.copyWith(autoScanOnStartup: value),
                  ),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(l10n.settingsShowCovers),
                  value: _settings.showCovers,
                  onChanged: (bool value) =>
                      _update(_settings.copyWith(showCovers: value)),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(l10n.settingsAutoInstallReframework),
                  value: _settings.autoInstallReframework,
                  onChanged: (bool value) => _update(
                    _settings.copyWith(autoInstallReframework: value),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
