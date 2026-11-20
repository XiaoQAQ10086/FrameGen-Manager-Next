import 'package:material_ui/material_ui.dart';

/// Shared motion timings and curves for the legacy-styled shell.
///
/// Every animation in the UI pulls from these four tiers so durations and
/// easings stay consistent across dialogs, menus, switches and page changes.
class MotionTokens {
  const MotionTokens._();

  /// Snappy feedback: dropdown menus and other instantly-opened overlays.
  static const Duration quick = Duration(milliseconds: 120);

  /// Default control transition: buttons, switches, navigation labels.
  static const Duration standard = Duration(milliseconds: 180);

  /// Entrance of modal overlays: dialogs and their backdrop dimming.
  static const Duration enter = Duration(milliseconds: 220);

  /// Page-level cross-fade between sections.
  static const Duration page = Duration(milliseconds: 300);

  /// Deceleration for elements appearing or settling into place.
  static const Curve enterCurve = Curves.easeOutCubic;

  /// Acceleration for elements leaving.
  static const Curve exitCurve = Curves.easeInCubic;

  /// Symmetric ease applied while a page fades in.
  static const Curve pageInCurve = Cubic(0.22, 0.61, 0.36, 1);

  /// Symmetric ease applied while a page fades out.
  static const Curve pageOutCurve = Cubic(0.55, 0.06, 0.68, 0.19);
}
