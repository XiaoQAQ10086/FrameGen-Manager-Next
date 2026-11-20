import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

/// Legacy WebView2 button surfaces reused through Material button styles.
///
/// Standard buttons use a quiet blue-grey face with a crisp border, while the
/// colored actions keep a soft vertical gradient and a readable white label.
abstract final class LegacyButtonStyles {
  static const Color _ink = Color(0xFF1D2A3F);
  static const Color _shadow = Color(0x24101828);
  static const Color _borderLight = Color(0xFFC2D2E6);
  static const Color _dangerBorder = Color(0xFFE6C2C2);
  static const Color _dangerFace = Color(0xFFFEF7F7);
  static const Color _neutralFace = Color(0xFFF6F9FE);
  static const Color _accentBorder = Color(0x8C007AFF);

  /// Default `.btn`: quiet blue-grey face with a dark label.
  static ButtonStyle get standard => _style(
    top: const Color(0xFFF0F6FE),
    bottom: const Color(0xFFD2E0F2),
    border: const Color(0xFF8FA9C9),
    foreground: _ink,
    shadow: Colors.transparent,
    elevation: 0,
  );

  /// `.btn.primary`: blue gradient for confirm actions.
  static ButtonStyle get primary => _style(
    top: const Color(0xFF2B9BFF),
    bottom: AppTokens.accent,
    border: const Color(0xFF0066D6),
    foreground: Colors.white,
    shadow: const Color(0x2E0066D6),
    elevation: 2,
  );

  /// `.btn.success` / deploy actions.
  static ButtonStyle get success => _style(
    top: const Color(0xFF1F9243),
    bottom: const Color(0xFF116B2D),
    border: const Color(0xFF0B5523),
    foreground: Colors.white,
    shadow: const Color(0x2E0F632A),
    elevation: 2,
  );

  /// `.btn.scan`: green action with a stronger glow.
  static ButtonStyle get scan => _style(
    top: const Color(0xFF1F9243),
    bottom: const Color(0xFF116B2D),
    border: const Color(0xFF0B5523),
    foreground: Colors.white,
    shadow: const Color(0x40157A33),
    elevation: 3,
  );

  /// `.btn.danger`: soft red surface.
  static ButtonStyle get danger => _style(
    top: const Color(0xFFFFF4F4),
    bottom: const Color(0xFFFFE0E0),
    border: const Color(0xFFD98F8F),
    foreground: AppTokens.danger,
    shadow: const Color(0x1ABE2A2A),
    elevation: 0,
  );

