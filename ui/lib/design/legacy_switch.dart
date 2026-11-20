import 'package:material_ui/material_ui.dart';

import 'motion_tokens.dart';

/// Fixed-size pill switch: white knob, grey/blue track, identical knob in both
/// states (Material 3 changes the thumb size between states).
class LegacySwitch extends StatelessWidget {
  const LegacySwitch({super.key, required this.value, required this.onChanged});

  final bool value;
  final ValueChanged<bool>? onChanged;

  @override
  Widget build(BuildContext context) {
    final bool enabled = onChanged != null;
    return MouseRegion(
      cursor: enabled
          ? SystemMouseCursors.click
          : SystemMouseCursors.basic,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: enabled ? () => onChanged!(!value) : null,
        child: Opacity(
          opacity: enabled ? 1 : 0.5,
          child: AnimatedContainer(
            duration: MotionTokens.standard,
            curve: MotionTokens.enterCurve,
            width: 56,
            height: 32,
            padding: const EdgeInsets.all(3),
            decoration: BoxDecoration(
              color: value ? const Color(0xFF007AFF) : const Color(0xFFD9DDE3),
              borderRadius: BorderRadius.circular(999),
            ),
            child: AnimatedAlign(
              duration: MotionTokens.standard,
              curve: MotionTokens.enterCurve,
              alignment: value ? Alignment.centerRight : Alignment.centerLeft,
              child: Container(
                width: 26,
                height: 26,
                decoration: const BoxDecoration(
                  color: Colors.white,
                  shape: BoxShape.circle,
                  boxShadow: <BoxShadow>[
                    BoxShadow(
                      color: Color(0x1F101828),
                      blurRadius: 3,
                      offset: Offset(0, 1),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
