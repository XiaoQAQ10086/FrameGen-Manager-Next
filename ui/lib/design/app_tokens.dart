import 'package:material_ui/material_ui.dart';

/// Visual tokens mirrored from the legacy WebView2 UI.
abstract final class AppTokens {
  static const Color background = Color(0xFFF3F6FB);
  static const Color backgroundEnd = Color(0xFFEEF2F8);
  static const Color surface = Color(0xF2FFFFFF);
  static const Color surfaceSolid = Colors.white;
  static const Color surfaceMuted = Color(0xFFF8FAFC);
  static const Color border = Color(0xFFDDE4EE);
  static const Color cardBorder = Color(0xFFDCE5F2);
  static const Color textPrimary = Color(0xFF172033);
  static const Color textSecondary = Color(0xFF606B7D);
  static const Color accent = Color(0xFF007AFF);
  static const Color accentSoft = Color(0x1A007AFF);
  static const Color success = Color(0xFF0B6B3A);
  static const Color successSoft = Color(0xFFE8F6EE);
  static const Color warning = Color(0xFFB25A00);
  static const Color warningSoft = Color(0xFFFDF0E1);
  static const Color danger = Color(0xFFA52222);
  static const Color dangerSoft = Color(0xFFFBEAEA);
  static const Color scanGreen = Color(0xFF1B9D57);
  static const Color launchYellow = Color(0xFFF4C542);

  static const double radius = 16;
  static const double radiusLarge = 16;
  static const double radiusMedium = 12;
  static const double radiusSmall = 10;
  static const double gap = 14;

  static const EdgeInsets pagePadding = EdgeInsets.fromLTRB(20, 18, 20, 24);
  static const EdgeInsets panelPadding = EdgeInsets.all(14);

  static const List<BoxShadow> panelShadow = <BoxShadow>[
    BoxShadow(
      color: Color(0x1A18274B),
      blurRadius: 34,
      offset: Offset(0, 12),
    ),
  ];
  static const List<BoxShadow> cardShadow = <BoxShadow>[
    BoxShadow(color: Color(0x1418274B), blurRadius: 22, offset: Offset(0, 8)),
    BoxShadow(color: Color(0x0A18274B), blurRadius: 3, offset: Offset(0, 1)),
  ];
  static const List<BoxShadow> strongShadow = <BoxShadow>[
    BoxShadow(
      color: Color(0x33181E3C),
      blurRadius: 70,
      offset: Offset(0, 24),
    ),
  ];

  static const BoxDecoration panelDecoration = BoxDecoration(
    color: surfaceSolid,
    borderRadius: BorderRadius.all(Radius.circular(radius)),
    border: Border.fromBorderSide(BorderSide(color: cardBorder)),
  );

  static const BoxDecoration cardDecoration = BoxDecoration(
    color: surfaceSolid,
    borderRadius: BorderRadius.all(Radius.circular(radius)),
    border: Border.fromBorderSide(BorderSide(color: cardBorder)),
    boxShadow: cardShadow,
  );
}
