import 'package:fgm_ui/features/dlss5/dlss5_hotkey.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('maps letters, digits and function keys to virtual keys', () {
    expect(Dlss5Hotkey.virtualKeyFor(PhysicalKeyboardKey.keyA), 0x41);
    expect(Dlss5Hotkey.virtualKeyFor(PhysicalKeyboardKey.keyZ), 0x5A);
    expect(Dlss5Hotkey.virtualKeyFor(PhysicalKeyboardKey.digit1), 0x31);
    expect(Dlss5Hotkey.virtualKeyFor(PhysicalKeyboardKey.digit0), 0x30);
    expect(Dlss5Hotkey.virtualKeyFor(PhysicalKeyboardKey.f1), 0x70);
    expect(Dlss5Hotkey.virtualKeyFor(PhysicalKeyboardKey.f12), 0x7B);
    expect(Dlss5Hotkey.virtualKeyFor(PhysicalKeyboardKey.home), 0x24);
    expect(Dlss5Hotkey.virtualKeyFor(PhysicalKeyboardKey.arrowLeft), 0x25);
  });

  test('formats labels for the dialog', () {
    expect(Dlss5Hotkey.label(0x48), 'H');
    expect(Dlss5Hotkey.label(0x70), 'F1');
    expect(Dlss5Hotkey.label(0x24), 'Home');
    expect(Dlss5Hotkey.label(0xFE), 'VK 0xFE');
  });
}
