import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';
import 'motion_tokens.dart';

/// Legacy `.select` control: centered framed trigger with a fast custom menu.
class LegacySelect<T> extends StatefulWidget {
  const LegacySelect({
    super.key,
    required this.value,
    required this.items,
    required this.labelFor,
    required this.onChanged,
    this.label,
    this.dense = false,
    this.enabled = true,
  });

  final T? value;
  final List<T> items;
  final String Function(T value) labelFor;
  final ValueChanged<T>? onChanged;
  final String? label;
  final bool dense;
  final bool enabled;

  @override
  State<LegacySelect<T>> createState() => _LegacySelectState<T>();
}

class _LegacySelectState<T> extends State<LegacySelect<T>> {
  final GlobalKey _triggerKey = GlobalKey();
  OverlayEntry? _entry;

  bool get _active =>
      widget.enabled && widget.onChanged != null && widget.items.isNotEmpty;

  String get _text =>
      widget.value == null ? '-' : widget.labelFor(widget.value as T);

  @override
  void dispose() {
    _remove();
    super.dispose();
  }

  void _remove() {
    _entry?.remove();
    _entry = null;
  }

  void _toggle() {
    if (_entry != null) {
      _remove();
      return;
    }
    final RenderBox? box =
        _triggerKey.currentContext?.findRenderObject() as RenderBox?;
    final OverlayState? overlay = Overlay.maybeOf(context);
    if (box == null || overlay == null) {
      return;
    }
    final Offset origin = box.localToGlobal(Offset.zero);
    _entry = OverlayEntry(
      builder: (BuildContext context) => _SelectOverlay<T>(
        origin: origin,
        width: box.size.width,
        triggerHeight: box.size.height,
        value: widget.value,
        items: widget.items,
        labelFor: widget.labelFor,
        onSelected: (T item) {
          _remove();
          if (item != widget.value) {
            widget.onChanged?.call(item);
          }
        },
        onDismiss: _remove,
      ),
    );
    overlay.insert(_entry!);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        if (widget.label != null) ...<Widget>[
          Text(
            widget.label!,
            style: const TextStyle(
              color: AppTokens.textSecondary,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 5),
        ],
        Semantics(
          button: true,
          label: _text,
          enabled: _active,
          excludeSemantics: true,
          onTap: _active ? _toggle : null,
          child: Opacity(
            opacity: _active ? 1 : 0.55,
            child: Material(
              color: Colors.transparent,
              child: InkWell(
                borderRadius: BorderRadius.circular(AppTokens.radiusSmall),
                onTap: _active ? _toggle : null,
                splashFactory: NoSplash.splashFactory,
                hoverColor: const Color(0x0F007AFF),
                mouseCursor: SystemMouseCursors.click,
                child: Container(
                  key: _triggerKey,
                  width: double.infinity,
                  height: widget.dense ? 31 : 34,
                  decoration: BoxDecoration(
                    gradient: const LinearGradient(
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                      colors: <Color>[Color(0xFFFFFFFF), Color(0xFFF4F7FB)],
                    ),
                    border: Border.all(color: const Color(0x2E14233C)),
                    borderRadius: BorderRadius.circular(AppTokens.radiusSmall),
                    boxShadow: const <BoxShadow>[
                      BoxShadow(
                        color: Color(0x0F101828),
                        blurRadius: 3,
                        offset: Offset(0, 1),
                      ),
                    ],
                  ),
                  child: Stack(
                    alignment: Alignment.centerLeft,
                    children: <Widget>[
                      Padding(
                        padding: const EdgeInsets.only(left: 12, right: 30),
                        child: Text(
                          _text,
                          maxLines: 1,
                          textAlign: TextAlign.start,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            color: AppTokens.textPrimary,
                            fontSize: widget.dense ? 12 : 13,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                      const Positioned(
                        right: 9,
                        child: Icon(
                          Icons.keyboard_arrow_down_rounded,
                          size: 17,
                          color: AppTokens.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// Full-screen overlay hosting the menu panel below the trigger.
class _SelectOverlay<T> extends StatefulWidget {
  const _SelectOverlay({
    required this.origin,
    required this.width,
    required this.triggerHeight,
    required this.value,
    required this.items,
    required this.labelFor,
    required this.onSelected,
    required this.onDismiss,
  });

  final Offset origin;
  final double width;
  final double triggerHeight;
  final T? value;
  final List<T> items;
  final String Function(T value) labelFor;
  final ValueChanged<T> onSelected;
  final VoidCallback onDismiss;

  @override
  State<_SelectOverlay<T>> createState() => _SelectOverlayState<T>();
}

class _SelectOverlayState<T> extends State<_SelectOverlay<T>>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: MotionTokens.quick,
  )..forward();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final CurvedAnimation curve = CurvedAnimation(
      parent: _controller,
      curve: MotionTokens.enterCurve,
    );
    return Stack(
      children: <Widget>[
        Positioned.fill(
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: widget.onDismiss,
            child: const ColoredBox(color: Colors.transparent),
          ),
        ),
        Positioned(
          left: widget.origin.dx,
          top: widget.origin.dy + widget.triggerHeight + 6,
          width: widget.width,
          child: FadeTransition(
            opacity: curve,
            child: ScaleTransition(
              alignment: Alignment.topCenter,
              scale: Tween<double>(begin: 0.97, end: 1).animate(curve),
              child: _MenuPanel<T>(
                value: widget.value,
                items: widget.items,
                labelFor: widget.labelFor,
                onSelected: widget.onSelected,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _MenuPanel<T> extends StatelessWidget {
  const _MenuPanel({
    required this.value,
    required this.items,
    required this.labelFor,
    required this.onSelected,
  });

  final T? value;
  final List<T> items;
  final String Function(T value) labelFor;
  final ValueChanged<T> onSelected;

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: const BoxConstraints(maxHeight: 268),
      padding: const EdgeInsets.all(5),
      decoration: BoxDecoration(
        color: const Color(0xFFFDFDFE),
        borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
        border: Border.all(color: const Color(0x1F14233C)),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x2E121E34),
            blurRadius: 24,
            offset: Offset(0, 10),
          ),
        ],
      ),
      child: Material(
        color: Colors.transparent,
        child: ListView(
          shrinkWrap: true,
          padding: EdgeInsets.zero,
          children: <Widget>[
            for (final T item in items)
              _MenuRow(
                label: labelFor(item),
                selected: item == value,
                onTap: () => onSelected(item),
              ),
          ],
        ),
      ),
    );
  }
}

class _MenuRow extends StatelessWidget {
  const _MenuRow({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 1),
      child: InkWell(
        borderRadius: BorderRadius.circular(8),
        onTap: onTap,
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
          decoration: selected
              ? BoxDecoration(
                  color: const Color(0xFFEAF3FF),
                  borderRadius: BorderRadius.circular(8),
                  boxShadow: const <BoxShadow>[
                    BoxShadow(
                      color: Color(0x14007AFF),
                      blurRadius: 6,
                      offset: Offset(0, 2),
                    ),
                  ],
                )
              : null,
          child: Text(
            label,
            maxLines: 1,
            textAlign: TextAlign.center,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: selected ? AppTokens.accent : AppTokens.textPrimary,
              fontSize: 13,
              fontWeight: selected ? FontWeight.w700 : FontWeight.w400,
            ),
          ),
        ),
      ),
    );
  }
}
