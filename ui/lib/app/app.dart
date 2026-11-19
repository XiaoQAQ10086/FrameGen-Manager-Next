import 'package:material_ui/material_ui.dart';

import '../bridge/app_settings.dart';
import '../bridge/core_library.dart';
import '../bridge/settings_bridge.dart';
import '../design/app_theme.dart';
import '../l10n/app_localizations.dart';
import 'app_shell.dart';

/// Application root. Locale and the native bridge are injected here so tests
/// can run without the Rust cdylib.
class FgmApp extends StatefulWidget {
  const FgmApp({super.key, this.core, this.locale});

  final CoreLibrary? core;
  final Locale? locale;

  @override
  State<FgmApp> createState() => _FgmAppState();
}

class _FgmAppState extends State<FgmApp> {
  Locale? _locale;

  @override
  void initState() {
    super.initState();
    _locale = widget.locale;
    if (_locale == null && widget.core != null) {
      final AppSettings settings = SettingsBridge.load(widget.core);
      _locale = _localeFor(settings.language);
    }
  }

  void _setLanguage(String language) {
    setState(() => _locale = _localeFor(language));
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      onGenerateTitle: (BuildContext context) =>
          AppLocalizations.of(context).appTitle,
      debugShowCheckedModeBanner: false,
      locale: _locale,
      theme: AppTheme.light,
      themeMode: ThemeMode.light,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        AppLocalizations.delegate,
        ...GlobalMaterialLocalizations.delegates,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      home: AppShell(
        core: widget.core,
        onLanguageChanged: _setLanguage,
      ),
    );
  }
}

Locale? _localeFor(String language) {
  return switch (language) {
    'chineseSimplified' => const Locale('zh'),
    'english' => const Locale('en'),
    _ => null,
  };
}
