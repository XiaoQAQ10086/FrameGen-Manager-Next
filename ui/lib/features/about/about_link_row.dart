import 'package:material_ui/material_ui.dart';

import '../../design/app_tokens.dart';

/// Label / value row with a trailing action, used by the About panels.
class AboutLinkRow extends StatelessWidget {
  const AboutLinkRow({
    super.key,
    required this.label,
    required this.value,
    required this.action,
    required this.onPressed,
  });

  final String label;
  final String value;
  final String action;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SizedBox(
          width: 130,
          child: Text(
            label,
            style: const TextStyle(
              color: AppTokens.textSecondary,
              fontSize: 12.5,
            ),
          ),
        ),
        Expanded(
          child: Text(
            value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: AppTokens.textPrimary,
              fontSize: 12,
              height: 1.45,
            ),
          ),
        ),
        const SizedBox(width: 8),
        TextButton(onPressed: onPressed, child: Text(action)),
      ],
    );
  }
}
