/// Formats a Windows path for display with an upper-case drive letter.
String displayPath(String value) {
  if (value.length >= 2 && value[1] == ':') {
    return value[0].toUpperCase() + value.substring(1);
  }
  return value;
}
