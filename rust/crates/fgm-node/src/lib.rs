#![allow(unsafe_code)]

use napi::bindgen_prelude::{AsyncTask, Task};
use napi_derive::napi;
use serde_json::{Value, json};

/// Returns the product version embedded in the Rust crates.
#[napi]
pub fn version() -> String {
    fgm_domain::version::VERSION.to_string()
}

/// Runs a bridge command off the Electron main thread and returns its JSON
/// envelope as a JavaScript object.
#[napi]
pub fn invoke(name: String, payload: Option<Value>) -> AsyncTask<InvokeTask> {
    AsyncTask::new(InvokeTask { name, payload })
}

pub struct InvokeTask {
    name: String,
    payload: Option<Value>,
}

impl Task for InvokeTask {
    type Output = String;
    type JsValue = String;

    fn compute(&mut self) -> napi::Result<Self::Output> {
        let request = self
            .payload
            .as_ref()
            .map_or_else(|| "{}".to_string(), Value::to_string);
        fgm_ffi::rust_api::invoke(&self.name, Some(&request)).map_err(napi::Error::from_reason)
    }

    fn resolve(&mut self, _env: napi::Env, output: Self::Output) -> napi::Result<Self::JsValue> {
        Ok(output)
    }
}

/// Small helper for diagnostics from the main process.
#[napi]
pub fn command_names() -> Vec<String> {
    vec!["version".to_string(), "library.list".to_string()]
}

#[allow(dead_code)]
fn _unused() -> Value {
    json!({})
}
