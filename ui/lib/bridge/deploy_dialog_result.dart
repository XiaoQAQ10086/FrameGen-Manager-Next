import 'deploy_options.dart';

/// Outcome of the frame-generation deploy modal.
final class DeployDialogResult {
  const DeployDialogResult.deploy(this.options) : restore = false;

  const DeployDialogResult.restore()
    : options = null,
      restore = true;

  final DeployOptions? options;
  final bool restore;
}
