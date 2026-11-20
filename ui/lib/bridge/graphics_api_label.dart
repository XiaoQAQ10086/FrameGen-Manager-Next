/// Rendering of the graphics API key returned by the core.
///
/// The core emits lower-case keys (`dx12`, `vulkan`); every screen shows them
/// upper case so the same game reads the same way on cards, pills and dialogs.
abstract final class GraphicsApiLabel {
  static String of(String api) => api.toUpperCase();
}
