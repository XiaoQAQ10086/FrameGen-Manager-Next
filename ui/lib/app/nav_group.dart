import 'package:material_ui/material_ui.dart';

import '../design/app_tokens.dart';
import '../design/motion_tokens.dart';

/// Segmented top-bar navigation whose selection pill slides between items.
class NavGroup extends StatefulWidget {
  const NavGroup({
    super.key,
    required this.entries,
    required this.selected,
    required this.onSelected,
  });

  final List<({String glyph, String label})> entries;
  final int selected;
  final ValueChanged<int> onSelected;

  @override
  State<NavGroup> createState() => _NavGroupState();
}

class _NavGroupState extends State<NavGroup> {
  final GlobalKey _stackKey = GlobalKey();
  late final List<GlobalKey> _itemKeys = List<GlobalKey>.generate(
    widget.entries.length,
    (int _) => GlobalKey(),
  );
  Rect? _pill;

  @override
  void initState() {
    super.initState();
    _scheduleMeasure();
  }

  @override
  void didUpdateWidget(NavGroup oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.selected != widget.selected ||
        oldWidget.entries.length != widget.entries.length) {
      _scheduleMeasure();
    }
  }

  void _scheduleMeasure() {
    WidgetsBinding.instance.addPostFrameCallback((_) => _measure());
  }

  void _measure() {
    if (!mounted) {
      return;
    }
    final RenderBox? stack =
        _stackKey.currentContext?.findRenderObject() as RenderBox?;
    final int index = widget.selected;
    if (stack == null || index < 0 || index >= _itemKeys.length) {
      return;
    }
    final RenderBox? item =
        _itemKeys[index].currentContext?.findRenderObject() as RenderBox?;
    if (item == null || !item.hasSize) {
      return;
    }
    final Rect rect = item.localToGlobal(Offset.zero, ancestor: stack) & item.size;
    if (rect != _pill) {
      setState(() => _pill = rect);
    }
  }

  @override
  Widget build(BuildContext context) {
    final Rect? pill = _pill;
    return Container(
      padding: const EdgeInsets.all(3),
      decoration: BoxDecoration(
        color: const Color(0xFFEEF1F5),
        borderRadius: BorderRadius.circular(12),
      ),
      child: NotificationListener<SizeChangedLayoutNotification>(
        // A window resize moves the items without changing the selection.
        onNotification: (SizeChangedLayoutNotification _) {
          _scheduleMeasure();
          return false;
        },
        child: SizeChangedLayoutNotifier(
          child: Stack(
            key: _stackKey,
            children: <Widget>[
              if (pill != null)
                AnimatedPositioned(
                  key: const ValueKey<String>('nav-pill'),
                  // The first measurement lands after the first frame;
                  // snapping it in avoids a pill flying in from the corner.
                  duration: _animated ? MotionTokens.standard : Duration.zero,
                  curve: MotionTokens.enterCurve,
                  left: pill.left,
                  top: pill.top,
                  width: pill.width,
                  height: pill.height,
                  child: const _SelectionPill(),
                ),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  for (var index = 0; index < widget.entries.length; index++)
                    _NavItem(
                      key: _itemKeys[index],
                      glyph: widget.entries[index].glyph,
                      label: widget.entries[index].label,
                      selected: index == widget.selected,
                      onTap: () => widget.onSelected(index),
                    ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  bool get _animated => _pill != null;
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    super.key,
    required this.glyph,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String glyph;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 2),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(999),
          onTap: onTap,
          splashFactory: NoSplash.splashFactory,
          highlightColor: Colors.transparent,
          hoverColor: const Color(0x14007AFF),
          focusColor: Colors.transparent,
          mouseCursor: SystemMouseCursors.click,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                if (glyph.isNotEmpty) ...<Widget>[
                  AnimatedDefaultTextStyle(
                    duration: MotionTokens.standard,
                    curve: MotionTokens.enterCurve,
                    style: TextStyle(
                      color: selected
                          ? AppTokens.accent
                          : AppTokens.textSecondary,
                      fontSize: 14,
                    ),
                    child: Text(glyph),
                  ),
                  const SizedBox(width: 4),
                ],
                AnimatedDefaultTextStyle(
                  duration: MotionTokens.standard,
                  curve: MotionTokens.enterCurve,
                  style: TextStyle(
                    color: selected
                        ? AppTokens.accent
                        : AppTokens.textSecondary,
                    // 500 rather than 700: the system face only ships Regular
                    // and Bold, and Bold at this size closes up dense glyphs
                    // such as 赏.
                    fontSize: 14,
                    fontWeight: FontWeight.w500,
                  ),
                  child: Text(label),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// White capsule with the legacy drop shadow, drawn behind the nav items.
class _SelectionPill extends StatelessWidget {
  const _SelectionPill();

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(999),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x1A18274B),
            blurRadius: 8,
            offset: Offset(0, 2),
          ),
          BoxShadow(
            color: Color(0x0F18274B),
            blurRadius: 2,
            offset: Offset(0, 1),
          ),
        ],
      ),
    );
  }
}
