import 'dart:ui';

import 'package:fgm_ui/app/app.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';

void main() {
  testWidgets('renders the shell in English', (WidgetTester tester) async {
    tester.view.physicalSize = const Size(1280, 850);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const FgmApp(locale: Locale('en')),
    );
    await tester.pumpAndSettle();

    expect(find.text('FrameGen Manager-Next'), findsOneWidget);
    expect(find.text('Rust core not loaded'), findsWidgets);
    expect(find.text('Game library'), findsOneWidget);
  });

  testWidgets('renders the shell in Chinese', (WidgetTester tester) async {
    tester.view.physicalSize = const Size(1280, 850);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const FgmApp(locale: Locale('zh')),
    );
    await tester.pumpAndSettle();

    expect(find.text('游戏库'), findsOneWidget);
    expect(find.text('Rust 核心未加载'), findsWidgets);
  });
}
