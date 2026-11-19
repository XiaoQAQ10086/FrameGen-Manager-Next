import 'package:material_ui/material_ui.dart';

import '../bridge/core_library.dart';
import '../design/app_tokens.dart';
import '../features/framegen/framegen_page.dart';
import '../l10n/app_localizations.dart';
import 'app_section.dart';

/// Root layout: top navigation, page host and status bar.
class AppShell extends StatefulWidget {
  const AppShell({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  AppSection _section = AppSection.frameGeneration;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Column(
        children: <Widget>[
          _TopBar(
            selected: _section,
            onSelected: (AppSection section) =>
                setState(() => _section = section),
          ),
          Expanded(
            child: AnimatedSwitcher(
              duration: const Duration(milliseconds: 180),
              switchInCurve: Curves.easeOutCubic,
              switchOutCurve: Curves.easeInCubic,
              transitionBuilder: _fadeThrough,
              child: KeyedSubtree(
                key: ValueKey<AppSection>(_section),
                child: _section == AppSection.frameGeneration
                    ? FrameGenPage(core: widget.core)
                    : _PlaceholderPage(section: _section),
              ),
            ),
          ),
          _StatusBar(core: widget.core),
        ],
      ),
    );
  }

  Widget _fadeThrough(Widget child, Animation<double> animation) {
    final Animation<Offset> offset = Tween<Offset>(
      begin: const Offset(0, 0.02),
      end: Offset.zero,
    ).animate(CurvedAnimation(parent: animation, curve: Curves.easeOutCubic));

    return FadeTransition(
      opacity: animation,
      child: SlideTransition(position: offset, child: child),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.selected, required this.onSelected});

  final AppSection selected;
  final ValueChanged<AppSection> onSelected;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);

    return Container(
      height: 72,
      padding: const EdgeInsets.symmetric(horizontal: 24),
      decoration: const BoxDecoration(
        color: AppTokens.surface,
        border: Border(bottom: BorderSide(color: AppTokens.border)),
      ),
      child: Row(
        children: <Widget>[
          const _BrandMark(),
          const SizedBox(width: 12),
          Text(
            l10n.appTitle,
            style: const TextStyle(
              color: AppTokens.textPrimary,
              fontSize: 15,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(width: 28),
          Expanded(
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: <Widget>[
                  for (final AppSection section in AppSection.values)
                    _NavItem(
                      section: section,
                      label: _sectionLabel(l10n, section),
                      selected: section == selected,
                      onTap: () => onSelected(section),
                    ),
                ],
              ),
            ),
          ),
          const SizedBox(width: 16),
          _Badge(label: l10n.phaseBadge),
        ],
      ),
    );
  }
}

class _BrandMark extends StatelessWidget {
  const _BrandMark();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 36,
      height: 36,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(11),
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: <Color>[AppTokens.accent, Color(0xFF5AC8FA)],
        ),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x330A84FF),
            blurRadius: 12,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: const Text(
        'F',
        style: TextStyle(
          color: Colors.white,
          fontSize: 18,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.section,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final AppSection section;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 2),
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          curve: Curves.easeOutCubic,
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
          decoration: BoxDecoration(
            color: selected ? AppTokens.accentSoft : Colors.transparent,
            borderRadius: BorderRadius.circular(10),
          ),
          child: Row(
            children: <Widget>[
              Icon(
                _sectionIcon(section),
                size: 17,
                color: selected
                    ? AppTokens.accent
                    : AppTokens.textSecondary,
              ),
              const SizedBox(width: 7),
              Text(
                label,
                style: TextStyle(
                  color: selected
                      ? AppTokens.accent
                      : AppTokens.textSecondary,
                  fontSize: 13,
                  fontWeight:
                      selected ? FontWeight.w600 : FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _PlaceholderPage extends StatelessWidget {
  const _PlaceholderPage({required this.section});

  final AppSection section;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);

    return Padding(
      padding: AppTokens.pagePadding,
      child: Container(
        decoration: BoxDecoration(
          color: AppTokens.surface,
          borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
          border: Border.all(color: AppTokens.border),
          boxShadow: AppTokens.panelShadow,
        ),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Container(
                width: 56,
                height: 56,
                decoration: const BoxDecoration(
                  color: AppTokens.accentSoft,
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  _sectionIcon(section),
                  color: AppTokens.accent,
                  size: 26,
                ),
              ),
              const SizedBox(height: 18),
              Text(
                _sectionLabel(l10n, section),
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 8),
              Text(
                l10n.sectionPending,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _StatusBar extends StatelessWidget {
  const _StatusBar({this.core});

  final CoreLibrary? core;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String coreLabel = core == null
        ? l10n.coreOffline
        : l10n.footerCore(core!.version);

    return Container(
      height: 30,
      padding: const EdgeInsets.symmetric(horizontal: 24),
      decoration: const BoxDecoration(
        color: AppTokens.surface,
        border: Border(top: BorderSide(color: AppTokens.border)),
      ),
      child: Row(
        children: <Widget>[
          Container(
            width: 7,
            height: 7,
            decoration: BoxDecoration(
              color: core == null ? AppTokens.warning : AppTokens.success,
              shape: BoxShape.circle,
            ),
          ),
          const SizedBox(width: 8),
          Text(coreLabel, style: Theme.of(context).textTheme.labelMedium),
          const Spacer(),
          Text(
            l10n.footerPlatform,
            style: Theme.of(context).textTheme.labelMedium,
          ),
        ],
      ),
    );
  }
}

class _Badge extends StatelessWidget {
  const _Badge({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: AppTokens.accentSoft,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: AppTokens.accent,
          fontSize: 11,
          fontWeight: FontWeight.w600,
          letterSpacing: 0.2,
        ),
      ),
    );
  }
}

String _sectionLabel(AppLocalizations l10n, AppSection section) {
  return switch (section) {
    AppSection.frameGeneration => l10n.navFrameGeneration,
    AppSection.smoothMotion => l10n.navSmoothMotion,
    AppSection.dlss5 => l10n.navDlss5,
    AppSection.anr => l10n.navAnr,
    AppSection.gpu => l10n.navGpu,
    AppSection.logs => l10n.navLogs,
    AppSection.settings => l10n.navSettings,
    AppSection.about => l10n.navAbout,
  };
}

IconData _sectionIcon(AppSection section) {
  return switch (section) {
    AppSection.frameGeneration => Icons.auto_awesome_motion,
    AppSection.smoothMotion => Icons.motion_photos_on,
    AppSection.dlss5 => Icons.hdr_strong,
    AppSection.anr => Icons.memory,
    AppSection.gpu => Icons.developer_board,
    AppSection.logs => Icons.receipt_long,
    AppSection.settings => Icons.tune,
    AppSection.about => Icons.info_outline,
  };
}
