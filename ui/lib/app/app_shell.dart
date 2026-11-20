import 'package:material_ui/material_ui.dart';

import '../bridge/app_settings.dart';
import '../bridge/core_library.dart';
import '../bridge/settings_bridge.dart';
import '../design/app_tokens.dart';
import '../design/legacy_select.dart';
import '../design/shell_background.dart';
import '../features/about/about_page.dart';
import '../features/anr/anr_page.dart';
import '../features/dlss5/dlss5_page.dart';
import '../features/donate/donate_page.dart';
import '../features/framegen/framegen_page.dart';
import '../features/logs/logs_page.dart';
import '../features/smooth_motion/smooth_motion_page.dart';
import '../l10n/app_localizations.dart';
import 'app_section.dart';

/// Root layout mirroring the legacy topbar, navigation and page host.
class AppShell extends StatefulWidget {
  const AppShell({super.key, this.core, this.onLanguageChanged});

  final CoreLibrary? core;
  final ValueChanged<String>? onLanguageChanged;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  AppSection _section = AppSection.frameGeneration;
  late String _language = _loadLanguage();

  String _loadLanguage() {
    if (widget.core == null) {
      return 'system';
    }
    return SettingsBridge.load(widget.core).language;
  }

  void _selectLanguage(String language) {
    setState(() => _language = language);
    if (widget.core != null) {
      final AppSettings settings = SettingsBridge.load(widget.core);
      SettingsBridge.save(settings.copyWith(language: language), widget.core);
    }
    widget.onLanguageChanged?.call(language);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.transparent,
      body: Stack(
        children: <Widget>[
          const ShellBackground(),
          Column(
            children: <Widget>[
              _TopBar(
                core: widget.core,
                selected: _section,
                language: _language,
                onSelected: (AppSection section) =>
                    setState(() => _section = section),
                onLanguage: _selectLanguage,
              ),
              Expanded(
                child: AnimatedSwitcher(
                  duration: const Duration(milliseconds: 300),
                  switchInCurve: const Cubic(0.22, 0.61, 0.36, 1),
                  switchOutCurve: const Cubic(0.55, 0.06, 0.68, 0.19),
                  transitionBuilder: _fadeThrough,
                  layoutBuilder:
                      (Widget? current, List<Widget> previous) {
                        return Stack(
                          fit: StackFit.expand,
                          children: <Widget>[
                            ...previous,
                            if (current != null) current,
                          ],
                        );
                      },
                  child: KeyedSubtree(
                    key: ValueKey<AppSection>(_section),
                    child: switch (_section) {
                      AppSection.frameGeneration =>
                        FrameGenPage(core: widget.core),
                      AppSection.smoothMotion =>
                        SmoothMotionPage(core: widget.core),
                      AppSection.dlss5 => Dlss5Page(core: widget.core),
                      AppSection.anr => AnrPage(core: widget.core),
                      AppSection.logs => LogsPage(core: widget.core),
                      AppSection.about => AboutPage(core: widget.core),
                      AppSection.donate => const DonatePage(),
                    },
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _fadeThrough(Widget child, Animation<double> animation) {
    final Animation<Offset> offset = Tween<Offset>(
      begin: const Offset(0, 0.02),
      end: Offset.zero,
    ).animate(animation);

    return FadeTransition(
      opacity: animation,
      child: SlideTransition(position: offset, child: child),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({
    required this.core,
    required this.selected,
    required this.language,
    required this.onSelected,
    required this.onLanguage,
  });

  final CoreLibrary? core;
  final AppSection selected;
  final String language;
  final ValueChanged<AppSection> onSelected;
  final ValueChanged<String> onLanguage;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Container(
      constraints: const BoxConstraints(minHeight: 56),
      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 6),
      decoration: const BoxDecoration(
        color: Color(0xB8FFFFFF),
        border: Border(bottom: BorderSide(color: AppTokens.border)),
      ),
      child: LayoutBuilder(
        builder: (BuildContext context, BoxConstraints constraints) {
          final bool showTagline = constraints.maxWidth >= 1360;
          return Row(
            children: <Widget>[
              ClipRRect(
                borderRadius: BorderRadius.circular(9),
                child: Image.asset(
                  'assets/app.png',
                  width: 34,
                  height: 34,
                  fit: BoxFit.cover,
                ),
              ),
              const SizedBox(width: 10),
              Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  const Text(
                    'FrameGen Manager-Next',
                    style: TextStyle(
                      color: AppTokens.textPrimary,
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Text(
                        'v${core?.version ?? '2.3.4'}',
                        style: const TextStyle(
                          color: AppTokens.textSecondary,
                          fontSize: 11.5,
                        ),
                      ),
                      if (showTagline) ...<Widget>[
                        const SizedBox(width: 8),
                        Text(
                          l10n.appTagline,
                          style: const TextStyle(
                            color: AppTokens.textSecondary,
                            fontSize: 11.5,
                          ),
                        ),
                      ],
                    ],
                  ),
                ],
              ),
              const SizedBox(width: 22),
              Expanded(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: <Widget>[
                    Flexible(
                      child: SingleChildScrollView(
                        scrollDirection: Axis.horizontal,
                        child: Container(
                          padding: const EdgeInsets.all(3),
                          decoration: BoxDecoration(
                            color: const Color(0xFFEEF1F5),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
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
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              SizedBox(
                width: 152,
                child: _LanguageSelect(
                  language: language,
                  onChanged: onLanguage,
                ),
              ),
            ],
          );
        },
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
    final String glyph = switch (section) {
      AppSection.logs => '≡',
      AppSection.about => 'ⓘ',
      AppSection.donate => '♡',
      _ => '',
    };
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
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
            decoration: BoxDecoration(
              color: selected ? Colors.white : Colors.transparent,
              borderRadius: BorderRadius.circular(999),
              boxShadow: selected
                  ? const <BoxShadow>[
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
                    ]
                  : null,
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                if (glyph.isNotEmpty) ...<Widget>[
                  AnimatedDefaultTextStyle(
                    duration: const Duration(milliseconds: 180),
                    curve: Curves.easeOutCubic,
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
                  duration: const Duration(milliseconds: 180),
                  curve: Curves.easeOutCubic,
                  style: TextStyle(
                    color: selected
                        ? AppTokens.accent
                        : AppTokens.textSecondary,
                    fontSize: 13,
                    fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
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

class _LanguageSelect extends StatelessWidget {
  const _LanguageSelect({required this.language, required this.onChanged});

  final String language;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final String value = switch (language) {
      'chineseSimplified' => 'chineseSimplified',
      'english' => 'english',
      _ => 'system',
    };
    return LegacySelect<String>(
      value: value,
      dense: true,
      items: const <String>['system', 'chineseSimplified', 'english'],
      labelFor: (String item) => switch (item) {
        'chineseSimplified' => l10n.languageChinese,
        'english' => l10n.languageEnglish,
        _ => l10n.languageSystem,
      },
      onChanged: (String selected) {
        if (selected != value) {
          onChanged(selected);
        }
      },
    );
  }
}

String _sectionLabel(AppLocalizations l10n, AppSection section) {
  return switch (section) {
    AppSection.frameGeneration => l10n.navFrameGeneration,
    AppSection.smoothMotion => l10n.navSmoothMotion,
    AppSection.dlss5 => l10n.navDlss5,
    AppSection.anr => l10n.navAnr,
    AppSection.logs => l10n.navLogs,
    AppSection.about => l10n.navAbout,
    AppSection.donate => l10n.navDonate,
  };
}
