import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';
import 'legacy_button_styles.dart';

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
      filledButtonTheme: FilledButtonThemeData(
        style: LegacyButtonStyles.primary,
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: LegacyButtonStyles.standard,
      ),
      textButtonTheme: TextButtonThemeData(
        style: LegacyButtonStyles.ghost,
      ),
      iconButtonTheme: IconButtonThemeData(
        style: LegacyButtonStyles.iconButton,
      ),
      switchTheme: SwitchThemeData(
        thumbColor: const WidgetStatePropertyAll<Color>(Colors.white),
        trackColor: WidgetStateProperty.resolveWith<Color>(
          (Set<WidgetState> states) => states.contains(WidgetState.selected)
              ? AppTokens.accent
              : const Color(0xFFD9DDE3),
        ),
        trackOutlineColor: const WidgetStatePropertyAll<Color>(
          Colors.transparent,
        ),
        trackOutlineWidth: const WidgetStatePropertyAll<double>(0),
        materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
      ),
      popupMenuTheme: PopupMenuThemeData(
        color: const Color(0xFFFDFDFE),
        surfaceTintColor: Colors.transparent,
        elevation: 12,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
          side: const BorderSide(color: Color(0x1F14233C)),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: Colors.white,
        surfaceTintColor: Colors.transparent,
        elevation: 18,
        shadowColor: const Color(0x40101828),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(20),
          side: const BorderSide(color: Color(0x1F14233C)),
        ),
      ),
      textTheme: const TextTheme(
        displaySmall: TextStyle(
          color: AppTokens.textPrimary,
          fontSize: 28,
          fontWeight: FontWeight.w700,
          height: 1.25,
        ),
        headlineSmall: TextStyle(
          color: AppTokens.textPrimary,
          fontSize: 20,
          fontWeight: FontWeight.w700,
          letterSpacing: 0.2,
          height: 1.25,
        ),
        titleLarge: TextStyle(
          color: AppTokens.textPrimary,
          fontSize: 16,
          fontWeight: FontWeight.w600,
          height: 1.25,
        ),
        titleMedium: TextStyle(
          color: AppTokens.textPrimary,
          fontSize: 14,
          fontWeight: FontWeight.w700,
          height: 1.35,
        ),
        bodyMedium: TextStyle(
          color: AppTokens.textSecondary,
          fontSize: 14,
          height: 1.5,
        ),
        bodySmall: TextStyle(
          color: AppTokens.textSecondary,
          fontSize: 13,
          height: 1.5,
        ),
        labelLarge: TextStyle(
          color: AppTokens.textPrimary,
          fontSize: 13,
          fontWeight: FontWeight.w500,
          height: 1.4,
        ),
        labelMedium: TextStyle(
          color: AppTokens.textSecondary,
          fontSize: 12,
          fontWeight: FontWeight.w500,
          height: 1.4,
        ),
        labelSmall: TextStyle(
          color: AppTokens.textSecondary,
          fontSize: 11,
          fontWeight: FontWeight.w500,
          height: 1.35,
        ),
      ),
    );
  }
}
