/// One NVIDIA display adapter as stored in the registry.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DisplayAdapter {
    /// `VEN_10DE...\\<instance>` under `Enum\\PCI`.
    pub instance_path: String,
    /// Driver record key name, for example `0002`.
    pub driver_index: String,
    /// `DriverDesc` from the class key (the driver's own name).
    pub driver_name: String,
    /// Raw `DeviceDesc`, possibly an `@oem.inf...;Name` reference.
    pub device_desc: String,
    /// Human readable name shown by Windows.
    pub display_name: String,
}
