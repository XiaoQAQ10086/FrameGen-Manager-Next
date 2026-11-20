import 'package:material_ui/material_ui.dart';

import '../../design/app_tokens.dart';
import '../../design/library_list_head.dart';
import '../../design/library_notice.dart';

/// Layout shell for the frame-generation workspace: list header, notices and
/// the wide/narrow arrangement of sidebar plus content.
class FrameGenWorkspace extends StatelessWidget {
  const FrameGenWorkspace({
    super.key,
    required this.title,
    required this.status,
    required this.removedCount,
    required this.scanning,
    required this.adding,
    required this.errorNotice,
    required this.outdatedNotice,
    required this.sidebar,
    required this.content,
    required this.onAdd,
    required this.onRemoved,
    required this.onScan,
  });

  final String title;
  final String status;
  final int removedCount;
  final bool scanning;
  final bool adding;
  final String? errorNotice;
  final String? outdatedNotice;
  final Widget sidebar;
  final Widget content;
  final VoidCallback onAdd;
  final VoidCallback onRemoved;
  final VoidCallback onScan;

  @override
  Widget build(BuildContext context) {
    final String? error = errorNotice;
    final String? outdated = outdatedNotice;
    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          LibraryListHead(
            title: title,
            status: status,
            removedCount: removedCount,
            scanning: scanning,
            adding: adding,
            onAdd: onAdd,
            onRemoved: onRemoved,
            onScan: onScan,
          ),
          if (error != null)
            LibraryNotice(
              message: error,
              tone: AppTokens.warning,
              background: AppTokens.warningSoft,
            ),
          if (outdated != null)
            LibraryNotice(
              message: outdated,
              tone: AppTokens.accent,
              background: AppTokens.accentSoft,
            ),
          const SizedBox(height: 14),
          Expanded(
            child: LayoutBuilder(
              builder: (BuildContext context, BoxConstraints constraints) {
                final bool wide = constraints.maxWidth >= 980;
                if (!wide) {
                  return ListView(
                    children: <Widget>[
                      sidebar,
                      const SizedBox(height: AppTokens.gap),
                      SizedBox(height: 460, child: content),
                    ],
                  );
                }
                return Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    SizedBox(width: 340, child: sidebar),
                    const SizedBox(width: 18),
                    Expanded(child: content),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
