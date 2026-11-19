part of 'core_library.dart';

/// Sends a request buffer and frees the native reply.
String? _requestJson(
  _RequestDart function,
  String request,
  _AllocDart alloc,
  _StringFreeDart freeString,
  _FreeBytesDart freeBytes,
) {
  final List<int> bytes = utf8.encode(request);
  final Pointer<Uint8> buffer = alloc(bytes.length);
  if (buffer.address == 0) {
    return null;
  }
  try {
    buffer.asTypedList(bytes.length).setAll(0, bytes);
    final Pointer<Int8> pointer = function(buffer, bytes.length);
    if (pointer.address == 0) {
      return null;
    }
    try {
      return readCString(pointer);
    } finally {
      freeString(pointer);
    }
  } finally {
    freeBytes(buffer, bytes.length);
  }
}

/// Reads and frees a no-argument JSON reply.
String? _jsonString(_JsonDart function, _StringFreeDart freeString) {
  final Pointer<Int8> pointer = function();
  if (pointer.address == 0) {
    return null;
  }
  try {
    return readCString(pointer);
  } finally {
    freeString(pointer);
  }
}
