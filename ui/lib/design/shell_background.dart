import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

/// Gradient backdrop with the legacy soft radial glows.
class ShellBackground extends StatelessWidget {
  const ShellBackground({super.key});

  @override
  Widget build(BuildContext context) {
    return const DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: <Color>[AppTokens.background, AppTokens.backgroundEnd],
        ),
      ),
      child: Stack(
        children: <Widget>[
          _Glow(
            alignment: Alignment(-0.84, -1.0),
            color: Color(0x3876BAFF),
            size: 1000,
          ),
          _Glow(
            alignment: Alignment(0.92, -1.0),
            color: Color(0x21007AFF),
            size: 900,
          ),
          _Glow(
            alignment: Alignment(0.6, 1.0),
            color: Color(0x2478D2B4),
            size: 800,
          ),
        ],
      ),
    );
  }
}

class _Glow extends StatelessWidget {
  const _Glow({
    required this.alignment,
    required this.color,
    required this.size,
  });

  final Alignment alignment;
  final Color color;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: alignment,
      child: IgnorePointer(
        child: Container(
          width: size,
          height: size * 0.55,
          decoration: BoxDecoration(
            gradient: RadialGradient(
              colors: <Color>[color, color.withValues(alpha: 0)],
            ),
          ),
        ),
      ),
    );
  }
}
