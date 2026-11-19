import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

/// Rounded legacy badge used for states, launchers and proxy owners.
class LegacyPill extends StatelessWidget {
  const LegacyPill({
    super.key,
    required this.label,
    this.foreground = AppTokens.textSecondary,
    this.background = AppTokens.surfaceMuted,
    this.border,
    this.fontSize = 11.5,
    this.maxWidth,
  });

  final String label;
  final Color foreground;
  final Color background;
  final Color? border;
  final double fontSize;
  final double? maxWidth;

  @override
  Widget build(BuildContext context) {
    final Widget text = Text(
      label,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        color: foreground,
        fontSize: fontSize,
        fontWeight: FontWeight.w600,
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
}
