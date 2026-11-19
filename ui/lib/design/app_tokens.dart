import 'package:material_ui/material_ui.dart';

/// Visual constants shared by the shell and feature pages.
abstract final class AppTokens {
  static const Color background = Color(0xFFF4F7FB);
  static const Color surface = Colors.white;
  static const Color surfaceMuted = Color(0xFFF8FAFC);
  static const Color border = Color(0xFFE2E8F0);
  static const Color textPrimary = Color(0xFF0F172A);
  static const Color textSecondary = Color(0xFF64748B);
  static const Color accent = Color(0xFF0A84FF);
  static const Color accentSoft = Color(0xFFE6F1FF);
  static const Color success = Color(0xFF1F9D55);
  static const Color successSoft = Color(0xFFE8F6EE);
  static const Color warning = Color(0xFFB45309);
  static const Color warningSoft = Color(0xFFFEF3C7);

  static const double radiusLarge = 20;
  static const double radiusMedium = 14;
  static const double gap = 16;

  static const EdgeInsets pagePadding = EdgeInsets.all(24);

  static const List<BoxShadow> panelShadow = [
    BoxShadow(
      color: Color(0x0A0F172A),
      blurRadius: 24,
      offset: Offset(0, 8),
    ),
  ];
}
