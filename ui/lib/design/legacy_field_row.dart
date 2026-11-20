import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

/// Legacy form row: a fixed-width label on the left, the control on the right.
///
/// The deploy dialogs line every option up in this grid; putting the label
/// above the control (Material's default) makes the forms read as two columns
/// of stacked blocks instead of the legacy list.
class LegacyFieldRow extends StatelessWidget {
  const LegacyFieldRow({super.key, required this.label, required this.child});

  /// Wide enough for the longest option label, so every control starts level.
  static const double labelWidth = 72;

  final String label;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        SizedBox(
          width: labelWidth,
          child: Text(
            label,
            style: const TextStyle(
              color: AppTokens.textSecondary,
              fontSize: 12,
            ),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(child: child),
      ],
    );
  }
}
