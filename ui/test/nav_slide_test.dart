import 'package:fgm_ui/app/app.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';

/// Pins the sliding selection pill on the top-bar navigation.
void main() {
  testWidgets('the pill slides to the newly selected tab', (
    WidgetTester tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 850);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(const FgmApp(locale: Locale('en')));
    await tester.pumpAndSettle();

    final Finder pill = find.byKey(const ValueKey<String>('nav-pill'));
    expect(pill, findsOneWidget);
    final Rect before = tester.getRect(pill);

    await tester.tap(find.text('About').first);
    await tester.pumpAndSettle();

    final Rect after = tester.getRect(pill);
    expect(after.left, greaterThan(before.left));
    expect(tester.takeException(), isNull);
  });
}
