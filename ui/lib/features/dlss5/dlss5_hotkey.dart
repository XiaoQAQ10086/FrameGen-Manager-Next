import 'package:flutter/services.dart';

/// Windows virtual-key mapping for the ReShade overlay hotkey.
final class Dlss5Hotkey {
  const Dlss5Hotkey._();

  /// Maps a USB HID usage to the Windows virtual-key code.
  static int? virtualKeyFor(PhysicalKeyboardKey key) {
    final int hid = key.usbHidUsage & 0xFFFF;
    if (hid >= 0x04 && hid <= 0x1D) {
      return 0x41 + (hid - 0x04);
    }
    if (hid >= 0x1E && hid <= 0x26) {
      return 0x31 + (hid - 0x1E);
    }
    if (hid == 0x27) {
      return 0x30;
    }
    if (hid >= 0x3A && hid <= 0x45) {
      return 0x70 + (hid - 0x3A);
    }
    return switch (hid) {
      0x28 => 0x0D,
      0x29 => 0x1B,
      0x2A => 0x08,
      0x2B => 0x09,
      0x2C => 0x20,
      0x2D => 0xBD,
      0x2E => 0xBB,
      0x2F => 0xDB,
      0x30 => 0xDD,
      0x31 => 0xDC,
      0x33 => 0xBA,
      0x34 => 0xDE,
      0x35 => 0xC0,
      0x36 => 0xBC,
      0x37 => 0xBE,
      0x38 => 0xBF,
      0x49 => 0x2D,
      0x4A => 0x24,
      0x4B => 0x21,
      0x4C => 0x2E,
      0x4D => 0x23,
      0x4E => 0x22,
      0x4F => 0x27,
      0x50 => 0x25,
      0x51 => 0x28,
      0x52 => 0x26,
      _ => null,
    };
  }

  static String label(int virtualKey) {
    if (virtualKey >= 0x41 && virtualKey <= 0x5A) {
      return String.fromCharCode(virtualKey);
    }
    if (virtualKey >= 0x30 && virtualKey <= 0x39) {
      return String.fromCharCode(virtualKey);
    }
    if (virtualKey >= 0x70 && virtualKey <= 0x7B) {
      return 'F${virtualKey - 0x70 + 1}';
    }
    return switch (virtualKey) {
      0x20 => 'Space',
      0x09 => 'Tab',
      0x0D => 'Enter',
      0x08 => 'Backspace',
      0x24 => 'Home',
      0x23 => 'End',
      0x21 => 'Page Up',
      0x22 => 'Page Down',
      0x2D => 'Insert',
      0x2E => 'Delete',
      0x25 => 'Left',
      0x26 => 'Up',
      0x27 => 'Right',
      0x28 => 'Down',
      _ => 'VK 0x${virtualKey.toRadixString(16).toUpperCase()}',
    };
  }
}
