import 'package:material_ui/material_ui.dart';

import '../../design/app_tokens.dart';
import '../../design/legacy_switch.dart';

/// Title / hint row with a trailing switch, used by the About panels.
class AboutSwitchRow extends StatelessWidget {
  const AboutSwitchRow({
    super.key,
    required this.title,
    required this.hint,
    required this.value,
    required this.onChanged,
  });

  final String title;
  final String hint;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                title,
                style: const TextStyle(
                  color: AppTokens.textPrimary,
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                hint,
                style: const TextStyle(
                  color: AppTokens.textSecondary,
                  fontSize: 11.5,
                  height: 1.45,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: 12),
        LegacySwitch(value: value, onChanged: onChanged),
      ],
    );
  }
}
