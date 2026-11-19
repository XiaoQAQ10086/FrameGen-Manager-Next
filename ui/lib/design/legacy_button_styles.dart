import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

/// Legacy WebView2 button surfaces reused through Material button styles.
///
/// Every variant paints the same layered look as `.btn` in the legacy
/// stylesheet: a soft vertical gradient, a 1px border, a small drop shadow and
/// a slightly brighter face on hover.
abstract final class LegacyButtonStyles {
  static const Color _ink = Color(0xFF26344A);
  static const Color _shadow = Color(0x24101828);

  /// Default `.btn`: light gradient face with a dark label.
  static ButtonStyle get standard => _style(
    top: const Color(0xFFF4F9FF),
    bottom: const Color(0xFFD4E2F3),
    border: const Color(0xFFA6BCD8),
    foreground: _ink,
    shadow: const Color(0x33101828),
  );

  /// `.btn.primary`: blue gradient for confirm actions.
  static ButtonStyle get primary => _style(
    top: const Color(0xFF2B9BFF),
    bottom: AppTokens.accent,
    border: const Color(0xFF0066D6),
    foreground: Colors.white,
    shadow: const Color(0x2E0066D6),
  );

  /// `.btn.success` / deploy actions.
  static ButtonStyle get success => _style(
    top: const Color(0xFF239F48),
    bottom: const Color(0xFF157A33),
    border: const Color(0xFF0F632A),
    foreground: Colors.white,
    shadow: const Color(0x2E0F632A),
  );

  /// `.btn.scan`: green action with a stronger glow.
  static ButtonStyle get scan => _style(
    top: const Color(0xFF239F48),
    bottom: const Color(0xFF157A33),
    border: const Color(0xFF0F632A),
    foreground: Colors.white,
    shadow: const Color(0x40157A33),
    elevation: 3,
  );

  /// `.btn.danger`: soft red surface.
  static ButtonStyle get danger => _style(
    top: const Color(0xFFFFF1F1),
    bottom: const Color(0xFFFFD9D9),
    border: const Color(0xFFD98F8F),
    foreground: AppTokens.danger,
    shadow: const Color(0x26BE2A2A),
  );

  /// `.btn.launch`: yellow launch action.
  static ButtonStyle get launch => _style(
    top: const Color(0xFFFFD34D),
    bottom: const Color(0xFFF5B301),
    border: const Color(0xFFD99A00),
    foreground: const Color(0xFF3D2C00),
    shadow: const Color(0x33D99A00),
  );

