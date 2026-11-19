import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

/// Legacy `.select` control: framed gradient trigger with a custom menu.
class LegacySelect<T> extends StatelessWidget {
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

  bool get _active => enabled && onChanged != null && items.isNotEmpty;

  @override
  Widget build(BuildContext context) {
    final String text = value == null ? '-' : labelFor(value as T);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        if (label != null) ...<Widget>[
          Text(
            label!,
            style: const TextStyle(
              color: AppTokens.textSecondary,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 5),
        ],
        Builder(
          builder: (BuildContext context) {
            return Semantics(
              button: true,
              label: text,
              enabled: _active,
              excludeSemantics: true,
              onTap: _active ? () => _open(context) : null,
              child: Opacity(
                opacity: _active ? 1 : 0.55,
                child: Material(
                  color: Colors.transparent,
                  child: InkWell(
                    borderRadius: BorderRadius.circular(AppTokens.radiusSmall),
                    onTap: _active ? () => _open(context) : null,
                    child: Container(
                      height: dense ? 31 : 34,
                      padding: EdgeInsets.only(
                        left: dense ? 10 : 11,
                        right: dense ? 9 : 11,
                      ),
                      decoration: BoxDecoration(
                        gradient: const LinearGradient(
                          begin: Alignment.topCenter,
                          end: Alignment.bottomCenter,
                          colors: <Color>[
                            Color(0xFFFFFFFF),
                            Color(0xFFF4F7FB),
                          ],
                        ),
                        border: Border.all(color: const Color(0x2E14233C)),
                        borderRadius: BorderRadius.circular(
                          AppTokens.radiusSmall,
                        ),
                        boxShadow: const <BoxShadow>[
                          BoxShadow(
                            color: Color(0x0F101828),
                            blurRadius: 3,
                            offset: Offset(0, 1),
                          ),
                        ],
                      ),
                      child: Row(
                        children: <Widget>[
                          Expanded(
                            child: Text(
                              text,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                color: AppTokens.textPrimary,
                                fontSize: dense ? 12 : 12.5,
                              ),
                            ),
                          ),
                          const SizedBox(width: 6),
                          const Icon(
                            Icons.keyboard_arrow_down_rounded,
                            size: 17,
                            color: AppTokens.textSecondary,
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            );
          },
        ),
      ],
    );
  }

  Future<void> _open(BuildContext context) async {
    final RenderBox box = context.findRenderObject()! as RenderBox;
    final RenderBox overlay =
        Overlay.of(context).context.findRenderObject()! as RenderBox;
    final Offset topLeft = box.localToGlobal(Offset.zero, ancestor: overlay);
    final double width = box.size.width;
    final RelativeRect position = RelativeRect.fromLTRB(
      topLeft.dx,
      topLeft.dy + box.size.height + 6,
      overlay.size.width - topLeft.dx - width,
      overlay.size.height - topLeft.dy - box.size.height - 6,
    );
    final T? picked = await showMenu<T>(
      context: context,
      position: position,
      color: const Color(0xFFFDFDFE),
      elevation: 12,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
        side: const BorderSide(color: Color(0x1F14233C)),
      ),
      constraints: BoxConstraints(
        minWidth: width,
        maxWidth: width,
        maxHeight: 268,
      ),
      items: <PopupMenuEntry<T>>[
        for (final T item in items)
          PopupMenuItem<T>(
            value: item,
            height: 34,
            padding: const EdgeInsets.symmetric(horizontal: 8),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    labelFor(item),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: item == value
                          ? AppTokens.accent
                          : AppTokens.textPrimary,
                      fontSize: 12.5,
                      fontWeight: item == value
                          ? FontWeight.w700
                          : FontWeight.w500,
                    ),
                  ),
                ),
                if (item == value)
                  const Icon(
                    Icons.check_rounded,
                    size: 15,
                    color: AppTokens.accent,
                  ),
              ],
            ),
          ),
      ],
    );
    if (picked != null && picked != value) {
      onChanged?.call(picked);
    }
  }
}
