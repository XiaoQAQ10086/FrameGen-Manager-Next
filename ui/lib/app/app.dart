import 'package:material_ui/material_ui.dart';

import '../bridge/core_library.dart';
import '../design/app_theme.dart';
import '../l10n/app_localizations.dart';
import 'app_shell.dart';

/// Application root. Locale and the native bridge are injected here so tests
/// can run without the Rust cdylib.
class FgmApp extends StatelessWidget {
  const FgmApp({super.key, this.core, this.locale});

  final CoreLibrary? core;
  final Locale? locale;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      onGenerateTitle: (BuildContext context) =>
          AppLocalizations.of(context).appTitle,
      debugShowCheckedModeBanner: false,
      locale: locale,
      theme: AppTheme.light,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        AppLocalizations.delegate,
        ...GlobalMaterialLocalizations.delegates,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      home: AppShell(core: core),
    );
  }
}
