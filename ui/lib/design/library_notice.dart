import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

class LibraryNotice extends StatelessWidget {
  const LibraryNotice({
    super.key,
    required this.message,
    required this.tone,
    required this.background,
  });

  final String message;
  final Color tone;
  final Color background;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(top: 10),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
        border: Border.all(color: tone.withValues(alpha: 0.25)),
      ),
      child: Text(
        message,
        style: TextStyle(color: tone, fontSize: 13),
      ),
    );
  }
}