  /// `.btn.ghost`: borderless accent text action.
  static ButtonStyle get ghost => ButtonStyle(
    backgroundColor: const WidgetStatePropertyAll<Color>(Colors.transparent),
    foregroundColor: WidgetStateProperty.resolveWith<Color>(
      (Set<WidgetState> states) => states.contains(WidgetState.disabled)
          ? AppTokens.accent.withValues(alpha: 0.45)
          : AppTokens.accent,
    ),
    overlayColor: const WidgetStatePropertyAll<Color>(AppTokens.accentSoft),
    elevation: const WidgetStatePropertyAll<double>(0),
    minimumSize: const WidgetStatePropertyAll<Size>(Size(0, 30)),
    padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(
      EdgeInsets.symmetric(horizontal: 10, vertical: 6),
    ),
    textStyle: WidgetStatePropertyAll<TextStyle>(
      _textStyle(AppTokens.accent, 12.5),
    ),
    shape: WidgetStatePropertyAll<OutlinedBorder>(
      RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppTokens.radiusSmall),
      ),
    ),
  );

  /// `.icon-btn`: 34x34 white square used by modal headers.
  static ButtonStyle get iconButton => standard.copyWith(
    minimumSize: const WidgetStatePropertyAll<Size>(Size.square(34)),
    padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(EdgeInsets.zero),
    iconSize: const WidgetStatePropertyAll<double>(18),
  );

  /// List-head actions share one width so the three buttons line up.
  static ButtonStyle get headingAction => standard.copyWith(
    minimumSize: const WidgetStatePropertyAll<Size>(Size(116, 38)),
    padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(
      EdgeInsets.symmetric(horizontal: 16),
    ),
  );

  static ButtonStyle get headingScan => scan.copyWith(
    minimumSize: const WidgetStatePropertyAll<Size>(Size(116, 38)),
    padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(
      EdgeInsets.symmetric(horizontal: 16),
    ),
  );

  /// Compact `.btn.small` used inside game cards (27px tall, radius 9).
  static ButtonStyle compact(ButtonStyle base) => base.copyWith(
    minimumSize: const WidgetStatePropertyAll<Size>(Size(88, 34)),
    padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(
      EdgeInsets.symmetric(horizontal: 16, vertical: 7),
    ),
    textStyle: WidgetStatePropertyAll<TextStyle>(
      _textStyle(_foregroundOf(base), 12.5),
    ),
    shape: WidgetStatePropertyAll<OutlinedBorder>(
      RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
    ),
    elevation: const WidgetStatePropertyAll<double>(1),
  );

  /// `.btn.small.icon-only`: 30x30 gradient square for card actions.
  static ButtonStyle compactIcon(ButtonStyle base) => base.copyWith(
    minimumSize: const WidgetStatePropertyAll<Size>(Size.square(34)),
    maximumSize: const WidgetStatePropertyAll<Size>(Size.square(34)),
    padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(EdgeInsets.zero),
    iconSize: const WidgetStatePropertyAll<double>(16),
    shape: WidgetStatePropertyAll<OutlinedBorder>(
      RoundedRectangleBorder(borderRadius: BorderRadius.circular(9)),
    ),
  );

  /// Picks a variant from a legacy accent color.
  static ButtonStyle of(Color color) {
    if (color == AppTokens.danger) {
      return danger;
    }
    if (color == AppTokens.accent) {
      return primary;
    }
    return success;
  }

  /// Shared gradient/border surface for filled and outlined buttons.
  static ButtonStyle _style({
    required Color top,
    required Color bottom,
    required Color border,
    required Color foreground,
    Color? shadow,
    double elevation = 1,
  }) {
    return ButtonStyle(
      backgroundBuilder: _surface(top: top, bottom: bottom),
      foregroundColor: WidgetStateProperty.resolveWith<Color>(
        (Set<WidgetState> states) => states.contains(WidgetState.disabled)
            ? foreground.withValues(alpha: 0.45)
            : foreground,
      ),
      iconColor: WidgetStateProperty.resolveWith<Color>(
        (Set<WidgetState> states) => states.contains(WidgetState.disabled)
            ? foreground.withValues(alpha: 0.45)
            : foreground,
      ),
      side: WidgetStateProperty.resolveWith<BorderSide>(
        (Set<WidgetState> states) {
          if (states.contains(WidgetState.disabled)) {
            return BorderSide(color: border.withValues(alpha: 0.45));
          }
          if (states.contains(WidgetState.hovered)) {
            return const BorderSide(color: Color(0x8C007AFF));
          }
          return BorderSide(color: border);
        },
      ),
      shadowColor: WidgetStatePropertyAll<Color>(shadow ?? _shadow),
      surfaceTintColor: const WidgetStatePropertyAll<Color>(
        Colors.transparent,
      ),
      overlayColor: const WidgetStatePropertyAll<Color>(Colors.transparent),
      elevation: WidgetStateProperty.resolveWith<double>((Set<WidgetState> s) {
        if (s.contains(WidgetState.disabled)) {
          return 0;
        }
        return s.contains(WidgetState.hovered) ? elevation + 3 : elevation;
      }),
      minimumSize: const WidgetStatePropertyAll<Size>(Size(0, 38)),
      padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(
        EdgeInsets.symmetric(horizontal: 18, vertical: 9),
      ),
      textStyle: WidgetStatePropertyAll<TextStyle>(
        _textStyle(foreground, 13),
      ),
      shape: WidgetStatePropertyAll<OutlinedBorder>(
        RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppTokens.radiusSmall),
        ),
      ),
      animationDuration: const Duration(milliseconds: 160),
    );
  }

  static TextStyle _textStyle(Color color, double size) {
    return TextStyle(
      color: color,
      fontSize: size,
      fontWeight: FontWeight.w600,
      letterSpacing: 0.1,
    );
  }

  static Color _foregroundOf(ButtonStyle style) {
    return style.foregroundColor?.resolve(<WidgetState>{}) ?? _ink;
  }

  static ButtonLayerBuilder _surface({
    required Color top,
    required Color bottom,
  }) {
    return (BuildContext context, Set<WidgetState> states, Widget? child) {
      final bool disabled = states.contains(WidgetState.disabled);
      final bool pressed = states.contains(WidgetState.pressed);
      final bool hovered = states.contains(WidgetState.hovered);
      final List<Color> colors;
      if (pressed) {
        colors = <Color>[bottom, top];
      } else if (hovered) {
        colors = <Color>[
          Color.lerp(top, Colors.white, 0.16)!,
          Color.lerp(bottom, Colors.white, 0.06)!,
        ];
      } else {
        colors = <Color>[top, bottom];
      }
      return Opacity(
        opacity: disabled ? 0.45 : 1,
        child: DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: colors,
            ),
            borderRadius: BorderRadius.circular(AppTokens.radiusSmall),
          ),
          child: child,
        ),
      );
    };
  }
}
