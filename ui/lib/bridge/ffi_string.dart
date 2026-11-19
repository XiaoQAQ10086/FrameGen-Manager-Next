import 'dart:convert';
import 'dart:ffi';

/// Reads a NUL-terminated UTF-8 string returned by the Rust core.
String readCString(Pointer<Int8> pointer) {
  final List<int> bytes = <int>[];
  var index = 0;
  while (true) {
    final int unit = pointer[index];
    if (unit == 0) {
      break;
    }
    bytes.add(unit & 0xFF);
    index++;
  }
  return utf8.decode(bytes, allowMalformed: true);
}