  /// `.btn.launch`: yellow launch action.
  static ButtonStyle get launch => _style(
    top: const Color(0xFFFFD75E),
    bottom: const Color(0xFFF2AE00),
    border: const Color(0xFFD99A00),
    foreground: const Color(0xFF3D2C00),
    shadow: const Color(0x33D99A00),
    elevation: 2,
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

  /// Flat white square used by modal headers (no shadow, no dark corners).
  static ButtonStyle get iconButton => _flat(
    background: _neutralFace,
    border: _borderLight,
    foreground: AppTokens.textSecondary,
    size: 34,
    iconSize: 18,
  );

  /// Full-width panel action with a taller, more readable face.
  static ButtonStyle tall(ButtonStyle base) {
    final Color foreground = _foregroundOf(base);
    return base.copyWith(
      minimumSize: const WidgetStatePropertyAll<Size>(Size(0, 44)),
      padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(
        EdgeInsets.symmetric(horizontal: 18, vertical: 11),
      ),
      textStyle: WidgetStatePropertyAll<TextStyle>(
        _textStyle(foreground, 13.5),
      ),
    );
  }

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

  /// Compact `.btn.small` used inside game cards.
  static ButtonStyle compact(ButtonStyle base) => base.copyWith(
    minimumSize: const WidgetStatePropertyAll<Size>(Size(108, 38)),
    padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(
      EdgeInsets.symmetric(horizontal: 18, vertical: 8),
    ),
    textStyle: WidgetStatePropertyAll<TextStyle>(
      _textStyle(_foregroundOf(base), 13),
    ),
    shape: WidgetStatePropertyAll<OutlinedBorder>(
      RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
    ),
    elevation: const WidgetStatePropertyAll<double>(0),
  );

  /// `.btn.small.icon-only`: flat 34px square for card actions.
  static ButtonStyle compactIcon(ButtonStyle base) {
    final Color foreground = _foregroundOf(base);
    final bool danger = foreground == AppTokens.danger;
    return _flat(
      background: danger ? _dangerFace : _neutralFace,
      border: danger ? _dangerBorder : _borderLight,
      foreground: foreground,
      size: 38,
      iconSize: 17,
    );
  }

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
    final bool lightText = foreground.computeLuminance() > 0.6;
    return ButtonStyle(
      backgroundBuilder: _surface(top: top, bottom: bottom, border: border),
      foregroundColor: _stateColor(foreground),
      iconColor: _stateColor(foreground),
      side: WidgetStateProperty.resolveWith<BorderSide>(
        (Set<WidgetState> states) {
          if (states.contains(WidgetState.disabled)) {
            return BorderSide(color: border.withValues(alpha: 0.45));
          }
          if (states.contains(WidgetState.hovered)) {
            return const BorderSide(color: _accentBorder, width: 1.4);
          }
          return BorderSide(color: border, width: 1.4);
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
        return s.contains(WidgetState.hovered) ? elevation + 2 : elevation;
      }),
      minimumSize: const WidgetStatePropertyAll<Size>(Size(0, 38)),
      padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(
        EdgeInsets.symmetric(horizontal: 18, vertical: 9),
      ),
      textStyle: WidgetStatePropertyAll<TextStyle>(
        _textStyle(foreground, 13.5, strong: lightText),
      ),
      shape: WidgetStatePropertyAll<OutlinedBorder>(
        RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppTokens.radiusSmall),
        ),
      ),
      animationDuration: const Duration(milliseconds: 160),
    );
  }

  /// Flat square icon button without elevation, used for card/toolbar icons.
  static ButtonStyle _flat({
    required Color background,
    required Color border,
    required Color foreground,
    required double size,
    required double iconSize,
  }) {
    return ButtonStyle(
      backgroundColor: WidgetStateProperty.resolveWith<Color>(
        (Set<WidgetState> states) {
          if (states.contains(WidgetState.disabled)) {
            return background.withValues(alpha: 0.5);
          }
          if (states.contains(WidgetState.hovered)) {
            return const Color(0xFFF0F6FE);
          }
          return background;
        },
      ),
      foregroundColor: _stateColor(foreground),
      iconColor: _stateColor(foreground),
      overlayColor: const WidgetStatePropertyAll<Color>(AppTokens.accentSoft),
      side: WidgetStateProperty.resolveWith<BorderSide>(
        (Set<WidgetState> states) => BorderSide(
          color: states.contains(WidgetState.hovered) ? _accentBorder : border,
        ),
      ),
      elevation: const WidgetStatePropertyAll<double>(0),
      shadowColor: const WidgetStatePropertyAll<Color>(Colors.transparent),
      surfaceTintColor: const WidgetStatePropertyAll<Color>(
        Colors.transparent,
      ),
      minimumSize: WidgetStatePropertyAll<Size>(Size.square(size)),
      maximumSize: WidgetStatePropertyAll<Size>(Size.square(size)),
      padding: const WidgetStatePropertyAll<EdgeInsetsGeometry>(EdgeInsets.zero),
      iconSize: WidgetStatePropertyAll<double>(iconSize),
      shape: WidgetStatePropertyAll<OutlinedBorder>(
        RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    );
  }

  static TextStyle _textStyle(
    Color color,
    double size, {
    bool strong = false,
  }) {
    return TextStyle(
      color: color,
      fontSize: size,
      fontWeight: strong ? FontWeight.w700 : FontWeight.w600,
      letterSpacing: 0.2,
      shadows: color == Colors.white
          ? const <Shadow>[
              Shadow(
                color: Color(0x4D000000),
                offset: Offset(0, 1),
                blurRadius: 1,
              ),
            ]
          : null,
    );
  }

  static WidgetStateProperty<Color> _stateColor(Color color) {
    return WidgetStateProperty.resolveWith<Color>(
      (Set<WidgetState> states) => states.contains(WidgetState.disabled)
          ? color.withValues(alpha: 0.45)
          : color,
    );
  }

  static Color _foregroundOf(ButtonStyle style) {
    return style.foregroundColor?.resolve(<WidgetState>{}) ?? _ink;
  }

  static ButtonLayerBuilder _surface({
    required Color top,
    required Color bottom,
    required Color border,
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
            border: Border.all(
              color: disabled
                  ? border.withValues(alpha: 0.45)
                  : hovered
                  ? _accentBorder
                  : border,
              width: 1.4,
            ),
          ),
          child: child,
        ),
      );
    };
  }
}
