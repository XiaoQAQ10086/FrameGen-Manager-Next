import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Guards the design tokens documented in `docs/behavior/ui-design.md`.
///
/// These tests read the Dart sources instead of the widget tree: the drift they
/// catch is a hardcoded `fontSize` or `Curves.*` value, which no widget test can
/// see once it renders correctly.
void main() {
  final List<File> sources = Directory('lib')
      .listSync(recursive: true)
      .whereType<File>()
      .where((File file) => file.path.endsWith('.dart'))
      .where((File file) => !_norm(file).contains('/l10n/'))
      .toList();

  test('sources are discoverable', () {
    expect(sources.length, greaterThan(50));
  });

  test('font sizes stay on the eight-step scale', () {
    final Set<double> allowed = <double>{11, 12, 13, 14, 16, 20, 28, 44};
    final RegExp pattern = RegExp(r'fontSize:\s*([0-9]+(?:\.[0-9]+)?)');
    expect(
      _violations(sources, pattern, (RegExpMatch match) {
        final double size = double.parse(match.group(1)!);
        return allowed.contains(size) ? null : 'fontSize: $size';
      }),
      isEmpty,
      reason: 'Allowed sizes are $allowed (see docs/behavior/ui-design.md)',
    );
  });

  test('font weights stay within the four-step range', () {
    const Set<String> allowed = <String>{'w400', 'w500', 'w600', 'w700'};
    final RegExp pattern = RegExp(r'FontWeight\.(w[0-9]{3})');
    expect(
      _violations(sources, pattern, (RegExpMatch match) {
        final String weight = match.group(1)!;
        return allowed.contains(weight) ? null : 'FontWeight.$weight';
      }),
      isEmpty,
      reason: 'Allowed weights are $allowed',
    );
  });

  test('easing curves come from MotionTokens', () {
    final RegExp pattern = RegExp(r'\bCurves\.[A-Za-z]+|\bCubic\(');
    final List<File> offenders = sources
        .where((File file) => !_norm(file).endsWith('/design/motion_tokens.dart'))
        .where((File file) => pattern.hasMatch(file.readAsStringSync()))
        .toList();
    expect(
      offenders.map(_norm).toList(),
      isEmpty,
      reason: 'Reference MotionTokens instead of raw curves',
    );
  });
}

List<String> _violations(
  List<File> sources,
  RegExp pattern,
  String? Function(RegExpMatch match) check,
) {
  final List<String> found = <String>[];
  for (final File file in sources) {
    final List<String> lines = file.readAsLinesSync();
    for (int i = 0; i < lines.length; i++) {
      for (final RegExpMatch match in pattern.allMatches(lines[i])) {
        final String? problem = check(match);
        if (problem != null) {
          found.add('${_norm(file)}:${i + 1}  $problem');
        }
      }
    }
  }
  return found;
}

String _norm(File file) => file.path.replaceAll(r'\', '/');
