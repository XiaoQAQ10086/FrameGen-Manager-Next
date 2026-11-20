import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

/// Rounded legacy badge used for states, launchers and proxy owners.
///
/// When a soft tonal background is used, the label is darkened to the matching
/// text token so small pill text stays legible; the border keeps the original
/// accent tone.
class LegacyPill extends StatelessWidget {
  const LegacyPill({
    super.key,
    required this.label,
    this.foreground = AppTokens.textSecondary,
    this.background = AppTokens.surfaceMuted,
    this.border,
    this.fontSize = 12,
    this.maxWidth,
    this.textAlign = TextAlign.start,
  });

  final String label;
  final Color foreground;
  final Color background;
  final Color? border;
  final double fontSize;
  final double? maxWidth;
  final TextAlign textAlign;

  @override
  Widget build(BuildContext context) {
    final Color labelColor = _labelColor();
    final Widget text = Text(
      label,
      maxLines: 1,
      textAlign: textAlign,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        color: labelColor,
        fontSize: fontSize,
        fontWeight: FontWeight.w700,
      ),
    );
    return Container(
      constraints: maxWidth == null
          ? null
          : BoxConstraints(maxWidth: maxWidth!),
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 2),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: border ?? foreground.withValues(alpha: 0.25)),
      ),
      child: maxWidth == null
          ? text
          : Tooltip(message: label, child: text),
    );
  }

  Color _labelColor() {
    if (foreground == AppTokens.accent && background == AppTokens.accentSoft) {
      return AppTokens.accentText;
    }
    if (foreground == AppTokens.warning && background == AppTokens.warningSoft) {
      return AppTokens.warningText;
    }
    return foreground;
  }
}
