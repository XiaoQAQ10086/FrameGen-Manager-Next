import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

/// Light Material 3 theme for the desktop shell.
abstract final class AppTheme {
  static ThemeData get light {
    final ColorScheme scheme = ColorScheme.fromSeed(
      seedColor: AppTokens.accent,
      brightness: Brightness.light,
    ).copyWith(
      primary: AppTokens.accent,
      surface: AppTokens.surface,
      outline: AppTokens.border,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: AppTokens.background,
      fontFamily: 'Microsoft YaHei UI',
      fontFamilyFallback: const <String>['Microsoft YaHei', 'Segoe UI'],
      dividerColor: AppTokens.border,
      splashFactory: InkSparkle.splashFactory,
      textTheme: const TextTheme(
        headlineSmall: TextStyle(
          color: AppTokens.textPrimary,
          fontSize: 20,
          fontWeight: FontWeight.w700,
          letterSpacing: 0.2,
        ),
        titleMedium: TextStyle(
          color: AppTokens.textPrimary,
          fontSize: 15,
          fontWeight: FontWeight.w600,
        ),
        bodyMedium: TextStyle(
          color: AppTokens.textSecondary,
          fontSize: 14,
          height: 1.5,
        ),
        labelMedium: TextStyle(
          color: AppTokens.textSecondary,
          fontSize: 12,
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }
}
